using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o serviço de reversão de drivers (manual ou automática pós-falha).
/// </summary>
public interface IDriverRollbackService
{
    /// <summary>
    /// Lista todas as versões anteriores disponíveis para o dispositivo informado (DriverStore + Backups).
    /// </summary>
    Task<IReadOnlyList<DriverBackupInfo>> GetRollbackCandidatesAsync(PnpDevice device);

    /// <summary>
    /// Executa a reversão para uma versão específica a partir de um pacote de backup ou oem*.inf do DriverStore.
    /// </summary>
    Task<InstallationResult> RollbackDriverAsync(
        PnpDevice device,
        DriverBackupInfo backupInfo,
        bool isSimulation = false,
        CancellationToken cancellationToken = default);
}
