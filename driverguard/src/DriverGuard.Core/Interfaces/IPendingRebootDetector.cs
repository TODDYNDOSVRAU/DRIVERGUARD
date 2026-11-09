namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para detecção de reinicialização pendente no sistema operacional Windows.
/// </summary>
public interface IPendingRebootDetector
{
    /// <summary>
    /// Retorna verdadeiro se o Windows tiver uma reinicialização pendente registrada no Registro.
    /// </summary>
    bool IsRebootPending(out string pendingReason);
}
