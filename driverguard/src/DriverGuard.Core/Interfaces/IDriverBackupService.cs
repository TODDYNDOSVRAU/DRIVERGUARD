using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para exportação e catalogação dos drivers ativos do DriverStore para a pasta de segurança.
/// </summary>
public interface IDriverBackupService
{
    /// <summary>
    /// Exporta o driver ativo do dispositivo utilizando pnputil /export-driver para pasta isolada.
    /// </summary>
    Task<DriverBackupInfo?> ExportCurrentDriverAsync(PnpDevice device, bool isSimulation = false);

    /// <summary>
    /// Lista todos os backups armazenados localmente para o dispositivo informado.
    /// </summary>
    IReadOnlyList<DriverBackupInfo> GetAvailableBackups(string deviceId);
}
