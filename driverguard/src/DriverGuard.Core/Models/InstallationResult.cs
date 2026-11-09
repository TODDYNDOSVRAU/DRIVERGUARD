namespace DriverGuard.Core.Models;

/// <summary>
/// Resultado detalhado da operação de instalação executada via pnputil com auditoria de reversão.
/// </summary>
public class InstallationResult
{
    public required bool Success { get; init; }
    public bool NeedsReboot { get; init; }
    public int ExitCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool RolledBackAutomatically { get; init; }
    public bool RollbackSuccess { get; init; }
    public uint PostInstallErrorCode { get; init; }

    public static InstallationResult Ok(bool rebootRequired, int exitCode, string message) => new()
    {
        Success = true,
        NeedsReboot = rebootRequired,
        ExitCode = exitCode,
        Message = message,
        RolledBackAutomatically = false,
        RollbackSuccess = false
    };

    public static InstallationResult FailedAndRolledBack(int exitCode, string error, uint pnpError, bool rollbackOk) => new()
    {
        Success = false,
        NeedsReboot = false,
        ExitCode = exitCode,
        Message = error,
        RolledBackAutomatically = true,
        RollbackSuccess = rollbackOk,
        PostInstallErrorCode = pnpError
    };

    public static InstallationResult Simulation(string deviceName, string version) => new()
    {
        Success = true,
        NeedsReboot = false,
        ExitCode = 0,
        Message = $"[MODO SIMULAÇÃO] Instalação do driver {version} para {deviceName} simulada com êxito. Nenhum arquivo de sistema foi alterado."
    };
}
