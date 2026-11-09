using System.Diagnostics;
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

        return await Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(targetDir);

                string infTarget = !string.IsNullOrWhiteSpace(device.InfName) && device.InfName.StartsWith("oem", StringComparison.OrdinalIgnoreCase)
                    ? device.InfName
                    : "*";

                _logger.Information("Executando pnputil /export-driver {Inf} '{Target}'...", infTarget, targetDir);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = $"/export-driver {infTarget} \"{targetDir}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    throw new InvalidOperationException("Falha ao iniciar o processo pnputil.exe.");

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(30000);

                _logger.Information("pnputil /export-driver finalizado com código {Code}. Output: {Out}", process.ExitCode, stdout.Trim());

                // Procura arquivo .inf dentro da pasta de destino exportada
                var exportedInfs = Directory.GetFiles(targetDir, "*.inf", SearchOption.AllDirectories);
                string primaryInf = exportedInfs.Length > 0 ? exportedInfs[0] : Path.Combine(targetDir, device.InfName);

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
