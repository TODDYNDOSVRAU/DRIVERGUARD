namespace DriverGuard.Core.Models;

/// <summary>
/// Resultado da solicitação de criação de ponto de restauração do sistema (System Restore).
/// </summary>
public class RestorePointResult
{
    public required bool Success { get; init; }
    public required bool WasThrottledOrDisabled { get; init; }
    public uint ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;

    public static RestorePointResult Created(string name) => new()
    {
        Success = true,
        WasThrottledOrDisabled = false,
        Message = $"Ponto de restauração '{name}' criado com sucesso."
    };

    public static RestorePointResult ThrottledOrDisabled(string reason) => new()
    {
        Success = false,
        WasThrottledOrDisabled = true,
        Message = reason
    };

    public static RestorePointResult Failed(uint errorCode, string error) => new()
    {
        Success = false,
        WasThrottledOrDisabled = false,
        ErrorCode = errorCode,
        Message = $"Falha ao invocar srclient.dll (Erro 0x{errorCode:X8}): {error}"
    };

    public static RestorePointResult Simulated() => new()
    {
        Success = true,
        WasThrottledOrDisabled = false,
        Message = "[MODO SIMULAÇÃO] Ponto de restauração simulado com sucesso."
    };
}
