using System.Windows;
using DriverGuard.Core.Models;
using Wpf.Ui.Controls;

namespace DriverGuard.UI.Views.Dialogs;

public partial class InstallConfirmDialog : FluentWindow
{
    private readonly UpdateCandidate _candidate;
    public bool UserConfirmed { get; private set; }

    public InstallConfirmDialog(UpdateCandidate candidate)
    {
        InitializeComponent();
        _candidate = candidate;

        var dev = _candidate.TargetDevice;
        var pkg = _candidate.NewDriverPackage;

        TxtDeviceName.Text = dev.Name;
        TxtHardwareId.Text = dev.PrimaryHardwareId;

        TxtCurrentVersion.Text = $"Versão: {dev.DriverVersion}";
        TxtCurrentDate.Text = $"Data: {dev.FormattedDriverDate}";

        TxtNewVersion.Text = $"Versão: {candidate.NewVersion}";
        TxtNewDate.Text = $"Data: {candidate.FormattedNewDate}";

        TxtSource.Text = candidate.SourceName;
        TxtSignature.Text = candidate.SignatureStatusSummary;
        TxtProvider.Text = pkg.Provider;
        TxtSize.Text = candidate.PackageSizeBytes > 0 ? candidate.FormattedSize : "Via WUA";

        // Se for risco crítico, exige confirmação dupla
        if (dev.IsCriticalRisk)
        {
            BdrCriticalRisk.Visibility = Visibility.Visible;
            BtnInstall.IsEnabled = false;
        }
        else
        {
            BdrCriticalRisk.Visibility = Visibility.Collapsed;
            BtnInstall.IsEnabled = true;
        }
    }

    private void ChkConfirmCritical_Changed(object sender, RoutedEventArgs e)
    {
        BtnInstall.IsEnabled = ChkConfirmCritical.IsChecked == true;
    }

    private void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = false;
        DialogResult = false;
        Close();
    }
}
