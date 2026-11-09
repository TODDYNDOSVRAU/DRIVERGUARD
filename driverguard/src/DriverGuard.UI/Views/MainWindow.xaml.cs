using System.Windows;
using DriverGuard.UI.ViewModels;
using Wpf.Ui.Controls;

namespace DriverGuard.UI.Views;

/// <summary>
/// Lógica de interação para MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Ao abrir a janela pela primeira vez, executa a auditoria automática do inventário
        await _viewModel.LoadInventoryAsync();
    }
}
