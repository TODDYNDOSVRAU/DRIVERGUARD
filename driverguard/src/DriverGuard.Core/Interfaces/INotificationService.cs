namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para exibição de notificações nativas (Windows Toasts) nos 4 cenários críticos estipulados pelas regras de segurança.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notificação 1: Disparada quando uma instalação de driver falhar.
    /// </summary>
    void ShowInstallationFailedToast(string deviceName, string error);

    /// <summary>
    /// Notificação 2: Disparada quando uma reversão (automática ou manual) for executada.
    /// </summary>
    void ShowRollbackExecutedToast(string deviceName, string rolledBackVersion, bool isAutomatic);

    /// <summary>
    /// Notificação 3: Disparada quando um pacote for rejeitado por assinatura digital inválida, ausente ou revogada.
    /// </summary>
    void ShowInvalidSignatureToast(string deviceName, string reason);

    /// <summary>
    /// Notificação 4: Disparada quando um dispositivo entrar em estado de erro PnP (ConfigManagerErrorCode != 0).
    /// </summary>
    void ShowDeviceInErrorToast(string deviceName, uint errorCode, string description);
}
