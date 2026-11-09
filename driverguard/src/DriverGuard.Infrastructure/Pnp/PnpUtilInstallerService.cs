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

        // 6. Backup do driver atual (somente o pacote do dispositivo)
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
        PnpUtilRunner.PnpUtilResult installResult;
        try
        {
            installResult = await PnpUtilRunner.RunAsync($"/add-driver \"{pkg.InfPath}\" /install", 90000);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao executar pnputil para instalação.");
            installResult = new PnpUtilRunner.PnpUtilResult(-1, string.Empty, ex.Message, false);
        }

        int exitCode = installResult.ExitCode;
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
                ? installResult.TimedOut
                    ? "Falha no processo pnputil (tempo limite excedido)"
                    : $"Falha no processo pnputil (código {exitCode})"
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
    }

    private static async Task<bool> ExecuteRollbackProcessAsync(string backupInfPath)
    {
        try
        {
            var result = await PnpUtilRunner.RunAsync($"/add-driver \"{backupInfPath}\" /install", 90000);
            return result.ExitCode == 0 || result.ExitCode == 3010;
        }
        catch (Exception ex)
        {
            Log.ForContext<PnpUtilInstallerService>().Error(ex, "Erro fatal ao reverter.");
            return false;
        }
    }
}
