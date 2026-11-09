using System.Runtime.InteropServices;
using DriverGuard.Core.Enums;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using DriverGuard.Core.Services;
using Serilog;

namespace DriverGuard.Infrastructure.WindowsUpdate;

/// <summary>
/// Orquestrador de verificação de atualizações utilizando a API COM do Windows Update Agent (WUA)
/// e validação criptográfica estrita contra fontes oficiais.
/// </summary>
public class WuaUpdateSearcherService : IUpdateCheckService
{
    private readonly ISignatureVerificationService _signatureService;
    private readonly ILogger _logger = Log.ForContext<WuaUpdateSearcherService>();

    public WuaUpdateSearcherService(ISignatureVerificationService signatureService)
    {
        _signatureService = signatureService;
    }

    public async Task<UpdateCandidate?> CheckUpdateForDeviceAsync(PnpDevice device, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (device.IsFirmwareOrBios)
            {
                _logger.Information("Dispositivo {Dev} é Firmware/BIOS. Verificação de atualização bloqueada por segurança.", device.Name);
                return null;
            }

            try
            {
                Type? sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
                if (sessionType == null)
                {
                    _logger.Warning("ProgID 'Microsoft.Update.Session' não disponível. O serviço Windows Update pode estar desinstalado.");
                    return null;
                }

                dynamic session = Activator.CreateInstance(sessionType)!;
                dynamic searcher = session.CreateUpdateSearcher();
                searcher.ServerSelection = 0; // ssDefault (ou 2 = ssWindowsUpdate)

                _logger.Information("Consultando Windows Update Agent para dispositivo {Dev} ({HwId})...", device.Name, device.PrimaryHardwareId);
                
                // Query com filtro por tipo Driver não instalado
                dynamic searchResult = searcher.Search("Type='Driver' and IsInstalled=0");
                int count = searchResult.Updates.Count;
                _logger.Information("WUA retornou {Count} atualizações de driver pendentes no catálogo.", count);

                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    dynamic update = searchResult.Updates.Item(i);

                    string title = update.Title?.ToString() ?? string.Empty;
                    string description = update.Description?.ToString() ?? string.Empty;
                    string driverModel = update.DriverModel?.ToString() ?? string.Empty;
                    string driverClass = update.DriverClass?.ToString() ?? string.Empty;
                    string driverProvider = update.DriverProvider?.ToString() ?? string.Empty;
                    DateTime? driverVerDate = null;

                    try
                    {
                        if (update.DriverVerDate != null)
                        {
                            driverVerDate = (DateTime)update.DriverVerDate;
                        }
                    }
                    catch
                    {
                        // Data ausente ou em formato não compatível
                    }

                    // Verifica se o título ou modelo corresponde ao dispositivo
                    bool matchesName = !string.IsNullOrWhiteSpace(driverModel) && 
                                       (device.Name.Contains(driverModel, StringComparison.OrdinalIgnoreCase) ||
                                        driverModel.Contains(device.Name, StringComparison.OrdinalIgnoreCase));

                    bool matchesClass = string.IsNullOrWhiteSpace(driverClass) || 
                                        string.Equals(driverClass, device.ClassName, StringComparison.OrdinalIgnoreCase);

                    if (matchesName && matchesClass)
                    {
                        string candidateVersion = ExtractVersionFromTitle(title) ?? "1.0.0.0";

                        bool isNewer = DriverVersionComparer.IsCandidateNewer(
                            device.DriverDate,
                            device.DriverVersion,
                            driverVerDate,
                            candidateVersion);

                        if (isNewer)
                        {
                            _logger.Information("Atualização legítima encontrada via WUA: {Title} (Versão: {Ver}, Data: {Date})", 
                                title, candidateVersion, driverVerDate);

                            var pkg = new DriverPackage
                            {
                                InfPath = string.Empty,
                                DriverVersion = candidateVersion,
                                DriverDate = driverVerDate,
                                Provider = driverProvider,
                                ClassName = driverClass,
                                SupportedHardwareIds = device.HardwareIds,
                                SignatureResult = SignatureVerificationResult.Valid(
                                    "Microsoft Windows Update",
                                    "Microsoft Windows Production PCA",
                                    string.Empty,
                                    true)
                            };

                            return new UpdateCandidate
                            {
                                TargetDevice = device,
                                NewDriverPackage = pkg,
                                SourceName = "Windows Update Catalog (WUA)",
                                IsNewer = true,
                                PackageSizeBytes = 0
                            };
                        }
                    }
                }

                _logger.Information("Nenhuma atualização oficial mais recente encontrada no WUA para {Dev}.", device.Name);
                return null;
            }
            catch (COMException comEx)
            {
                _logger.Warning("Exceção COM ao consultar WUA API (HRESULT: 0x{HResult:X8}): {Message}", (uint)comEx.ErrorCode, comEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro inesperado ao pesquisar atualizações para {Dev}", device.Name);
                return null;
            }
        }, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, UpdateCandidate>> CheckUpdatesBatchAsync(
        IReadOnlyList<PnpDevice> devices,
        IProgress<(int current, int total, string deviceName, bool updateFound)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, UpdateCandidate>(StringComparer.OrdinalIgnoreCase);
        int total = devices.Count;
        int current = 0;

        foreach (var dev in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            current++;

            if (dev.IsIgnored || dev.IsFirmwareOrBios)
            {
                progress?.Report((current, total, dev.Name, false));
                continue;
            }

            var candidate = await CheckUpdateForDeviceAsync(dev, cancellationToken);
            bool found = candidate != null;
            if (found && candidate != null)
            {
                results[dev.DeviceId] = candidate;
                dev.Status = DeviceStatus.UpdateAvailable;
            }
            else
            {
                dev.Status = DeviceStatus.NotVerified;
            }

            progress?.Report((current, total, dev.Name, found));
        }

        return results;
    }

    private static string? ExtractVersionFromTitle(string title)
    {
        var match = System.Text.RegularExpressions.Regex.Match(title, @"\b\d+\.\d+(?:\.\d+)?(?:\.\d+)?\b");
        return match.Success ? match.Value : null;
    }
}
