using System.Windows;
using System.Windows.Media;
using DriverGuard.Core.Enums;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace DriverGuard.UI.Views.Dialogs;

public partial class FolderDriverDialog : FluentWindow
{
    private readonly PnpDevice _targetDevice;
    private readonly IFolderDriverParserService _folderParser;

    public FolderDriverDialog(PnpDevice targetDevice, IFolderDriverParserService folderParser)
    {
        InitializeComponent();
        _targetDevice = targetDevice;
        _folderParser = folderParser;

        TxtTargetDevice.Text = _targetDevice.Name;
        TxtTargetHwId.Text = _targetDevice.PrimaryHardwareId;
        TxtCurrentVersion.Text = $"Versão: {_targetDevice.DriverVersion}";
        TxtCurrentDate.Text = $"Data: {_targetDevice.FormattedDriverDate}";
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecione a pasta contendo o driver (.inf e .cat)"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtFolderPath.Text = dialog.FolderName;
        }
    }

    private async void BtnInspect_Click(object sender, RoutedEventArgs e)
    {
        string path = TxtFolderPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            System.Windows.MessageBox.Show("Por favor, selecione um diretório válido existente.", "Diretório Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnInspect.IsEnabled = false;
        TxtValidationSummary.Text = "Examinando arquivos INF e validando assinaturas com WinVerifyTrust...";

        try
        {
            var candidate = await _folderParser.EvaluateFolderForDeviceAsync(_targetDevice, path);

            if (candidate == null)
            {
                // Nenhum driver compatível com o Hardware ID
                BdrSignatureStatus.Background = new SolidColorBrush(Color.FromArgb(40, 255, 59, 48));
                BdrSignatureStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                IcoSignature.Symbol = SymbolIcon.Dismiss24;
                IcoSignature.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                TxtSignatureTitle.Text = "Incompatível com o Dispositivo";
                TxtSignatureTitle.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                TxtSignatureSigner.Text = "Nenhum arquivo .INF na pasta contém Hardware IDs correspondentes a este dispositivo.";
                TxtSignatureError.Visibility = Visibility.Collapsed;
                TxtValidationSummary.Text = "Pacote rejeitado: incompatível com o hardware.";
                return;
            }

            var pkg = candidate.NewDriverPackage;
            TxtInfPath.Text = pkg.InfPath;
            TxtNewVersion.Text = $"Versão: {pkg.DriverVersion}";
            TxtNewDate.Text = $"Data: {(pkg.DriverDate.HasValue ? pkg.DriverDate.Value.ToString("dd/MM/yyyy") : "N/A")}";
            TxtMatchedHwIds.Text = string.Join(Environment.NewLine, pkg.SupportedHardwareIds);

            var sig = pkg.SignatureResult;
            if (sig != null && sig.IsValid)
            {
                BdrSignatureStatus.Background = new SolidColorBrush(Color.FromArgb(30, 16, 124, 65));
                BdrSignatureStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 124, 65));
                IcoSignature.Symbol = SymbolIcon.ShieldCheckmark24;
                IcoSignature.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
                TxtSignatureTitle.Text = sig.IsWhqlCertified ? "Assinatura Válida (WHQL Homologado)" : "Assinatura Válida (Catálogo Oficial)";
                TxtSignatureTitle.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65));
                TxtSignatureSigner.Text = $"Signatário: {sig.SignerSubject}\nEmissor: {sig.SignerIssuer}";
                TxtSignatureError.Visibility = Visibility.Collapsed;

                string versionStatus = candidate.IsNewer ? "Driver mais recente que o instalado." : "Driver igual ou mais antigo que a versão atual.";
                TxtValidationSummary.Text = $"Pacote Aprovado na Verificação Criptográfica. {versionStatus}";
            }
            else
            {
                BdrSignatureStatus.Background = new SolidColorBrush(Color.FromArgb(40, 255, 59, 48));
                BdrSignatureStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                IcoSignature.Symbol = SymbolIcon.ShieldDismiss24;
                IcoSignature.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                TxtSignatureTitle.Text = "Assinatura Digital Inválida ou Ausente";
                TxtSignatureTitle.Foreground = new SolidColorBrush(Color.FromRgb(255, 59, 48));
                TxtSignatureSigner.Text = "O pacote NÃO possui catálogo assinado ou a assinatura foi recusada pelo WinVerifyTrust.";
                TxtSignatureError.Text = sig?.ErrorMessage ?? "Falha na cadeia de certificados.";
                TxtSignatureError.Visibility = Visibility.Visible;
                TxtValidationSummary.Text = "Pacote REJEITADO pelas diretrizes de segurança: não assinado.";
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro durante a inspeção: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnInspect.IsEnabled = true;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
