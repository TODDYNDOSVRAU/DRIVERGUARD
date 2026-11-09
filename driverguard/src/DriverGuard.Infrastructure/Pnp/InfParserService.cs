using System.Globalization;
using System.Text.RegularExpressions;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using DriverGuard.Core.Services;
using Serilog;

namespace DriverGuard.Infrastructure.Pnp;

/// <summary>
/// Parser para análise e extração estruturada de arquivos de instalação de drivers (.INF).
/// </summary>
public class InfParserService : IFolderDriverParserService
{
    private readonly ISignatureVerificationService _signatureService;
    private readonly ILogger _logger = Log.ForContext<InfParserService>();

    public InfParserService(ISignatureVerificationService signatureService)
    {
        _signatureService = signatureService;
    }

    public async Task<IReadOnlyList<DriverPackage>> ParseFolderDriversAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var packages = new List<DriverPackage>();
            if (!Directory.Exists(directoryPath))
            {
                _logger.Warning("Diretório especificado para leitura de drivers não existe: {Dir}", directoryPath);
                return packages;
            }

            var infFiles = Directory.GetFiles(directoryPath, "*.inf", SearchOption.AllDirectories);
            _logger.Information("Localizados {Count} arquivos .INF no diretório {Dir}", infFiles.Length, directoryPath);

            foreach (var infPath in infFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var package = ParseSingleInf(infPath);
                    if (package != null)
                    {
                        // Valida catálogo associado
                        package.SignatureResult = _signatureService.VerifyInfAndCatalog(infPath, package.CatalogPath);
                        packages.Add(package);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Falha ao analisar arquivo INF: {Path}", infPath);
                }
            }

