using DriverGuard.Core.Interfaces;
using Microsoft.Toolkit.Uwp.Notifications;
using Serilog;

namespace DriverGuard.Infrastructure.Notifications;

/// <summary>
/// Exibe notificações Toast nativas do Windows 10 e 11 utilizando a biblioteca oficial Microsoft.Toolkit.Uwp.Notifications.
/// </summary>
public class WindowsToastNotificationService : INotificationService
{
    private readonly ILogger _logger = Log.ForContext<WindowsToastNotificationService>();

    public void ShowInstallationFailedToast(string deviceName, string error)
    {
        try
        {
            new ToastContentBuilder()
                .AddText("DriverGuard — Falha na Instalação")
                .AddText($"Dispositivo: {deviceName}")
                .AddText($"Erro: {error}")
                .Show();

            _logger.Information("Toast de falha de instalação exibido para {Dev}", deviceName);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Não foi possível disparar notificação Toast de falha.");
        }
    }

    public void ShowRollbackExecutedToast(string deviceName, string rolledBackVersion, bool isAutomatic)
    {
        try
        {
            string title = isAutomatic 
                ? "DriverGuard — Reversão Automática Executada" 
                : "DriverGuard — Reversão Manual Concluída";

            new ToastContentBuilder()
                .AddText(title)
                .AddText($"Dispositivo: {deviceName}")
                .AddText($"O dispositivo foi restaurado para a versão anterior ({rolledBackVersion}).")
                .Show();

            _logger.Information("Toast de reversão ({Type}) exibido para {Dev}", isAutomatic ? "Automática" : "Manual", deviceName);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Não foi possível disparar notificação Toast de reversão.");
        }
    }

    public void ShowInvalidSignatureToast(string deviceName, string reason)
    {
        try
        {
            new ToastContentBuilder()
                .AddText("DriverGuard — Assinatura Inválida / Rejeitada")
                .AddText($"Dispositivo: {deviceName}")
                .AddText($"O pacote foi bloqueado pelas regras de segurança: {reason}")
                .Show();

            _logger.Information("Toast de assinatura inválida exibido para {Dev}", deviceName);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Não foi possível disparar notificação Toast de assinatura inválida.");
        }
    }

    public void ShowDeviceInErrorToast(string deviceName, uint errorCode, string description)
    {
        try
        {
            new ToastContentBuilder()
                .AddText($"DriverGuard — Erro PnP Detectado (Código {errorCode})")
                .AddText($"Dispositivo: {deviceName}")
                .AddText(description)
                .Show();

            _logger.Information("Toast de erro PnP (Código {Code}) exibido para {Dev}", errorCode, deviceName);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Não foi possível disparar notificação Toast de erro de dispositivo.");
        }
    }
}
