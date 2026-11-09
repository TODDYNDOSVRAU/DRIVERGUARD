using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using DriverGuard.Core.Interfaces;
using DriverGuard.Infrastructure.Notifications;
using DriverGuard.Infrastructure.Persistence;
using DriverGuard.Infrastructure.Pnp;
using DriverGuard.Infrastructure.Security;
using DriverGuard.Infrastructure.SystemServices;
using DriverGuard.Infrastructure.WindowsUpdate;
using DriverGuard.UI.ViewModels;
using DriverGuard.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace DriverGuard.UI;

/// <summary>
/// Ponto de inicialização do DriverGuard com injeção de dependência e logging Serilog.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. Configuração do Serilog em arquivo rotativo
        string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DriverGuard", "Logs");
        Directory.CreateDirectory(logDir);
        string logFilePath = Path.Combine(logDir, "driverguard-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== DriverGuard Iniciado (Fase 4: Logs, Histórico SQLite, CSV e Notificações Toast) ===");

        // 2. Tratamento global de exceções
        SetupExceptionHandling();

        // 3. Validação de privilégios de Administrador
        if (!IsRunningAsAdministrator())
        {
            Log.Warning("DriverGuard não está sendo executado como Administrador.");
            MessageBox.Show(
                "O DriverGuard necessita de privilégios de Administrador para auditar e gerenciar drivers do Windows.\nPor favor, reinicie o aplicativo como Administrador.",
                "Privilégios Insuficientes",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        // 4. Configuração do Host de Injeção de Dependências
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Infraestrutura e PnP
                services.AddSingleton<IDeviceInventoryService, CimDeviceInventoryService>();
                services.AddSingleton<ISystemRestoreService, SystemRestoreService>();
                services.AddSingleton<IDriverBackupService, DriverBackupService>();
                services.AddSingleton<IPendingRebootDetector, PendingRebootDetector>();
                services.AddSingleton<ISignatureVerificationService, WinVerifyTrustService>();
                services.AddSingleton<IFolderDriverParserService, InfParserService>();
                services.AddSingleton<IUpdateCheckService, WuaUpdateSearcherService>();
                services.AddSingleton<IDriverInstallerService, PnpUtilInstallerService>();
                services.AddSingleton<IDriverRollbackService, DriverRollbackService>();

                // Persistência e Notificações (Fase 4)
                services.AddSingleton<IHistoryRepository, SqliteHistoryRepository>();
                services.AddSingleton<ICsvExportService, CsvExportService>();
                services.AddSingleton<INotificationService, WindowsToastNotificationService>();

                // ViewModels
                services.AddSingleton<MainViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        // 5. Exibição da Janela Principal
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("Encerrando DriverGuard...");
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Exceção não tratada no AppDomain.");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error(args.Exception, "Exceção de interface no Dispatcher.");
            MessageBox.Show(
                $"Ocorreu um erro inesperado:\n{args.Exception.Message}",
                "Erro de Execução",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log.Error(args.Exception, "Exceção de tarefa assíncrona não observada.");
            args.SetObserved();
        };
    }
}
