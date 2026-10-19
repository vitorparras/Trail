using System.Security.Cryptography;
using System.Xml.Linq;

namespace Trail.Web.Tests;

public sealed class VendoredFilesTests
{
    [Fact]
    public void VendoredFiles_MatchTheHashesPinnedInFrontendProps()
    {
        var repository = RepositoryRoot();
        var pins = XDocument.Load(Path.Combine(repository.FullName, "eng", "frontend.props"))
            .Descendants("VendoredFile")
            .Select(item => (File: (string)item.Attribute("File")!, Sha256: (string)item.Attribute("Sha256")!))
            .ToList();
        Assert.NotEmpty(pins);

        var mismatches = pins
            .Select(pin => (pin.File, Expected: pin.Sha256, Actual: Sha256Of(Path.Combine(repository.FullName, pin.File))))
            .Where(pin => !string.Equals(pin.Expected, pin.Actual, StringComparison.OrdinalIgnoreCase))
            .Select(pin => $"{pin.File}: expected {pin.Expected}, got {pin.Actual}");

        Assert.Empty(mismatches);
    }

    [Theory]
    [InlineData("daisyUI", "DaisyUIVersion")]
    [InlineData("htmx", "HtmxVersion")]
    [InlineData("Chart.js", "ChartJsVersion")]
    [InlineData("Lucide", "LucideVersion")]
    [InlineData("Tailwind CSS", "TailwindVersion")]
    public void ThirdPartyNotices_ListThePinnedVersion(string component, string versionProperty)
    {
        var repository = RepositoryRoot();
        var version = XDocument.Load(Path.Combine(repository.FullName, "eng", "frontend.props"))
            .Descendants(versionProperty)
            .Single()
            .Value;

        var notices = File.ReadAllText(Path.Combine(repository.FullName, "THIRD-PARTY-NOTICES.md"));

        Assert.Contains($"| {component} | {version} |", notices, StringComparison.Ordinal);
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);

        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    internal static DirectoryInfo RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trail.slnx")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("The repository root (Trail.slnx) was not found.");
    }
}
