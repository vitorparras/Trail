using System.Text.RegularExpressions;

namespace Trail.E2E.Tests;

public sealed partial class ComposeOverrideTests
{
    private static readonly string[] Header = ["services:", "  app:", "    environment:"];

    [Fact]
    public void ComposeOverride_DeclaresOnlyTheEnvironmentOfTheTestcontainersPath()
    {
        var path = Path.Combine(RepositoryPaths.Root, DockerAppFixture.ComposeOverridePath);
        var lines = File.ReadAllLines(path).Where(line => line.Trim().Length > 0).ToArray();

        Assert.Equal(Header, lines.Take(Header.Length));

        var declared = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines.Skip(Header.Length))
        {
            var match = EnvironmentEntryPattern().Match(line);
            Assert.True(match.Success, $"Unrecognized line under 'environment:' in {path}: '{line}'.");
            Assert.True(declared.TryAdd(match.Groups["key"].Value, match.Groups["value"].Value), $"Duplicated variable in {path}: '{line}'.");
        }

        Assert.Equal(
            DockerAppFixture.E2EEnvironment.OrderBy(entry => entry.Key, StringComparer.Ordinal),
            declared.OrderBy(entry => entry.Key, StringComparer.Ordinal));
    }

    [GeneratedRegex("^ {6}(?<key>[A-Za-z0-9_]+): (?:\"(?<value>[^\"]*)\"|(?<value>[^\"\\s#]+))$")]
    private static partial Regex EnvironmentEntryPattern();
}
