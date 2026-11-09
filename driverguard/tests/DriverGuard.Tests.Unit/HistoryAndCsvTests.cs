using DriverGuard.Core.Models;
using DriverGuard.Infrastructure.Notifications;
using DriverGuard.Infrastructure.Persistence;
using Xunit;

namespace DriverGuard.Tests.Unit;

public class HistoryAndCsvTests
{
    [Fact]
    public async Task CsvExportService_GeneratesValidCsvWithUtf8BomAndSemicolons()
    {
        var service = new CsvExportService();
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}.csv");

        var records = new List<OperationHistoryRecord>
        {
            new()
            {
                DeviceName = "Intel Ethernet I225-V",
                HardwareId = @"PCI\VEN_8086&DEV_7A60",
                OperationType = "Instalação",
                PreviousVersion = "1.0.0.0",
                NewVersion = "2.1.3.15",
                Source = "Microsoft Update Catalog (WUA)",
                ResultStatus = "Sucesso",
                ErrorMessage = string.Empty,
                IsSimulation = false
            },
            new()
            {
                DeviceName = "NVIDIA GeForce RTX 4070",
                HardwareId = @"PCI\VEN_10DE&DEV_2782",
                OperationType = "Instalação (Revertida Auto)",
                PreviousVersion = "31.0.15.5176",
                NewVersion = "32.0.0.0",
                Source = "Pasta Local",
                ResultStatus = "Falha / Revertido",
                ErrorMessage = "Erro PnP 43 detectado após instalação",
                IsSimulation = false
            }
        };

        try
        {
            await service.ExportToCsvAsync(records, tempFile);

            Assert.True(File.Exists(tempFile));

            // Verifica presença de UTF-8 BOM (0xEF, 0xBB, 0xBF)
            byte[] bytes = File.ReadAllBytes(tempFile);
            Assert.True(bytes.Length >= 3);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            // Verifica delimitador ponto-e-vírgula e colunas
            string content = File.ReadAllText(tempFile);
            Assert.Contains("Dispositivo;", content);
            Assert.Contains("HardwareID;", content);
            Assert.Contains("VersaoNova;", content);
            Assert.Contains("Intel Ethernet I225-V;", content);
            Assert.Contains("NVIDIA GeForce RTX 4070;", content);
            Assert.Contains("Erro PnP 43 detectado", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ToastNotificationService_TriggersAllFourCasesWithoutExceptions()
    {
        var toastService = new WindowsToastNotificationService();

        // 1. Instalação Falhou
        var ex1 = Record.Exception(() => toastService.ShowInstallationFailedToast("Test Device", "Processo pnputil retornou código de erro"));
        Assert.Null(ex1);

        // 2. Reversão Executada
        var ex2 = Record.Exception(() => toastService.ShowRollbackExecutedToast("Test Device", "1.0.0.0", isAutomatic: true));
        Assert.Null(ex2);

        // 3. Assinatura Inválida
        var ex3 = Record.Exception(() => toastService.ShowInvalidSignatureToast("Test Device", "Arquivo de catálogo não assinado digitalmente"));
        Assert.Null(ex3);

        // 4. Dispositivo em Erro PnP
        var ex4 = Record.Exception(() => toastService.ShowDeviceInErrorToast("Test Device", 43, "O Windows interrompeu este dispositivo porque encontrou problemas."));
        Assert.Null(ex4);
    }
}
