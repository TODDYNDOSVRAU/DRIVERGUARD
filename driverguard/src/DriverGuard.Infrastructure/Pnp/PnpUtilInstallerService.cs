using System.Diagnostics;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Serilog;

namespace DriverGuard.Infrastructure.Pnp;

/// <summary>
/// Executa a instalação de drivers através do pnputil.exe, garantindo ponto de restauração,
/// backup atômico, auditoria no histórico SQLite e notificações Toast do Windows.
/// </summary>
public class PnpUtilInstallerService : IDriverInstallerService
{
    private readonly ISystemRestoreService _restoreService;
    private readonly IDriverBackupService _backupService;
    private readonly IPendingRebootDetector _rebootDetector;
    private readonly IDeviceInventoryService _inventoryService;
    private readonly IHistoryRepository _historyRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger _logger = Log.ForContext<PnpUtilInstallerService>();

    public PnpUtilInstallerService(
        ISystemRestoreService restoreService,
        IDriverBackupService backupService,
        IPendingRebootDetector rebootDetector,
        IDeviceInventoryService inventoryService,
        IHistoryRepository historyRepository,
        INotificationService notificationService)
    {
        _restoreService = restoreService;
        _backupService = backupService;
        _rebootDetector = rebootDetector;
        _inventoryService = inventoryService;
        _historyRepository = historyRepository;
        _notificationService = notificationService;
    }

