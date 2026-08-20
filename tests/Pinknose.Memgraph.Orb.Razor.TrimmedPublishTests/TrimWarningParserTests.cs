namespace Pinknose.Memgraph.Orb.Razor.TrimmedPublishTests;

/// <summary>Parses ILLink's warnings out of publish output.</summary>
// These run without ORB_TRIM_TESTS: they are pure string parsing, no publish involved. That
// matters, because the parser is what decides whether the trimmed suite's headline assertion
// ("the trimmer warned about nothing in the library") means anything. A parser that silently
// matches nothing turns that assertion into a guarantee of nothing -- so the case where the
// paths look unfamiliar has to be tested directly rather than inferred from a green run on
// the one platform that happens to be handy.
[TestClass]
public class TrimWarningParserTests
{
    private const string WindowsLibrary = @"C:\src\Pinknose.Memgraph.Orb.Razor\Pinknose.Memgraph.Orb.Razor\";

    private const string WindowsOutput = """
          Optimizing assemblies for size. This process might take a while.
        ILLink : Trim analysis warning IL2072: Microsoft.AspNetCore.Components.ComponentFactory.PerformPropertyInjection(IServiceProvider, IComponent): 'componentType' argument does not satisfy 'DynamicallyAccessedMemberTypes.All'. [C:\src\Pinknose.Memgraph.Orb.Razor\samples\Host\Host.csproj]
        C:\src\Pinknose.Memgraph.Orb.Razor\Pinknose.Memgraph.Orb.Razor\Serialization\OrbJsonContext.cs(23,12): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbJson.SerializeGraph(OrbGraphPayload): Using member 'System.Text.Json.JsonSerializer.Serialize' which has 'RequiresUnreferencedCodeAttribute'. [C:\src\Pinknose.Memgraph.Orb.Razor\samples\Host\Host.csproj]
        C:\src\Pinknose.Memgraph.Orb.Razor\Pinknose.Memgraph.Orb.Razor\Serialization\OrbJsonContext.cs(26,12): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbJson.SerializeSettings(OrbSettings): Using member 'System.Text.Json.JsonSerializer.Serialize' which has 'RequiresUnreferencedCodeAttribute'. [C:\src\Pinknose.Memgraph.Orb.Razor\samples\Host\Host.csproj]
        C:\src\Pinknose.Memgraph.Orb.Razor\Pinknose.Memgraph.Orb.Razor\Serialization\OrbJsonContext.cs(30,9): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbJson.Build(Boolean): Using member 'System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver.DefaultJsonTypeInfoResolver()' which has 'RequiresUnreferencedCodeAttribute'. [C:\src\Pinknose.Memgraph.Orb.Razor\samples\Host\Host.csproj]
        C:\src\Pinknose.Memgraph.Orb.Razor\Pinknose.Memgraph.Orb.Razor\Serialization\OrbLayoutConverter.cs(18,9): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbLayoutConverter.Write(Utf8JsonWriter, OrbLayout, JsonSerializerOptions): Using member 'System.Text.Json.JsonSerializer.Serialize' which has 'RequiresUnreferencedCodeAttribute'. [C:\src\Pinknose.Memgraph.Orb.Razor\samples\Host\Host.csproj]
        """;

    // What the same publish looks like on a GitHub-hosted Linux runner: no drive letter, and
    // forward slashes throughout.
    private const string LinuxLibrary =
        "/home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/";

    private const string LinuxOutput = """
          Optimizing assemblies for size. This process might take a while.
        ILLink : Trim analysis warning IL2072: Microsoft.AspNetCore.Components.ComponentFactory.PerformPropertyInjection(IServiceProvider, IComponent): 'componentType' argument does not satisfy 'DynamicallyAccessedMemberTypes.All'. [/home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/samples/Host/Host.csproj]
        /home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Serialization/OrbJsonContext.cs(23,12): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbJson.SerializeGraph(OrbGraphPayload): Using member 'System.Text.Json.JsonSerializer.Serialize' which has 'RequiresUnreferencedCodeAttribute'. [/home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/samples/Host/Host.csproj]
        /home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Serialization/OrbJsonContext.cs(26,12): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbJson.SerializeSettings(OrbSettings): Using member 'System.Text.Json.JsonSerializer.Serialize' which has 'RequiresUnreferencedCodeAttribute'. [/home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/samples/Host/Host.csproj]
        /home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Serialization/OrbJsonContext.cs(30,9): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbJson.Build(Boolean): Using member 'System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver.DefaultJsonTypeInfoResolver()' which has 'RequiresUnreferencedCodeAttribute'. [/home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/samples/Host/Host.csproj]
        /home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/Serialization/OrbLayoutConverter.cs(18,9): Trim analysis warning IL2026: Pinknose.Memgraph.Orb.Razor.OrbLayoutConverter.Write(Utf8JsonWriter, OrbLayout, JsonSerializerOptions): Using member 'System.Text.Json.JsonSerializer.Serialize' which has 'RequiresUnreferencedCodeAttribute'. [/home/runner/work/Pinknose.Memgraph.Orb.Razor/Pinknose.Memgraph.Orb.Razor/samples/Host/Host.csproj]
        """;

    [TestMethod]
    [DataRow("windows")]
    [DataRow("linux")]
    public void CountsEveryLibraryWarningOnEitherPlatform(string platform)
    {
        var warnings = Parse(platform);

        Assert.AreEqual(3, warnings.GetValueOrDefault("OrbJsonContext.cs"));
        Assert.AreEqual(1, warnings.GetValueOrDefault("OrbLayoutConverter.cs"));
    }

    [TestMethod]
    [DataRow("windows")]
    [DataRow("linux")]
    public void IgnoresWarningsFromOutsideTheLibrary(string platform)
    {
        // The IL2072 in the samples above is the framework's own, reported against the sample
        // host's project file. Counting those would make the library's own count unreadable.
        var warnings = Parse(platform);

        Assert.AreEqual(2, warnings.Count, $"unexpected files: {string.Join(", ", warnings.Keys)}");
    }

    [TestMethod]
    [DataRow("windows")]
    [DataRow("linux")]
    public void ReportsNothingForAPublishThatWarnedAboutNothing(string platform)
    {
        var library = platform == "windows" ? WindowsLibrary : LinuxLibrary;

        var warnings = TrimWarningParser.WarningsByFile(
            "  Optimizing assemblies for size. This process might take a while.",
            library);

        Assert.IsEmpty(warnings);
    }

    private static Dictionary<string, int> Parse(string platform)
        => platform == "windows"
            ? TrimWarningParser.WarningsByFile(WindowsOutput, WindowsLibrary)
            : TrimWarningParser.WarningsByFile(LinuxOutput, LinuxLibrary);
}
