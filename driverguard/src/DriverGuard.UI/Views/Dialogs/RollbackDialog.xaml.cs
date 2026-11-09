using System.Windows;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Wpf.Ui.Controls;

namespace DriverGuard.UI.Views.Dialogs;

public partial class RollbackDialog : FluentWindow
{
    private readonly PnpDevice _device;
    private readonly IDriverRollbackService _rollbackService;

    public DriverBackupInfo? SelectedBackup { get; private set; }
    public bool UserConfirmed { get; private set; }

    public RollbackDialog(PnpDevice device, IDriverRollbackService rollbackService)
    {
        InitializeComponent();
        _device = device;
        _rollbackService = rollbackService;

        TxtHeaderInfo.Text = $"Versões anteriores catalogadas para o dispositivo: {_device.Name}";
        Loaded += RollbackDialog_Loaded;
    }

    private async void RollbackDialog_Loaded(object sender, RoutedEventArgs e)
    {
        var backups = await _rollbackService.GetRollbackCandidatesAsync(_device);
        GridBackups.ItemsSource = backups;

        if (backups.Count == 0)
        {
            TxtEmptyNotice.Visibility = Visibility.Visible;
            BtnRollback.IsEnabled = false;
        }
        else
        {
            TxtEmptyNotice.Visibility = Visibility.Collapsed;
            GridBackups.SelectedIndex = 0;
            BtnRollback.IsEnabled = true;
        }
    }

    private void BtnRollback_Click(object sender, RoutedEventArgs e)
    {
        if (GridBackups.SelectedItem is DriverBackupInfo backup)
        {
            var confirm = System.Windows.MessageBox.Show(
                $"Tem certeza de que deseja reverter o dispositivo '{_device.Name}' para a versão {backup.DriverVersion} (Data: {backup.FormattedDriverDate})?",
                "Confirmar Reversão de Driver",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                SelectedBackup = backup;
                UserConfirmed = true;
                DialogResult = true;
                Close();
            }
        }
        else
        {
            System.Windows.MessageBox.Show("Por favor, selecione um ponto de versão na lista.", "Seleção Necessária", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = false;
        DialogResult = false;
        Close();
    }
}
