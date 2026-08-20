using Microsoft.JSInterop;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Non-generic [JSInvokable] target. Keeping this off the open generic component avoids
/// generic-interop sharp edges and gives the dispatch path one job.
/// </summary>
public sealed class OrbEventRelay(Func<string, OrbEventPayload, Task> dispatch)
{
    private readonly Func<string, OrbEventPayload, Task> _dispatch = dispatch;

    [JSInvokable]
    public Task HandleOrbEvent(string type, OrbEventPayload payload) => _dispatch(type, payload);
}
