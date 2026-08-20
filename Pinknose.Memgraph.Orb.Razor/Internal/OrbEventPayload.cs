namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>Wire shape for events coming back from JavaScript.</summary>
public sealed class OrbEventPayload
{
    public string? Id { get; set; }
    public double LocalX { get; set; }
    public double LocalY { get; set; }
    public double GlobalX { get; set; }
    public double GlobalY { get; set; }
}
