namespace DriverGuard.Core.Models;

/// <summary>
/// Representa uma entrada imutável no histórico e log de auditoria persistido em SQLite.
/// </summary>
public class OperationHistoryRecord
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string DeviceName { get; set; } = string.Empty;
    public string HardwareId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string PreviousVersion { get; set; } = "N/A";
    public string NewVersion { get; set; } = "N/A";
    public string Source { get; set; } = "Desconhecida";
    public string ResultStatus { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsSimulation { get; set; }

    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
}
