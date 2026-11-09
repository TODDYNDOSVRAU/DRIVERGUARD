using System.Diagnostics;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Serilog;

namespace DriverGuard.Infrastructure.Pnp;

/// <summary>
/// Serviço de reversão manual de drivers com persistência de auditoria e notificações Toast.
/// </summary>
public class DriverRollbackService : IDriverRollbackService
{
    private readonly IDriverBackupService _backupService;
    private readonly IDeviceInventoryService _inventoryService;
    private readonly IHistoryRepository _historyRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger _logger = Log.ForContext<DriverRollbackService>();

    public DriverRollbackService(
        IDriverBackupService backupService,
        IDeviceInventoryService inventoryService,
        IHistoryRepository historyRepository,
        INotificationService notificationService)
    {
        _backupService = backupService;
        _inventoryService = inventoryService;
        _historyRepository = historyRepository;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<DriverBackupInfo>> GetRollbackCandidatesAsync(PnpDevice device)
    {
        return await Task.Run(() =>
        {
            var internalBackups = _backupService.GetAvailableBackups(device.DeviceId);
            _logger.Information("Localizados {Count} backups internos para o dispositivo {Name}", internalBackups.Count, device.Name);
            return internalBackups;
        });
    }

    public async Task<InstallationResult> RollbackDriverAsync(
        PnpDevice device,
        DriverBackupInfo backupInfo,
        bool isSimulation = false,
        CancellationToken cancellationToken = default)
    {
        if (isSimulation)
        {
            _logger.Information("[MODO SIMULAÇÃO] Reversão simulada para {Name}.", device.Name);

            await _historyRepository.AddRecordAsync(new OperationHistoryRecord
            {
                DeviceName = device.Name,
                HardwareId = device.PrimaryHardwareId,
                OperationType = "Reversão Manual",
                PreviousVersion = device.DriverVersion,
                NewVersion = backupInfo.DriverVersion,
                Source = "Backup Local",
                ResultStatus = "Simulado",
                ErrorMessage = string.Empty,
                IsSimulation = true
            });

            return InstallationResult.Simulation(device.Name, backupInfo.DriverVersion);
        }

        if (!File.Exists(backupInfo.PrimaryInfPath))
        {
            string err = $"Arquivo INF do backup não foi encontrado em '{backupInfo.PrimaryInfPath}'.";
            _logger.Error(err);

            await _historyRepository.AddRecordAsync(new OperationHistoryRecord
            {
                DeviceName = device.Name,
                HardwareId = device.PrimaryHardwareId,
                OperationType = "Reversão Manual",
                PreviousVersion = device.DriverVersion,
                NewVersion = backupInfo.DriverVersion,
                Source = "Backup Local",
                ResultStatus = "Falha",
                ErrorMessage = err,
                IsSimulation = false
            });

            return new InstallationResult { Success = false, Message = err };
        }

        return await Task.Run(async () =>
        {
            _logger.Information("Iniciando reversão manual de {Dev} para versão {Ver}...", device.Name, backupInfo.DriverVersion);

            var startInfo = new ProcessStartInfo
            {
                FileName = "pnputil.exe",
                Arguments = $"/add-driver \"{backupInfo.PrimaryInfPath}\" /install",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) throw new InvalidOperationException("Falha ao iniciar pnputil.");

            string stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit(90000);
            int exitCode = process.ExitCode;

            bool success = exitCode == 0 || exitCode == 3010;
            bool needsReboot = exitCode == 3010;

            if (success)
            {
                await Task.Delay(2000, cancellationToken);
                var postDevice = await _inventoryService.GetDeviceByIdAsync(device.DeviceId, cancellationToken);
                uint pnpError = postDevice?.ConfigManagerErrorCode ?? 0;

                string msg = needsReboot
                    ? $"Reversão concluída com sucesso para a versão {backupInfo.DriverVersion}. Reinicialização necessária."
                    : $"Reversão concluída com sucesso para a versão {backupInfo.DriverVersion}! O dispositivo está operacional.";

                // Dispara Toast 2 (Reversão Executada)
                _notificationService.ShowRollbackExecutedToast(device.Name, backupInfo.DriverVersion, isAutomatic: false);

                await _historyRepository.AddRecordAsync(new OperationHistoryRecord
                {
                    DeviceName = device.Name,
                    HardwareId = device.PrimaryHardwareId,
                    OperationType = "Reversão Manual",
                    PreviousVersion = device.DriverVersion,
                    NewVersion = backupInfo.DriverVersion,
                    Source = "Backup Local",
                    ResultStatus = "Sucesso",
                    ErrorMessage = needsReboot ? "Reinicialização pendente" : string.Empty,
                    IsSimulation = false
                });

                return InstallationResult.Ok(needsReboot, exitCode, msg);
            }

            string failMsg = $"O pnputil falhou ao aplicar o driver anterior (código {exitCode}). Output: {stdout}";
            _notificationService.ShowInstallationFailedToast(device.Name, failMsg);

            await _historyRepository.AddRecordAsync(new OperationHistoryRecord
            {
                DeviceName = device.Name,
                HardwareId = device.PrimaryHardwareId,
                OperationType = "Reversão Manual",
                PreviousVersion = device.DriverVersion,
                NewVersion = backupInfo.DriverVersion,
                Source = "Backup Local",
                ResultStatus = "Falha",
                ErrorMessage = failMsg,
                IsSimulation = false
            });

            return new InstallationResult
            {
                Success = false,
                ExitCode = exitCode,
                Message = failMsg
            };
        }, cancellationToken);
    }
}
