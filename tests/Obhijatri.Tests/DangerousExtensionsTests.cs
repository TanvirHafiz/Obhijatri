using Obhijatri.Safety.Downloads;

namespace Obhijatri.Tests;

public class DangerousExtensionsTests
{
    [Theory]
    [InlineData("invoice.pdf.exe")]
    [InlineData("photo.jpg.scr")]
    [InlineData("resume.docx.bat")]
    [InlineData("archive.zip.vbs")]
    public void DoubleExtensionTrick_IsDetected(string fileName) =>
        Assert.True(DangerousExtensions.IsDoubleExtensionTrick(fileName));

    [Theory]
    [InlineData("invoice.pdf")]
    [InlineData("installer.exe")]
    [InlineData("archive.tar.gz")]
    [InlineData("my.report.2026.pdf")]
    public void OrdinaryNames_AreNotFlagged(string fileName) =>
        Assert.False(DangerousExtensions.IsDoubleExtensionTrick(fileName));

    [Theory]
    [InlineData("setup.exe", true)]
    [InlineData("photo.jpg", false)]
    [InlineData("script.ps1", true)]
    public void IsExecutable_MatchesTheFinalExtension(string fileName, bool expected) =>
        Assert.Equal(expected, DangerousExtensions.IsExecutable(fileName));
}
