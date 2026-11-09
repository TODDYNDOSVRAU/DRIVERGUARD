namespace DriverGuard.Core.Models;

/// <summary>
/// Representa os metadados de um pacote de driver extraído de um arquivo INF e catálogo associado.
/// </summary>
public class DriverPackage
{
    public required string InfPath { get; init; }
    public string CatalogPath { get; init; } = string.Empty;
    public string PackageDirectory => Path.GetDirectoryName(InfPath) ?? string.Empty;

    public string DriverVersion { get; init; } = "0.0.0.0";
    public DateTime? DriverDate { get; init; }
    public string Provider { get; init; } = "Desconhecido";
    public string ClassName { get; init; } = "Unknown";
    public string ClassGuid { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportedHardwareIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SupportedCompatibleIds { get; init; } = Array.Empty<string>();

    public SignatureVerificationResult? SignatureResult { get; set; }

    public bool SupportsDevice(PnpDevice device)
    {
        // 1. Verifica correspondência com Hardware IDs do dispositivo
        foreach (var devHwId in device.HardwareIds)
        {
            if (SupportedHardwareIds.Any(pkgId => string.Equals(pkgId, devHwId, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // 2. Verifica correspondência com Compatible IDs
        foreach (var devCompId in device.CompatibleIds)
        {
            if (SupportedCompatibleIds.Any(pkgId => string.Equals(pkgId, devCompId, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }
}