            return packages;
        }, cancellationToken);
    }

    public async Task<UpdateCandidate?> EvaluateFolderForDeviceAsync(PnpDevice device, string directoryPath, CancellationToken cancellationToken = default)
    {
        var packages = await ParseFolderDriversAsync(directoryPath, cancellationToken);
        if (packages.Count == 0)
        {
            _logger.Information("Nenhum pacote INF válido encontrado na pasta: {Dir}", directoryPath);
            return null;
        }

        // Procura pacote que declare compatibilidade com os Hardware IDs do dispositivo
        foreach (var pkg in packages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pkg.SupportsDevice(device))
            {
                _logger.Information("Correspondência de Hardware ID confirmada entre dispositivo {Dev} e pacote {Inf}", device.Name, pkg.InfPath);

                bool isNewer = DriverVersionComparer.IsCandidateNewer(
                    device.DriverDate,
                    device.DriverVersion,
                    pkg.DriverDate,
                    pkg.DriverVersion);

                long packageSize = CalculateDirectorySize(pkg.PackageDirectory);

                return new UpdateCandidate
                {
                    TargetDevice = device,
                    NewDriverPackage = pkg,
                    SourceName = "Pasta Local",
                    IsNewer = isNewer,
                    PackageSizeBytes = packageSize
                };
            }
        }

        _logger.Information("Nenhum driver na pasta corresponde aos Hardware IDs do dispositivo {Name}", device.Name);
        return null;
    }

    public DriverPackage? ParseSingleInf(string infPath)
    {
        var lines = File.ReadAllLines(infPath);
        string currentSection = string.Empty;

        string className = "Unknown";
        string classGuid = string.Empty;
        string provider = "Desconhecido";
        string catalogFile = string.Empty;
        string driverVersion = "0.0.0.0";
        DateTime? driverDate = null;

        var supportedHwIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var supportedCompIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Mapeamento de seções
        var sectionDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            // Ignora comentários e linhas vazias
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                continue;

            // Remove comentários inline
            int commentIdx = line.IndexOf(';');
            if (commentIdx >= 0)
                line = line.Substring(0, commentIdx).Trim();

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = line.Substring(1, line.Length - 2).Trim();
                if (!sectionDict.ContainsKey(currentSection))
                    sectionDict[currentSection] = new List<string>();
                continue;
            }

            if (!string.IsNullOrEmpty(currentSection))
            {
                if (!sectionDict.ContainsKey(currentSection))
                    sectionDict[currentSection] = new List<string>();
                sectionDict[currentSection].Add(line);
            }
        }

        // 1. Processa [Version]
        if (sectionDict.TryGetValue("Version", out var versionLines))
        {
            foreach (var vLine in versionLines)
            {
                var parts = vLine.Split('=', 2);
                if (parts.Length < 2) continue;
                string key = parts[0].Trim();
                string val = parts[1].Trim().Trim('"');

                if (string.Equals(key, "Class", StringComparison.OrdinalIgnoreCase))
                    className = val;
                else if (string.Equals(key, "ClassGuid", StringComparison.OrdinalIgnoreCase))
                    classGuid = val;
                else if (string.Equals(key, "Provider", StringComparison.OrdinalIgnoreCase))
                    provider = val;
                else if (string.Equals(key, "CatalogFile", StringComparison.OrdinalIgnoreCase))
                    catalogFile = val;
                else if (string.Equals(key, "DriverVer", StringComparison.OrdinalIgnoreCase))
                {
                    ParseDriverVer(val, out driverDate, out driverVersion);
                }
            }
        }

        // 2. Extrai Hardware IDs de seções de modelos
        foreach (var kvp in sectionDict)
        {
            // Seções de modelos geralmente não são Version, Strings, SourceDisksNames, etc.
            if (IsModelSection(kvp.Key))
            {
                foreach (var line in kvp.Value)
                {
                    var match = Regex.Match(line, @"(?:PCI|USB|HDAUDIO|ACPI|ROOT)\\[A-Z0-9&_]+", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        supportedHwIds.Add(match.Value);
                    }
                    else
                    {
                        // Procura qualquer identificador que contenha VEN_ ou DEV_ ou VID_ ou PID_
                        var tokens = line.Split(new[] { ',', '=', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var token in tokens)
                        {
                            string t = token.Trim().Trim('"');
                            if (t.Contains("VEN_", StringComparison.OrdinalIgnoreCase) ||
                                t.Contains("VID_", StringComparison.OrdinalIgnoreCase))
                            {
                                supportedHwIds.Add(t);
                            }
                        }
                    }
                }
            }
        }

        string fullCatPath = string.Empty;
        if (!string.IsNullOrWhiteSpace(catalogFile))
        {
            fullCatPath = Path.Combine(Path.GetDirectoryName(infPath) ?? string.Empty, catalogFile);
        }

        return new DriverPackage
        {
            InfPath = infPath,
            CatalogPath = fullCatPath,
            DriverVersion = driverVersion,
            DriverDate = driverDate,
            Provider = provider,
            ClassName = className,
            ClassGuid = classGuid,
            SupportedHardwareIds = supportedHwIds.ToList(),
            SupportedCompatibleIds = supportedCompIds.ToList()
        };
    }

    private static void ParseDriverVer(string val, out DateTime? date, out string version)
    {
        date = null;
        version = "0.0.0.0";

        var segments = val.Split(',');
        if (segments.Length >= 1)
        {
            string dateStr = segments[0].Trim();
            // Padrão de INF é MM/DD/YYYY
            if (DateTime.TryParseExact(dateStr, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                date = parsedDate;
            }
            else if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var altDate))
            {
                date = altDate;
            }
        }

        if (segments.Length >= 2)
        {
            version = segments[1].Trim();
        }
    }

    private static bool IsModelSection(string sectionName)
    {
        string s = sectionName.ToLowerInvariant();
        if (s == "version" || s == "strings" || s == "sourcedisksnames" ||
            s == "sourcedisksfiles" || s == "destinationdirs" || s == "manufacturer")
        {
            return false;
        }

        return true;
    }

    private static long CalculateDirectorySize(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath)) return 0;
            var dir = new DirectoryInfo(directoryPath);
            return dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }
        catch
        {
            return 0;
        }
    }
}
