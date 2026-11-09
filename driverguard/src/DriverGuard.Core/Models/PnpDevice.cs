using DriverGuard.Core.Enums;

namespace DriverGuard.Core.Models;

/// <summary>
/// Representa um dispositivo Plug and Play (PnP) auditado pelo inventário CIM/WMI.
/// </summary>
public class PnpDevice
{
    public required string DeviceId { get; init; }
    public required string Name { get; init; }
    public string ClassName { get; init; } = "Unknown";
    public string ClassGuid { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = "Desconhecido";
    public IReadOnlyList<string> HardwareIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CompatibleIds { get; init; } = Array.Empty<string>();

    public string DriverVersion { get; init; } = "N/A";
    public DateTime? DriverDate { get; init; }
    public string DriverProvider { get; init; } = "Desconhecido";
    public string InfName { get; init; } = string.Empty;

    public uint ConfigManagerErrorCode { get; init; }
    public string StatusDescription => DriverGuard.Core.Enums.ConfigManagerErrorCode.GetDescription(ConfigManagerErrorCode);
    public bool HasProblem => ConfigManagerErrorCode != 0;

    public DeviceStatus Status { get; set; } = DeviceStatus.NotVerified;
    public bool IsIgnored { get; set; }
    public bool IsSelected { get; set; }

    /// <summary>
    /// Identifica se o dispositivo pertence a categorias críticas que exigem confirmação estrita (GPU, Rede, Chipset).
    /// </summary>
    public bool IsCriticalRisk
    {
        get
        {
            var cls = ClassName.ToLowerInvariant();
            return cls.Contains("display") || cls.Contains("video") || cls.Contains("net") ||
                   cls.Contains("system") || cls.Contains("scsi") || cls.Contains("hdc");
        }
    }

    /// <summary>
    /// Identifica se o dispositivo é firmware/BIOS, o qual o DriverGuard nunca deve tentar atualizar.
    /// </summary>
    public bool IsFirmwareOrBios
    {
        get
        {
            var cls = ClassName.ToLowerInvariant();
            var name = Name.ToLowerInvariant();
            return cls.Contains("firmware") || name.Contains("bios") || name.Contains("firmware") || name.Contains("uefi");
        }
    }

    public string PrimaryHardwareId => HardwareIds.Count > 0 ? HardwareIds[0] : (CompatibleIds.Count > 0 ? CompatibleIds[0] : DeviceId);

    public string FormattedDriverDate => DriverDate.HasValue ? DriverDate.Value.ToString("dd/MM/yyyy") : "N/A";
}
