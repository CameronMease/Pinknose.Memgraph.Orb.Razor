namespace Pinknose.Memgraph.Orb.Razor.Demo;

/// <summary>What a node is, which decides its colour.</summary>
public enum ProjectKind
{
    /// <summary>A deployable application.</summary>
    App,

    /// <summary>A project in the solution.</summary>
    Library,

    /// <summary>A package from NuGet.</summary>
    Package,

    /// <summary>A test project.</summary>
    Test
}

/// <summary>
/// A node in the demo graph: one project or package in a solution.
/// </summary>
// This is an ordinary domain record that happens to implement IOrbNode. Nothing converts it
// into a separate view model, and the click and hover events hand this same instance back --
// which is the point the demo's event log is making.
public sealed record Project(string Id, string Name, ProjectKind Kind) : IOrbNode
{
    /// <summary>How many other nodes depend on this one. Drives the node's size.</summary>
    public int Dependents { get; init; }

    /// <inheritdoc />
    public string? Label => Name;

    /// <inheritdoc />
    public OrbNodeStyle? Style => new()
    {
        Color = Colour,
        // Enough spread to read weight at a glance, capped so one hub cannot swamp the plate.
        Size = 6 + Math.Min(Dependents, 10) * 1.4,
        FontSize = 9,
        FontColor = "#14202b",
        FontBackgroundColor = "rgba(255,255,255,0.82)"
    };

    /// <summary>The kind's colour, shared with the legend so the two cannot drift apart.</summary>
    public string Colour => Kind switch
    {
        ProjectKind.App => "#0e7c6b",
        ProjectKind.Library => "#1f5fa9",
        ProjectKind.Package => "#8a6d1f",
        _ => "#9a3f6b"
    };
}

/// <summary>A "depends on" edge between two projects.</summary>
public sealed record Dependency(string Id, string Start, string End) : IOrbEdge
{
    /// <inheritdoc />
    // No label: sixty labelled edges is noise, and an edge with no label exercises the path
    // where the projector sends no style at all.
    public OrbEdgeStyle? Style => new() { Color = "#94a3b1", Width = 0.6, ArrowSize = 1.2 };
}

/// <summary>The demo's data: a plausible mid-sized .NET solution and what it depends on.</summary>
public static class SolutionGraph
{
    private static readonly (string Id, string Name, ProjectKind Kind)[] Nodes =
    [
        ("api", "Contoso.Api", ProjectKind.App),
        ("worker", "Contoso.Worker", ProjectKind.App),
        ("dashboard", "Contoso.Dashboard", ProjectKind.App),

        ("core", "Contoso.Core", ProjectKind.Library),
        ("domain", "Contoso.Domain", ProjectKind.Library),
        ("contracts", "Contoso.Contracts", ProjectKind.Library),
        ("persistence", "Contoso.Persistence", ProjectKind.Library),
        ("messaging", "Contoso.Messaging", ProjectKind.Library),
        ("telemetry", "Contoso.Telemetry", ProjectKind.Library),
        ("auth", "Contoso.Auth", ProjectKind.Library),
        ("http", "Contoso.Http", ProjectKind.Library),
        ("caching", "Contoso.Caching", ProjectKind.Library),
        ("search", "Contoso.Search", ProjectKind.Library),
        ("reporting", "Contoso.Reporting", ProjectKind.Library),
        ("validation", "Contoso.Validation", ProjectKind.Library),

        ("di", "Extensions.DependencyInjection", ProjectKind.Package),
        ("logging", "Extensions.Logging", ProjectKind.Package),
        ("config", "Extensions.Configuration", ProjectKind.Package),
        ("json", "System.Text.Json", ProjectKind.Package),
        ("npgsql", "Npgsql", ProjectKind.Package),
        ("dapper", "Dapper", ProjectKind.Package),
        ("redis", "StackExchange.Redis", ProjectKind.Package),
        ("rabbit", "RabbitMQ.Client", ProjectKind.Package),
        ("serilog", "Serilog", ProjectKind.Package),
        ("otel", "OpenTelemetry", ProjectKind.Package),
        ("fluent", "FluentValidation", ProjectKind.Package),
        ("polly", "Polly", ProjectKind.Package),
        ("mediatr", "MediatR", ProjectKind.Package),
        ("elastic", "Elastic.Clients", ProjectKind.Package),

        ("core-tests", "Core.Tests", ProjectKind.Test),
        ("domain-tests", "Domain.Tests", ProjectKind.Test),
        ("api-tests", "Api.Tests", ProjectKind.Test),
        ("persistence-tests", "Persistence.Tests", ProjectKind.Test),
        ("messaging-tests", "Messaging.Tests", ProjectKind.Test),
        ("integration-tests", "Integration.Tests", ProjectKind.Test)
    ];

    private static readonly (string From, string To)[] Edges =
    [
        ("api", "core"), ("api", "domain"), ("api", "auth"), ("api", "http"),
        ("api", "persistence"), ("api", "telemetry"), ("api", "mediatr"), ("api", "validation"),
        ("worker", "core"), ("worker", "messaging"), ("worker", "persistence"),
        ("worker", "telemetry"), ("worker", "reporting"), ("worker", "polly"),
        ("dashboard", "core"), ("dashboard", "contracts"), ("dashboard", "http"),
        ("dashboard", "search"), ("dashboard", "telemetry"),

        ("core", "di"), ("core", "logging"), ("core", "config"), ("core", "json"),
        ("domain", "core"), ("domain", "contracts"), ("domain", "validation"),
        ("contracts", "json"),
        ("persistence", "core"), ("persistence", "domain"), ("persistence", "npgsql"),
        ("persistence", "dapper"), ("persistence", "caching"),
        ("messaging", "core"), ("messaging", "contracts"), ("messaging", "rabbit"),
        ("messaging", "polly"), ("messaging", "json"),
        ("telemetry", "core"), ("telemetry", "otel"), ("telemetry", "serilog"),
        ("auth", "core"), ("auth", "http"), ("auth", "caching"),
        ("http", "core"), ("http", "polly"), ("http", "json"),
        ("caching", "core"), ("caching", "redis"),
        ("search", "core"), ("search", "contracts"), ("search", "elastic"),
        ("reporting", "core"), ("reporting", "domain"), ("reporting", "persistence"),
        ("validation", "fluent"), ("validation", "core"),

        ("core-tests", "core"), ("domain-tests", "domain"), ("api-tests", "api"),
        ("persistence-tests", "persistence"), ("messaging-tests", "messaging"),
        ("integration-tests", "api"), ("integration-tests", "worker"),
        ("integration-tests", "persistence"), ("integration-tests", "messaging")
    ];

    /// <summary>The projects, sized by how many other projects depend on each.</summary>
    public static List<Project> Projects { get; } =
    [
        .. Nodes.Select(n => new Project(n.Id, n.Name, n.Kind)
        {
            Dependents = Edges.Count(e => e.To == n.Id)
        })
    ];

    /// <summary>The dependencies between them.</summary>
    public static List<Dependency> Dependencies { get; } =
    [
        .. Edges.Select((e, i) => new Dependency($"e{i}", e.From, e.To))
    ];
}
