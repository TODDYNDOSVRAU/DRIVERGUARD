using System.Globalization;
using System.Management;
using DriverGuard.Core.Enums;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Serilog;

namespace DriverGuard.Infrastructure.Pnp;

/// <summary>
/// Serviço de inventário PnP e drivers utilizando WMI/CIM (Win32_PnPEntity e Win32_PnPSignedDriver).
/// </summary>
public class CimDeviceInventoryService : IDeviceInventoryService
{
    private readonly ILogger _logger = Log.ForContext<CimDeviceInventoryService>();

    public async Task<IReadOnlyList<PnpDevice>> GetDevicesAsync(
        IProgress<(int current, int total, string currentDevice)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            _logger.Information("Iniciando auditoria completa do inventário PnP...");
            var devices = new List<PnpDevice>();

            try
            {
                // 1. Coleta metadados de drivers assinados para correlação indexada por DeviceID
                var driverCache = LoadSignedDriversCache(cancellationToken);
                _logger.Information("Metadados de {Count} drivers assinados carregados do DriverStore.", driverCache.Count);

                // 2. Consulta entidades PnP presentes no sistema
                using var entitySearcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT DeviceID, Name, Description, PNPClass, ClassGuid, Manufacturer, HardwareID, CompatibleID, ConfigManagerErrorCode, Present FROM Win32_PnPEntity WHERE Present = True");

                using var entityCollection = entitySearcher.Get();
                int totalCount = entityCollection.Count;
                int currentIndex = 0;

                _logger.Information("Detectados {Total} dispositivos PnP presentes no sistema.", totalCount);

                foreach (ManagementObject entity in entityCollection)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    currentIndex++;

                    string? deviceId = entity["DeviceID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(deviceId))
                        continue;

                    string name = entity["Name"]?.ToString() ?? entity["Description"]?.ToString() ?? "Dispositivo Desconhecido";
                    string pnpClass = entity["PNPClass"]?.ToString() ?? "Sistema";
                    string classGuid = entity["ClassGuid"]?.ToString() ?? string.Empty;
                    string manufacturer = entity["Manufacturer"]?.ToString() ?? "Desconhecido";

                    var hardwareIds = ParseStringArray(entity["HardwareID"]);
                    var compatibleIds = ParseStringArray(entity["CompatibleID"]);

                    uint errorCode = 0;
                    if (entity["ConfigManagerErrorCode"] != null)
                    {
                        uint.TryParse(entity["ConfigManagerErrorCode"]?.ToString(), out errorCode);
                    }

                    // Notifica progresso para a UI
                    progress?.Report((currentIndex, totalCount, name));

                    // Correlaciona com o driver ativo
                    driverCache.TryGetValue(deviceId, out var driverInfo);

                    var device = new PnpDevice
                    {
                        DeviceId = deviceId,
                        Name = name,
                        ClassName = pnpClass,
                        ClassGuid = classGuid,
                        Manufacturer = manufacturer,
                        HardwareIds = hardwareIds,
                        CompatibleIds = compatibleIds,
                        DriverVersion = driverInfo?.Version ?? "N/A",
                        DriverDate = driverInfo?.Date,
                        DriverProvider = driverInfo?.Provider ?? "Desconhecido",
                        InfName = driverInfo?.InfName ?? string.Empty,
                        ConfigManagerErrorCode = errorCode,
                        Status = errorCode != 0 ? DeviceStatus.Error : DeviceStatus.NotVerified
                    };

                    devices.Add(device);
                }

                _logger.Information("Auditoria PnP concluída com sucesso: {Count} dispositivos processados.", devices.Count);
                return devices;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Auditoria PnP cancelada pelo usuário.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro fatal durante a coleta de inventário PnP.");
                throw new InvalidOperationException("Falha ao comunicar com o subsistema CIM/WMI do Windows.", ex);
            }
        }, cancellationToken);
    }

    public async Task<PnpDevice?> GetDeviceByIdAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                string escapedId = deviceId.Replace("\\", "\\\\").Replace("'", "\\'");
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    $"SELECT DeviceID, Name, Description, PNPClass, ClassGuid, Manufacturer, HardwareID, CompatibleID, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE DeviceID = '{escapedId}'");

                foreach (ManagementObject entity in searcher.Get())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string name = entity["Name"]?.ToString() ?? entity["Description"]?.ToString() ?? "Dispositivo Desconhecido";
                    string pnpClass = entity["PNPClass"]?.ToString() ?? "Sistema";
                    string classGuid = entity["ClassGuid"]?.ToString() ?? string.Empty;
                    string manufacturer = entity["Manufacturer"]?.ToString() ?? "Desconhecido";

                    var hardwareIds = ParseStringArray(entity["HardwareID"]);
                    var compatibleIds = ParseStringArray(entity["CompatibleID"]);

                    uint errorCode = 0;
                    if (entity["ConfigManagerErrorCode"] != null)
                    {
                        uint.TryParse(entity["ConfigManagerErrorCode"]?.ToString(), out errorCode);
                    }

                    var driverInfo = GetSingleDriverInfo(deviceId);

                    return new PnpDevice
                    {
                        DeviceId = deviceId,
                        Name = name,
                        ClassName = pnpClass,
                        ClassGuid = classGuid,
                        Manufacturer = manufacturer,
                        HardwareIds = hardwareIds,
                        CompatibleIds = compatibleIds,
                        DriverVersion = driverInfo?.Version ?? "N/A",
                        DriverDate = driverInfo?.Date,
                        DriverProvider = driverInfo?.Provider ?? "Desconhecido",
                        InfName = driverInfo?.InfName ?? string.Empty,
                        ConfigManagerErrorCode = errorCode,
                        Status = errorCode != 0 ? DeviceStatus.Error : DeviceStatus.NotVerified
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Falha ao obter dispositivo individual: {DeviceId}", deviceId);
                return null;
            }
        }, cancellationToken);
    }

    private Dictionary<string, DriverDetails> LoadSignedDriversCache(CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, DriverDetails>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var driverSearcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                "SELECT DeviceID, DriverVersion, DriverDate, DriverProviderName, InfName FROM Win32_PnPSignedDriver");

            foreach (ManagementObject driverObj in driverSearcher.Get())
            {
                cancellationToken.ThrowIfCancellationRequested();

                string? devId = driverObj["DeviceID"]?.ToString();
                if (string.IsNullOrWhiteSpace(devId))
                    continue;

                string version = driverObj["DriverVersion"]?.ToString() ?? "N/A";
                string provider = driverObj["DriverProviderName"]?.ToString() ?? "Desconhecido";
                string infName = driverObj["InfName"]?.ToString() ?? string.Empty;
                DateTime? date = ParseDmtfDate(driverObj["DriverDate"]?.ToString());

                cache[devId] = new DriverDetails(version, date, provider, infName);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Aviso: Não foi possível pré-carregar tabela completa de Win32_PnPSignedDriver. Continuando com fallback.");
        }

        return cache;
    }

    private DriverDetails? GetSingleDriverInfo(string deviceId)
    {
        try
        {
            string escapedId = deviceId.Replace("\\", "\\\\").Replace("'", "\\'");
            using var searcher = new ManagementObjectSearcher(
                "root\\CIMV2",
                $"SELECT DriverVersion, DriverDate, DriverProviderName, InfName FROM Win32_PnPSignedDriver WHERE DeviceID = '{escapedId}'");

            foreach (ManagementObject item in searcher.Get())
            {
                string version = item["DriverVersion"]?.ToString() ?? "N/A";
                string provider = item["DriverProviderName"]?.ToString() ?? "Desconhecido";
                string infName = item["InfName"]?.ToString() ?? string.Empty;
                DateTime? date = ParseDmtfDate(item["DriverDate"]?.ToString());

                return new DriverDetails(version, date, provider, infName);
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Driver assinado não encontrado para o dispositivo {DeviceId}", deviceId);
        }

        return null;
    }

    private static IReadOnlyList<string> ParseStringArray(object? value)
    {
        if (value is string[] array)
            return array;
        if (value is string single && !string.IsNullOrWhiteSpace(single))
            return new[] { single };

        return Array.Empty<string>();
    }

    private static DateTime? ParseDmtfDate(string? dmtfDate)
    {
        if (string.IsNullOrWhiteSpace(dmtfDate) || dmtfDate.Length < 8)
            return null;

        try
        {
            // O formato padrão WMI é YYYYMMDD******.******+***
            string ymd = dmtfDate.Substring(0, 8);
            if (DateTime.TryParseExact(ymd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return parsedDate;
            }
        }
        catch
        {
            // Ignora formatação atípica e retorna nulo
        }

        return null;
    }

    private sealed record DriverDetails(string Version, DateTime? Date, string Provider, string InfName);
}
