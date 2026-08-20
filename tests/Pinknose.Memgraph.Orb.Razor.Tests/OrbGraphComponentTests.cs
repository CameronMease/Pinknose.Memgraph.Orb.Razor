using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Pinknose.Memgraph.Orb.Razor.Tests;

[TestClass]
public class OrbGraphComponentTests : BunitContext
{
    private static readonly OrbNode[] TwoNodes = [new OrbNode("n1"), new OrbNode("n2")];
    private static readonly OrbEdge[] OneEdge = [new OrbEdge("e1", "n1", "n2")];

    private BunitJSModuleInterop SetupModule()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return JSInterop.SetupModule("./_content/Pinknose.Memgraph.Orb.Razor/orbGraph.js");
    }

    [TestMethod]
    public void Render_InitializesOrbWithProjectedData()
    {
        var module = SetupModule();

        Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        var init = module.Invocations["initializeOrb"].Single();
        var dataJson = (string?)init.Arguments[3];

        StringAssert.Contains(dataJson!, "\"id\":\"n1\"");
        StringAssert.Contains(dataJson!, "\"start\":\"n1\"");
    }

    [TestMethod]
    public void Render_SubscribesOnlyToWiredEvents()
    {
        var module = SetupModule();

        Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.OnNodeClick, _ => { }));

        var subscribed = (string[]?)module.Invocations["initializeOrb"].Single().Arguments[4];

        CollectionAssert.Contains(subscribed, "node-click");
        CollectionAssert.DoesNotContain(subscribed, "node-hover-enter");
    }

    [TestMethod]
    public void HostElement_MergesConsumerClassWithBaseClass()
    {
        SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .AddUnmatched("class", "border rounded")
            .AddUnmatched("id", "org-chart"));

        var div = cut.Find("div");

        StringAssert.Contains(div.GetAttribute("class")!, "orb-graph");
        StringAssert.Contains(div.GetAttribute("class")!, "border rounded");
        Assert.AreEqual("org-chart", div.GetAttribute("id"));
    }

    [TestMethod]
    public void HostElement_AppliesWidthAndHeight()
    {
        SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Height, "600px"));

        StringAssert.Contains(cut.Find("div").GetAttribute("style")!, "height:600px");
    }

    [TestMethod]
    public void UnchangedParameters_DoNotCallUpdateData()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        // bUnit 2.9.0 has no parameterless re-render; re-supplying identical parameter
        // values triggers the same re-render path that SetParametersAndRender() used to.
        cut.Render(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        Assert.AreEqual(0, module.Invocations["updateData"].Count,
            "an unchanged payload must not cross the interop boundary");
    }

    [TestMethod]
    public void RemovingANode_SendsItsIdForRemoval()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        cut.Render(p => p
            .Add(x => x.Nodes, [TwoNodes[0]])
            .Add(x => x.Edges, System.Array.Empty<OrbEdge>()));

        var update = module.Invocations["updateData"].Single();
        var removedNodeIds = (string[]?)update.Arguments[2];

        CollectionAssert.Contains(removedNodeIds, "n2");
    }

    [TestMethod]
    public async Task NodeClickFromJs_InvokesCallbackWithOriginalInstance()
    {
        SetupModule();

        OrbNode? clicked = null;
        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.OnNodeClick, e => clicked = e.Node));

        await cut.Instance.HandleEventForTestsAsync("node-click", new OrbEventPayload { Id = "n1" });

        Assert.AreSame(TwoNodes[0], clicked);
    }

    [TestMethod]
    public async Task ImperativeCallAfterDispose_IsSilentNoOp()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes));

        var graph = cut.Instance;
        await graph.DisposeAsync();

        // Must not throw, must not reach interop, and must not hang on the readiness gate.
        await graph.RecenterAsync();

        Assert.IsFalse(module.Invocations.Identifiers.Contains("recenter"),
            "disposal must short-circuit before the call reaches interop");
    }
}
