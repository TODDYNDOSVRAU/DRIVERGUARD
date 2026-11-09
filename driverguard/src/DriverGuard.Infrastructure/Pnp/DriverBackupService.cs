using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Serilog;

namespace DriverGuard.Infrastructure.Pnp;

/// <summary>
/// Gerencia a exportação e recuperação de drivers do DriverStore utilizando pnputil /export-driver.
/// </summary>
public class DriverBackupService : IDriverBackupService
{
    private readonly ILogger _logger = Log.ForContext<DriverBackupService>();
    private readonly string _baseBackupDirectory;

    public DriverBackupService()
    {
        _baseBackupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DriverGuard",
            "Backups");
        Directory.CreateDirectory(_baseBackupDirectory);
    }

    public async Task<DriverBackupInfo?> ExportCurrentDriverAsync(PnpDevice device, bool isSimulation = false)
    {
        string deviceHash = ComputeDeviceHash(device.DeviceId);
        string timestampFolder = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string targetDir = Path.Combine(_baseBackupDirectory, deviceHash, timestampFolder);

        if (isSimulation)
        {
            _logger.Information("[MODO SIMULAÇÃO] Exportação simulada para o dispositivo {Name} em {Dir}", device.Name, targetDir);
            return new DriverBackupInfo
            {
                BackupDirectory = targetDir,
                PrimaryInfPath = Path.Combine(targetDir, device.InfName.Length > 0 ? device.InfName : "simulated.inf"),
                DeviceId = device.DeviceId,
                DeviceName = device.Name,
                DriverVersion = device.DriverVersion,
                DriverDate = device.DriverDate,
                Provider = device.DriverProvider,
                BackupTimestamp = DateTime.UtcNow
            };
        }

        return await Task.Run(async () =>
        {
            try
            {
                // Exporta somente o pacote do próprio dispositivo (oemNN.inf).
                // Nunca usar "*": isso copiaria o DriverStore inteiro e tornaria a escolha do INF aleatória.
                if (string.IsNullOrWhiteSpace(device.InfName) ||
                    !device.InfName.StartsWith("oem", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Error("Backup abortado para {Name}: nome publicado do driver ausente ou inválido ('{Inf}').", device.Name, device.InfName);
                    return null;
                }

                Directory.CreateDirectory(targetDir);

                _logger.Information("Executando pnputil /export-driver {Inf} '{Target}'...", device.InfName, targetDir);

                var result = await PnpUtilRunner.RunAsync($"/export-driver {device.InfName} \"{targetDir}\"", 30000);

                _logger.Information("pnputil /export-driver finalizado com código {Code}. Output: {Out}", result.ExitCode, result.StdOut.Trim());

                if (result.TimedOut || result.ExitCode != 0)
                {
                    _logger.Error("pnputil /export-driver falhou (código {Code}, timeout: {Timeout}). Erro: {Err}",
                        result.ExitCode, result.TimedOut, result.StdErr.Trim());
                    return null;
                }

                // O pacote exportado deve conter exatamente um INF; se houver mais de um, a escolha seria ambígua
                var exportedInfs = Directory.GetFiles(targetDir, "*.inf", SearchOption.AllDirectories);
                if (exportedInfs.Length != 1)
                {
                    _logger.Error("Backup inválido para {Name}: {Count} arquivos .inf exportados (esperado 1).", device.Name, exportedInfs.Length);
                    return null;
                }

                string primaryInf = exportedInfs[0];

                var backupInfo = new DriverBackupInfo
                {
                    BackupDirectory = targetDir,
                    PrimaryInfPath = primaryInf,
                    DeviceId = device.DeviceId,
                    DeviceName = device.Name,
                    DriverVersion = device.DriverVersion,
                    DriverDate = device.DriverDate,
                    Provider = device.DriverProvider,
                    BackupTimestamp = DateTime.UtcNow
                };

                // Salva metadados do backup em JSON
                string metadataPath = Path.Combine(targetDir, "backup_metadata.json");
                File.WriteAllText(metadataPath, JsonSerializer.Serialize(backupInfo, new JsonSerializerOptions { WriteIndented = true }));

                // Gera script de contingência offline para WinRE / Prompt de emergência
                string emergencyCmd = Path.Combine(targetDir, "rollback_emergency.cmd");
                File.WriteAllText(emergencyCmd, $"@echo off{Environment.NewLine}pnputil.exe /add-driver \"{primaryInf}\" /install{Environment.NewLine}pause");

                return backupInfo;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Falha crítica ao exportar driver para reversão.");
                return null;
            }
        });
    }

    public IReadOnlyList<DriverBackupInfo> GetAvailableBackups(string deviceId)
    {
        var backups = new List<DriverBackupInfo>();
        string deviceHash = ComputeDeviceHash(deviceId);
        string deviceDir = Path.Combine(_baseBackupDirectory, deviceHash);

        if (!Directory.Exists(deviceDir))
            return backups;

        foreach (var subDir in Directory.GetDirectories(deviceDir))
        {
            string metadataPath = Path.Combine(subDir, "backup_metadata.json");
            if (File.Exists(metadataPath))
            {
                try
                {
                    string json = File.ReadAllText(metadataPath);
                    var info = JsonSerializer.Deserialize<DriverBackupInfo>(json);
                    if (info != null)
                        backups.Add(info);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Não foi possível carregar metadados de backup em {Dir}", subDir);
                }
            }
        }

        return backups.OrderByDescending(b => b.BackupTimestamp).ToList();
    }

    private static string ComputeDeviceHash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);
    }
}
