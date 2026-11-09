using DriverGuard.Core.Services;
using Xunit;

namespace DriverGuard.Tests.Unit;

public class DriverVersionComparerTests
{
    [Fact]
    public void IsCandidateNewer_NewerDateTakesPrecedenceOverVersion()
    {
        // No Windows PnP, a data do driver mais recente prevalece sobre uma versão numérica antiga
        var currentDate = new DateTime(2023, 1, 1);
        string currentVersion = "30.0.10.9999";

        var candidateDate = new DateTime(2024, 3, 15);
        string candidateVersion = "25.0.0.1"; // Versão com número menor, mas data mais nova

        bool isNewer = DriverVersionComparer.IsCandidateNewer(currentDate, currentVersion, candidateDate, candidateVersion);
        Assert.True(isNewer);
    }

    [Fact]
    public void IsCandidateNewer_OlderDateIsRejected()
    {
        var currentDate = new DateTime(2024, 1, 1);
        string currentVersion = "10.0.1.0";

        var candidateDate = new DateTime(2023, 12, 31);
        string candidateVersion = "10.0.2.0"; // Versão maior, mas data anterior

        bool isNewer = DriverVersionComparer.IsCandidateNewer(currentDate, currentVersion, candidateDate, candidateVersion);
        Assert.False(isNewer);
    }

    [Fact]
    public void IsCandidateNewer_EqualDates_HigherVersionWins()
    {
        var sameDate = new DateTime(2024, 2, 1);
        string currentVersion = "31.0.15.5176";
        string candidateVersion = "31.0.15.5186";

        bool isNewer = DriverVersionComparer.IsCandidateNewer(sameDate, currentVersion, sameDate, candidateVersion);
        Assert.True(isNewer);
    }

    [Fact]
    public void IsCandidateNewer_IdenticalDateAndVersion_ReturnsFalse()
    {
        var sameDate = new DateTime(2024, 2, 1);
        string version = "31.0.15.5186";

        bool isNewer = DriverVersionComparer.IsCandidateNewer(sameDate, version, sameDate, version);
        Assert.False(isNewer);
    }

    [Theory]
    [InlineData("10.0.19041.1", "10.0.18362.1", 1)]
    [InlineData("10.0.22621.1", "10.0.22621.1", 0)]
    [InlineData("1.2.3.4", "1.2.3.5", -1)]
    [InlineData("31.0.15.5186", "31.0.15.5176", 1)]
    public void CompareVersionStrings_ComparesAccurately(string v1, string v2, int expectedSign)
    {
        int result = DriverVersionComparer.CompareVersionStrings(v1, v2);
        Assert.Equal(expectedSign, Math.Sign(result));
    }
}
