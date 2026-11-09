namespace DriverGuard.Core.Enums;

/// <summary>
/// Status do dispositivo PnP no inventário do DriverGuard.
/// </summary>
public enum DeviceStatus
{
    /// <summary>
    /// O driver instalado é a versão mais recente verificada.
    /// </summary>
    UpToDate,

    /// <summary>
    /// Existe uma atualização oficial e com assinatura válida disponível.
    /// </summary>
    UpdateAvailable,

    /// <summary>
    /// O dispositivo reporta um código de erro no Gerenciador de Dispositivos (ConfigManagerErrorCode != 0).
    /// </summary>
    Error,

    /// <summary>
    /// Nenhuma fonte oficial confiável encontrada ou verificação ainda não executada.
    /// </summary>
    NotVerified,

    /// <summary>
    /// Dispositivo marcado pelo usuário para não receber alertas de atualização.
    /// </summary>
    Ignored
}
