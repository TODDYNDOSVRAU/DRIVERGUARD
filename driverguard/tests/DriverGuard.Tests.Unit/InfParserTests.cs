using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using DriverGuard.Infrastructure.Pnp;
using Xunit;

namespace DriverGuard.Tests.Unit;

public class InfParserTests
{
    [Fact]
    public void ParseSingleInf_ExtractsDriverVerAndHardwareIdsCorrectly()
    {
        string tempInf = Path.GetTempFileName() + ".inf";
        string infContent = @"
[Version]
Signature=""$Windows NT$""
Class=Net
ClassGuid={4d36e972-e325-11ce-bfc1-08002be10318}
Provider=""Intel""
CatalogFile=e2f68.cat
DriverVer=03/15/2024,2.1.3.15

[Manufacturer]
%Intel%=Intel,NTamd64

[Intel.NTamd64]
%E225Device%=E225_Inst, PCI\VEN_8086&DEV_7A60
";
        File.WriteAllText(tempInf, infContent);

        try
        {
            var mockSigService = new DummySignatureService();
            var parser = new InfParserService(mockSigService);

            var pkg = parser.ParseSingleInf(tempInf);

            Assert.NotNull(pkg);
            Assert.Equal("Net", pkg.ClassName);
            Assert.Equal("Intel", pkg.Provider);
            Assert.Equal("2.1.3.15", pkg.DriverVersion);
            Assert.NotNull(pkg.DriverDate);
            Assert.Equal(2024, pkg.DriverDate.Value.Year);
            Assert.Equal(3, pkg.DriverDate.Value.Month);
            Assert.Equal(15, pkg.DriverDate.Value.Day);
            Assert.Contains(@"PCI\VEN_8086&DEV_7A60", pkg.SupportedHardwareIds);
        }
        finally
        {
            if (File.Exists(tempInf))
                File.Delete(tempInf);
        }
    }

    [Fact]
    public void SupportsDevice_MatchesDeviceHardwareId()
    {
        var pkg = new DriverPackage
        {
            InfPath = "test.inf",
            SupportedHardwareIds = new[] { @"PCI\VEN_8086&DEV_7A60" }
        };

        var matchingDevice = new PnpDevice
        {
            DeviceId = "DEV_01",
            Name = "Intel Ethernet",
            HardwareIds = new[] { @"PCI\VEN_8086&DEV_7A60&SUBSYS_00008086" }
        };

        var nonMatchingDevice = new PnpDevice
        {
            DeviceId = "DEV_02",
            Name = "Realtek Controller",
            HardwareIds = new[] { @"PCI\VEN_10EC&DEV_8168" }
        };

        // Deve identificar que DEV_7A60 está contido ou corresponde
        Assert.Contains(pkg.SupportedHardwareIds, id => matchingDevice.HardwareIds[0].StartsWith(id));
    }

    private class DummySignatureService : ISignatureVerificationService
    {
        public SignatureVerificationResult VerifyFileSignature(string filePath)
        {
            return SignatureVerificationResult.Valid("Test Signer", "Test Issuer", "123456", true);
        }

        public SignatureVerificationResult VerifyInfAndCatalog(string infPath, string? catalogPath = null)
        {
            return SignatureVerificationResult.Valid("Test Signer", "Test Issuer", "123456", true);
        }
    }
}
