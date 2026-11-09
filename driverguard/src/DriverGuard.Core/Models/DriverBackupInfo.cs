namespace DriverGuard.Core.Models;

/// <summary>
/// Metadados de um driver exportado previamente do DriverStore para a pasta interna de backups.
/// </summary>
public class DriverBackupInfo
{
    public required string BackupDirectory { get; init; }
    public required string PrimaryInfPath { get; init; }
    public required string DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public string DriverVersion { get; init; } = "N/A";
    public DateTime? DriverDate { get; init; }
    public string Provider { get; init; } = "Desconhecido";
    public DateTime BackupTimestamp { get; init; } = DateTime.UtcNow;
    public string FormattedDriverDate => DriverDate.HasValue ? DriverDate.Value.ToString("dd/MM/yyyy") : "N/A";
    public string FormattedBackupDate => BackupTimestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
}