    public async Task<InstallationResult> InstallDriverAsync(
        UpdateCandidate candidate,
        bool isSimulation = false,
        CancellationToken cancellationToken = default)
    {
        var device = candidate.TargetDevice;
        var pkg = candidate.NewDriverPackage;

        // 1. Bloqueio de Firmware / BIOS
        if (device.IsFirmwareOrBios)
        {
            string blockedMsg = "Instalação bloqueada: o DriverGuard não instala atualizações de firmware ou BIOS.";
            _logger.Error(blockedMsg);

            await _historyRepository.AddRecordAsync(new OperationHistoryRecord
            {
                DeviceName = device.Name,
                HardwareId = device.PrimaryHardwareId,
                OperationType = "Instalação",
                PreviousVersion = device.DriverVersion,
                NewVersion = candidate.NewVersion,
                Source = candidate.SourceName,
                ResultStatus = "Bloqueado",
                ErrorMessage = blockedMsg,
                IsSimulation = isSimulation
            });

            return new InstallationResult { Success = false, Message = blockedMsg };
        }

        // 2. Validação de Assinatura Digital
        if (!candidate.IsSignatureValid)
        {
            string sigError = pkg.SignatureResult?.ErrorMessage ?? "Pacote não possui catálogo ou assinatura digital válida.";
            _logger.Error("Instalação bloqueada para {Dev}: {Err}", device.Name, sigError);

            _notificationService.ShowInvalidSignatureToast(device.Name, sigError);

            await _historyRepository.AddRecordAsync(new OperationHistoryRecord
            {
                DeviceName = device.Name,
                HardwareId = device.PrimaryHardwareId,
                OperationType = "Instalação",
                PreviousVersion = device.DriverVersion,
                NewVersion = candidate.NewVersion,
                Source = candidate.SourceName,
                ResultStatus = "Assinatura Inválida",
                ErrorMessage = sigError,
                IsSimulation = isSimulation
            });

            return new InstallationResult
            {
                Success = false,
                Message = $"Instalação bloqueada: {sigError}"
            };
        }

        // 3. Bloqueio por Reinício Pendente
        if (_rebootDetector.IsRebootPending(out string pendingReason))
        {
            _logger.Warning("Instalação bloqueada devido a reinicialização pendente: {Reason}", pendingReason);
            return new InstallationResult
            {
                Success = false,
                Message = $"Instalação bloqueada: {pendingReason}"
            };
        }

        // 4. Modo Simulação
        if (isSimulation)
        {
            _logger.Information("[MODO SIMULAÇÃO] Instalando {Dev}...", device.Name);
            await _restoreService.CreateRestorePointAsync($"Instalação: {device.Name} ({candidate.NewVersion})", true);
            await _backupService.ExportCurrentDriverAsync(device, true);

            await _historyRepository.AddRecordAsync(new OperationHistoryRecord
            {
                DeviceName = device.Name,
                HardwareId = device.PrimaryHardwareId,
                OperationType = "Instalação",
                PreviousVersion = device.DriverVersion,
                NewVersion = candidate.NewVersion,
                Source = candidate.SourceName,
                ResultStatus = "Simulado",
                ErrorMessage = string.Empty,
                IsSimulation = true
            });

            return InstallationResult.Simulation(device.Name, candidate.NewVersion);
        }

        // 5. Ponto de Restauração
        var restoreResult = await _restoreService.CreateRestorePointAsync($"DriverGuard: {device.Name} ({candidate.NewVersion})");

        // 6. Backup Atômico do DriverStore
        var backup = await _backupService.ExportCurrentDriverAsync(device, false);
        if (backup == null)
        {
            return new InstallationResult
            {
                Success = false,
                Message = "Instalação abortada: falha ao criar backup do driver atual no DriverStore."
            };
        }

        // 7. Execução do pnputil /add-driver /install
        return await Task.Run(async () =>
        {
            int exitCode = -1;
            string stdout = string.Empty;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = $"/add-driver \"{pkg.InfPath}\" /install",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) throw new InvalidOperationException("Falha ao iniciar pnputil.");

                stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit(90000);
                exitCode = process.ExitCode;
            }
            catch (Exception ex)
            {
                exitCode = -1;
                stdout = ex.Message;
            }

            bool processSuccess = exitCode == 0 || exitCode == 3010;
            uint postInstallError = 0;

            if (processSuccess)
            {
                await Task.Delay(2500, cancellationToken);
                var auditedDevice = await _inventoryService.GetDeviceByIdAsync(device.DeviceId, cancellationToken);
                if (auditedDevice != null)
                {
                    postInstallError = auditedDevice.ConfigManagerErrorCode;
                }
            }

            // GATILHO DE REVERSÃO AUTOMÁTICA
            if (!processSuccess || postInstallError != 0)
            {
                string reason = !processSuccess 
                    ? $"Falha no processo pnputil (código {exitCode})" 
                    : $"Dispositivo entrou em estado de erro (código PnP {postInstallError})";

                _logger.Error("FALHA NA INSTALAÇÃO: {Reason}. Disparando reversão automática...", reason);

                // Dispara Toast 1 (Falha) ou Toast 4 (Erro PnP)
                if (postInstallError != 0)
                    _notificationService.ShowDeviceInErrorToast(device.Name, postInstallError, reason);
                else
                    _notificationService.ShowInstallationFailedToast(device.Name, reason);

                // Executa reversão automática
                bool rollbackOk = await ExecuteRollbackProcessAsync(backup.PrimaryInfPath);

                // Dispara Toast 2 (Reversão Executada)
                _notificationService.ShowRollbackExecutedToast(device.Name, backup.DriverVersion, isAutomatic: true);

                await _historyRepository.AddRecordAsync(new OperationHistoryRecord
                {
                    DeviceName = device.Name,
                    HardwareId = device.PrimaryHardwareId,
                    OperationType = "Instalação (Revertida Auto)",
                    PreviousVersion = device.DriverVersion,
                    NewVersion = candidate.NewVersion,
                    Source = candidate.SourceName,
                    ResultStatus = "Falha / Revertido",
                    ErrorMessage = $"{reason}. Reversão automática: {(rollbackOk ? "Sucesso" : "Falhou")}.",
                    IsSimulation = false
                });

                return InstallationResult.FailedAndRolledBack(exitCode, reason, postInstallError, rollbackOk);
            }

            bool needsReboot = exitCode == 3010;
            string successMsg = needsReboot
                ? "Driver instalado com sucesso! Reinicialização do computador é necessária."
                : "Driver instalado com êxito e operando perfeitamente!";

            await _historyRepository.AddRecordAsync(new OperationHistoryRecord
            {
                DeviceName = device.Name,
                HardwareId = device.PrimaryHardwareId,
                OperationType = "Instalação",
                PreviousVersion = device.DriverVersion,
                NewVersion = candidate.NewVersion,
                Source = candidate.SourceName,
                ResultStatus = "Sucesso",
                ErrorMessage = needsReboot ? "Reinicialização necessária" : string.Empty,
                IsSimulation = false
            });

            return InstallationResult.Ok(needsReboot, exitCode, successMsg);
        }, cancellationToken);
    }

    private async Task<bool> ExecuteRollbackProcessAsync(string backupInfPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = $"/add-driver \"{backupInfPath}\" /install",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                process.WaitForExit(90000);
                return process.ExitCode == 0 || process.ExitCode == 3010;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro fatal ao reverter.");
                return false;
            }
        });
    }
}
