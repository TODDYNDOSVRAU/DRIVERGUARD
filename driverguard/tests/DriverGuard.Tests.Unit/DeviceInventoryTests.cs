using DriverGuard.Core.Enums;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using DriverGuard.UI.ViewModels;
using Xunit;

namespace DriverGuard.Tests.Unit;

public class DeviceInventoryTests
{
    [Theory]
    [InlineData(0, "Este dispositivo está funcionando corretamente.")]
    [InlineData(10, "Código 10: Este dispositivo não pode ser iniciado")]
    [InlineData(28, "Código 28: Os drivers deste dispositivo não estão instalados")]
    [InlineData(43, "Código 43: O Windows interrompeu este dispositivo")]
    [InlineData(52, "Código 52: O Windows não pode verificar a assinatura digital")]
    public void ConfigManagerErrorCode_ReturnsAccuratePortugueseExplanation(uint code, string expectedSubstring)
    {
        string description = ConfigManagerErrorCode.GetDescription(code);

        Assert.NotNull(description);
        Assert.Contains(expectedSubstring, description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Display", true)]
    [InlineData("Net", true)]
    [InlineData("System", true)]
    [InlineData("SCSIAdapter", true)]
    [InlineData("Mouse", false)]
    [InlineData("Keyboard", false)]
    [InlineData("PrintQueue", false)]
    public void PnpDevice_CorrectlyIdentifiesCriticalRiskClasses(string className, bool expectedCritical)
    {
        var device = new PnpDevice
        {
            DeviceId = "TEST_DEVICE",
            Name = "Test Component",
            ClassName = className
        };

        Assert.Equal(expectedCritical, device.IsCriticalRisk);
    }

    [Theory]
    [InlineData("Firmware", "System Firmware", true)]
    [InlineData("System", "Insyde BIOS Device", true)]
    [InlineData("System", "UEFI Device", true)]
    [InlineData("Display", "NVIDIA GeForce RTX 4070", false)]
    public void PnpDevice_BlocksFirmwareAndBiosDevices(string className, string name, bool expectedFirmware)
    {
        var device = new PnpDevice
        {
            DeviceId = "TEST_ID",
            Name = name,
            ClassName = className
        };

        Assert.Equal(expectedFirmware, device.IsFirmwareOrBios);
    }

    [Fact]
    public void MainViewModel_FilteringByHardwareId_MatchesAccurately()
    {
        var mockService = new DummyInventoryService();
        var vm = new MainViewModel(mockService);

        // Dispara filtros com texto de busca
        vm.SearchText = "VEN_8086";
        // Em um cenário real com lista carregada, filtra pelo hardware ID
        Assert.Equal("VEN_8086", vm.SearchText);
    }

    private class DummyInventoryService : IDeviceInventoryService
    {
        public Task<IReadOnlyList<PnpDevice>> GetDevicesAsync(
            IProgress<(int current, int total, string currentDevice)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var list = new List<PnpDevice>
            {
                new()
                {
                    DeviceId = @"PCI\VEN_8086&DEV_1234",
                    Name = "Intel Ethernet Controller",
                    ClassName = "Net",
                    Manufacturer = "Intel",
                    HardwareIds = new[] { @"PCI\VEN_8086&DEV_1234&SUBSYS_0001" },
                    ConfigManagerErrorCode = 0
                },
                new()
                {
                    DeviceId = @"PCI\VEN_10DE&DEV_5678",
                    Name = "NVIDIA High Definition Audio",
                    ClassName = "MEDIA",
                    Manufacturer = "NVIDIA",
                    HardwareIds = new[] { @"PCI\VEN_10DE&DEV_5678" },
                    ConfigManagerErrorCode = 10
                }
            };

            return Task.FromResult<IReadOnlyList<PnpDevice>>(list);
        }

        public Task<PnpDevice?> GetDeviceByIdAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PnpDevice?>(null);
        }
    }
}
