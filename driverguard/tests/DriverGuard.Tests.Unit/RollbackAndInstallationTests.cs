using DriverGuard.Core.Enums;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using DriverGuard.Infrastructure.Pnp;
using Xunit;

namespace DriverGuard.Tests.Unit;

public class RollbackAndInstallationTests
{
    [Fact]
    public async Task InstallDriverAsync_BlocksFirmwareAndBiosDevices()
    {
        var installer = CreateInstaller(isRebootPending: false);

        var firmwareDevice = new PnpDevice
        {
            DeviceId = "UEFI_TEST_01",
            Name = "System Firmware",
            ClassName = "Firmware"
        };

        var candidate = new UpdateCandidate
        {
            TargetDevice = firmwareDevice,
            NewDriverPackage = new DriverPackage { InfPath = "test.inf", SupportedHardwareIds = new[] { "UEFI_TEST_01" } },
            SourceName = "Test Source",
            IsNewer = true
        };

        var result = await installer.InstallDriverAsync(candidate, isSimulation: false);

        Assert.False(result.Success);
        Assert.Contains("bloqueada", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("firmware", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallDriverAsync_BlocksUnsignedDrivers()
    {
        var installer = CreateInstaller(isRebootPending: false);

        var device = new PnpDevice
        {
            DeviceId = "NET_TEST_01",
            Name = "Test Network Card",
            ClassName = "Net"
        };

        var candidate = new UpdateCandidate
        {
            TargetDevice = device,
            NewDriverPackage = new DriverPackage
            {
                InfPath = "test.inf",
                SignatureResult = SignatureVerificationResult.Invalid(SignatureStatus.Unsigned, "Sem assinatura digital")
            },
            SourceName = "Test Source",
            IsNewer = true
        };

        var result = await installer.InstallDriverAsync(candidate, isSimulation: false);

        Assert.False(result.Success);
        Assert.Contains("assinatura digital", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallDriverAsync_BlocksWhenRebootIsPending()
    {
        var installer = CreateInstaller(isRebootPending: true);

        var device = new PnpDevice
        {
            DeviceId = "DISPLAY_01",
            Name = "Display Adapter",
            ClassName = "Display"
        };

        var candidate = new UpdateCandidate
        {
            TargetDevice = device,
            NewDriverPackage = new DriverPackage
            {
                InfPath = "test.inf",
                SignatureResult = SignatureVerificationResult.Valid("Signer", "Issuer", "123", true)
            },
            SourceName = "WUA",
            IsNewer = true
        };

        var result = await installer.InstallDriverAsync(candidate, isSimulation: false);

        Assert.False(result.Success);
        Assert.Contains("reinicialização pendente", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallDriverAsync_SimulationMode_SucceedsWithoutTouchingSystem()
    {
        var installer = CreateInstaller(isRebootPending: false);

        var device = new PnpDevice
        {
            DeviceId = "DEV_01",
            Name = "Audio Controller",
            ClassName = "MEDIA"
        };

        var candidate = new UpdateCandidate
        {
            TargetDevice = device,
            NewDriverPackage = new DriverPackage
            {
                InfPath = "test.inf",
                DriverVersion = "6.0.9500.1",
                SignatureResult = SignatureVerificationResult.Valid("Signer", "Issuer", "123", true)
            },
            SourceName = "WUA",
            IsNewer = true
        };

        var result = await installer.InstallDriverAsync(candidate, isSimulation: true);

        Assert.True(result.Success);
        Assert.Contains("MODO SIMULAÇÃO", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PnpUtilInstallerService CreateInstaller(bool isRebootPending)
    {
        var mockRestore = new DummyRestoreService();
        var mockBackup = new DummyBackupService();
        var mockReboot = new DummyRebootDetector(isRebootPending);
        var mockInventory = new DummyInventoryService();

        return new PnpUtilInstallerService(mockRestore, mockBackup, mockReboot, mockInventory);
    }

    private class DummyRestoreService : ISystemRestoreService
    {
        public bool IsSystemRestoreEnabled() => true;
        public Task<RestorePointResult> CreateRestorePointAsync(string description, bool isSimulation = false)
            => Task.FromResult(RestorePointResult.Created(description));
    }

    private class DummyBackupService : IDriverBackupService
    {
        public Task<DriverBackupInfo?> ExportCurrentDriverAsync(PnpDevice device, bool isSimulation = false)
            => Task.FromResult<DriverBackupInfo?>(new DriverBackupInfo
            {
                BackupDirectory = "C:\\Backups\\Test",
                PrimaryInfPath = "C:\\Backups\\Test\\driver.inf",
                DeviceId = device.DeviceId,
                DeviceName = device.Name
            });

        public IReadOnlyList<DriverBackupInfo> GetAvailableBackups(string deviceId) => Array.Empty<DriverBackupInfo>();
    }

    private class DummyRebootDetector : IPendingRebootDetector
    {
        private readonly bool _pending;
        public DummyRebootDetector(bool pending) => _pending = pending;

        public bool IsRebootPending(out string pendingReason)
        {
            pendingReason = _pending ? "Atualização do Windows pendente de reinicialização." : string.Empty;
            return _pending;
        }
    }

    private class DummyInventoryService : IDeviceInventoryService
    {
        public Task<IReadOnlyList<PnpDevice>> GetDevicesAsync(IProgress<(int current, int total, string currentDevice)>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PnpDevice>>(Array.Empty<PnpDevice>());

        public Task<PnpDevice?> GetDeviceByIdAsync(string deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult<PnpDevice?>(new PnpDevice { DeviceId = deviceId, Name = "Mock", ConfigManagerErrorCode = 0 });
    }
}
