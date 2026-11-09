namespace DriverGuard.Core.Models;

/// <summary>
/// Proposta formal de atualização correlacionada entre o dispositivo físico instalado e o pacote oficial encontrado.
/// </summary>
public class UpdateCandidate
{
    public required PnpDevice TargetDevice { get; init; }
    public required DriverPackage NewDriverPackage { get; init; }
    public required string SourceName { get; init; }

    public string CurrentVersion => TargetDevice.DriverVersion;
    public string NewVersion => NewDriverPackage.DriverVersion;

    public DateTime? CurrentDate => TargetDevice.DriverDate;
    public DateTime? NewDate => NewDriverPackage.DriverDate;

    public string FormattedCurrentDate => TargetDevice.FormattedDriverDate;
    public string FormattedNewDate => NewDate.HasValue ? NewDate.Value.ToString("dd/MM/yyyy") : "N/A";

    public bool IsNewer { get; init; }
    public bool IsSignatureValid => NewDriverPackage.SignatureResult?.IsValid ?? false;
    public string SignatureStatusSummary => NewDriverPackage.SignatureResult != null 
        ? (NewDriverPackage.SignatureResult.IsWhqlCertified ? "Válida (WHQL)" : "Válida (Catálogo Assinado)")
        : "Não Verificada";

    public long PackageSizeBytes { get; init; }
    public string FormattedSize => $"{PackageSizeBytes / (1024.0 * 1024.0):F2} MB";
}
