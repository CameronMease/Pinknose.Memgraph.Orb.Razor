using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace Pinknose.Memgraph.Orb.Razor;

/// <summary>
/// Non-generic [JSInvokable] target. Keeping this off the open generic component avoids
/// generic-interop sharp edges and gives the dispatch path one job.
/// </summary>
internal sealed class OrbEventRelay(Func<string, OrbEventPayload, Task> dispatch)
{
    private readonly Func<string, OrbEventPayload, Task> _dispatch = dispatch;

    // Blazor binds this parameter by deserializing JSON into it reflectively, inside
    // DotNetDispatcher -- so nothing in compiled code ever calls OrbEventPayload's property
    // setters, and a trimmer is entitled to delete them. It does: with <IsTrimmable>true</>,
    // set_LocalX and its siblings disappear from the published assembly while the type and its
    // getters remain, so the relay is still invoked but every field arrives at its default.
    // The symptom is silent -- no exception, just a click that selects nothing.
    //
    // DynamicDependency is the trimmer's "if you keep this method, keep those members too".
    // Verified by TrimmedWasm_RaisesNodeClickBackToDotNet, which fails without it.
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(OrbEventPayload))]
    [JSInvokable]
    public Task HandleOrbEvent(string type, OrbEventPayload payload) => _dispatch(type, payload);
}
