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
    public void SecondUpdate_SendsOnlyTheNewNodes()
    {
        // The regression guard that matters. The defect this prevents is payload size, so nothing
        // fails when it comes back -- without this test the fix is asserted rather than shown.
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.Render(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var update = module.Invocations["updateData"].Single();
        var dataJson = (string?)update.Arguments[1];

        StringAssert.Contains(dataJson!, "\"id\":\"n2\"");
        Assert.IsFalse(
            dataJson!.Contains("\"id\":\"n1\"", StringComparison.Ordinal),
            "n1 was already on screen and unchanged, so re-sending it is the bug this prevents.");
    }

    [TestMethod]
    public void AnUpdateChangingOneNodesStyle_SendsThatNodeAndNoOther()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.Render(p => p
            .Add(x => x.Nodes, new[]
            {
                new OrbNode("n1") { Style = new OrbNodeStyle { Color = "#f00" } },
                new OrbNode("n2"),
            })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var dataJson = (string?)module.Invocations["updateData"].Single().Arguments[1];

        StringAssert.Contains(dataJson!, "\"id\":\"n1\"");
        Assert.IsFalse(dataJson!.Contains("\"id\":\"n2\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AnUpdateChangingNothing_DoesNotCallIntoJavaScript()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        cut.Render(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, new[] { new OrbEdge("e1", "n1", "n2") }));

        Assert.IsFalse(module.Invocations.Identifiers.Contains("updateData"));
    }

    [TestMethod]
    public void ARemovalStillReachesTheView_EvenWithNothingAdded()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.Render(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var removedNodeIds = (string[]?)module.Invocations["updateData"].Single().Arguments[2];

        CollectionAssert.AreEquivalent(new[] { "n2" }, removedNodeIds);
    }

    [TestMethod]
    public void TheFirstUpdateAfterRender_DoesNotResendWhatSetupAlreadySent()
    {
        // Guards the seam between the two paths. If first render does not record what it sent,
        // everything looks changed on the first update and the whole graph goes again -- the
        // original bug, delayed by exactly one render.
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        cut.Render(p => p
            .Add(x => x.Nodes, new[] { new OrbNode("n1"), new OrbNode("n2") })
            .Add(x => x.Edges, Array.Empty<OrbEdge>()));

        var dataJson = (string?)module.Invocations["updateData"].Single().Arguments[1];

        Assert.IsFalse(dataJson!.Contains("\"id\":\"n1\"", StringComparison.Ordinal));
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
    public void Render_WithDuplicateNodeId_DoesNotThrowAndSkipsInitialization()
    {
        var module = SetupModule();
        var duplicateIdNodes = new[] { new OrbNode("n1"), new OrbNode("n1") };

        // A duplicate id makes OrbProjector.Project throw. Before this fix that exception
        // propagated out of OnAfterRenderAsync and tore down the whole Blazor circuit --
        // the component must instead degrade quietly, the same way dangling edges do.
        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, duplicateIdNodes));

        Assert.IsNotNull(cut);
        Assert.IsFalse(module.Invocations.Identifiers.Contains("initializeOrb"),
            "a graph that failed to project must never reach the JS init call");
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

    [TestMethod]
    public async Task SetSeedPositionsAsync_SendsTheCoordinatesToTheMap()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        await cut.Instance.SetSeedPositionsAsync([new OrbNodePosition("n1", 10, 20)]);

        Assert.AreEqual(1, module.Invocations["setSeedPositions"].Count);
    }

    [TestMethod]
    public async Task SetSeedPositionsAsync_AnEmptyBatch_DoesNotCallIntoJavaScript()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        await cut.Instance.SetSeedPositionsAsync([]);

        Assert.IsFalse(module.Invocations.Identifiers.Contains("setSeedPositions"));
    }

    [TestMethod]
    public async Task SetNodePositionsAsync_ReachesTheGraph()
    {
        var module = SetupModule();

        var cut = Render<OrbGraph<OrbNode, OrbEdge>>(p => p
            .Add(x => x.Nodes, TwoNodes)
            .Add(x => x.Edges, OneEdge));

        await cut.Instance.SetNodePositionsAsync([new OrbNodePosition("n1", 10, 20)]);

        Assert.AreEqual(1, module.Invocations["setNodePositions"].Count);
    }
}
