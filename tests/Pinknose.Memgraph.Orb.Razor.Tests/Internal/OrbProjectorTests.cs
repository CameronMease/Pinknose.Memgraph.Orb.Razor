namespace Pinknose.Memgraph.Orb.Razor.Tests.Internal;

[TestClass]
public class OrbProjectorTests
{
    private static OrbProjectionResult<OrbNode, OrbEdge> Project(
        IEnumerable<OrbNode> nodes, IEnumerable<OrbEdge> edges)
        => OrbProjector.Project(nodes, edges);

    [TestMethod]
    public void Project_MapsIdsAndEndpoints()
    {
        var result = Project(
            [new OrbNode("n1"), new OrbNode("n2")],
            [new OrbEdge("e1", "n1", "n2")]);

        Assert.AreEqual(2, result.Payload.Nodes.Count);
        Assert.AreEqual("e1", result.Payload.Edges[0].Id);
        Assert.AreEqual("n1", result.Payload.Edges[0].Start);
        Assert.AreEqual("n2", result.Payload.Edges[0].End);
    }

    [TestMethod]
    public void Project_FoldsLabelIntoStyle()
    {
        // Orb reads labels from style.label, so Label must land there.
        var result = Project([new OrbNode("n1") { Label = "Alice" }], []);

        Assert.AreEqual("Alice", result.Payload.Nodes[0].Style!.Label);
    }

    [TestMethod]
    public void Project_LabelDoesNotOverwriteOtherStyleProperties()
    {
        var result = Project(
            [new OrbNode("n1") { Label = "Alice", Style = new OrbNodeStyle { Color = "#c33" } }],
            []);

        var style = result.Payload.Nodes[0].Style!;
        Assert.AreEqual("Alice", style.Label);
        Assert.AreEqual("#c33", style.Color);
    }

    [TestMethod]
    public void Project_CopiesEveryStyleProperty()
    {
        var result = Project(
            [new OrbNode("n1") { Style = new OrbNodeStyle
            {
                Color = "#c33", Shape = OrbNodeShape.Hexagon, Size = 14, ZIndex = 3
            } }],
            []);

        var style = result.Payload.Nodes[0].Style!;
        Assert.AreEqual("#c33", style.Color);
        Assert.AreEqual(OrbNodeShape.Hexagon, style.Shape);
        Assert.AreEqual(14d, style.Size);
        Assert.AreEqual(3d, style.ZIndex);
    }

    [TestMethod]
    public void Project_CopiesEveryEdgeStyleProperty()
    {
        var result = Project(
            [new OrbNode("n1"), new OrbNode("n2")],
            [new OrbEdge("e1", "n1", "n2") { Style = new OrbEdgeStyle
            {
                Color = "#333", Width = 2, LineStyle = OrbEdgeLineStyle.Dotted
            } }]);

        var style = result.Payload.Edges[0].Style!;
        Assert.AreEqual("#333", style.Color);
        Assert.AreEqual(2d, style.Width);
        Assert.AreSame(OrbEdgeLineStyle.Dotted, style.LineStyle);
    }

    // This is the projector's half of the style-clearing contract: a node with neither
    // Style nor Label projects a null payload style, on both the first projection and every
    // later one (e.g. after a consumer's Style went from non-null back to null). The other
    // half lives in orbGraph.js's pushStyles(), which must treat a null/undefined style as
    // "push {} to reset", not "skip and leave whatever was painted before" -- Orb's
    // setStyle() replaces wholesale and merge() never touches _style, so skipping would make
    // a cleared style permanent. This test exists so that JS-side `?? {}` is understood to
    // be load-bearing, not incidental.
    [TestMethod]
    public void Project_LeavesStyleNullWhenNothingToStyle()
    {
        var result = Project([new OrbNode("n1")], []);

        Assert.IsNull(result.Payload.Nodes[0].Style);
    }

    [TestMethod]
    public void Project_IndexesOriginalInstancesById()
    {
        var alice = new OrbNode("n1");
        var result = Project([alice], []);

        Assert.AreSame(alice, result.NodesById["n1"]);
    }

    [TestMethod]
    public void Project_ReportsDanglingEdgesButStillSendsThem()
    {
        var result = Project([new OrbNode("n1")], [new OrbEdge("e1", "n1", "missing")]);

        CollectionAssert.AreEqual(new[] { "e1" }, result.DanglingEdgeIds.ToArray());
        Assert.AreEqual(1, result.Payload.Edges.Count,
            "dangling edges are still sent so the graph self-heals when the node arrives");
    }

    // The two MergeLabel overloads copy 39 properties by hand. Spot-checking a few would
    // let a dropped property through, so these walk every property reflectively.
    private static object SampleValue(Type t)
    {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        if (u == typeof(string)) return "x";
        if (u == typeof(double)) return 1.5d;
        if (u == typeof(bool)) return true;
        if (u == typeof(OrbEdgeLineStyle)) return OrbEdgeLineStyle.Dashed;
        if (u.IsEnum) return Enum.GetValues(u).GetValue(0)!;
        throw new NotSupportedException($"No sample value for {u}.");
    }

    [TestMethod]
    public void Project_CopiesEveryNodeStylePropertyToThePayload()
    {
        var style = new OrbNodeStyle();
        foreach (var p in typeof(OrbNodeStyle).GetProperties())
        {
            p.SetValue(style, SampleValue(p.PropertyType));
        }

        var payload = Project([new OrbNode("n1") { Style = style }], []).Payload.Nodes[0].Style!;

        foreach (var p in typeof(OrbNodeStyle).GetProperties())
        {
            var target = typeof(OrbNodeStylePayload).GetProperty(p.Name);
            Assert.IsNotNull(target, $"OrbNodeStylePayload is missing '{p.Name}'");
            Assert.IsNotNull(target.GetValue(payload), $"projector dropped '{p.Name}'");
        }
    }

    [TestMethod]
    public void Project_CopiesEveryEdgeStylePropertyToThePayload()
    {
        var style = new OrbEdgeStyle();
        foreach (var p in typeof(OrbEdgeStyle).GetProperties())
        {
            p.SetValue(style, SampleValue(p.PropertyType));
        }

        var edge = new OrbEdge("e1", "n1", "n2") { Style = style };
        var payload = Project([new OrbNode("n1"), new OrbNode("n2")], [edge]).Payload.Edges[0].Style!;

        foreach (var p in typeof(OrbEdgeStyle).GetProperties())
        {
            var target = typeof(OrbEdgeStylePayload).GetProperty(p.Name);
            Assert.IsNotNull(target, $"OrbEdgeStylePayload is missing '{p.Name}'");
            Assert.IsNotNull(target.GetValue(payload), $"projector dropped '{p.Name}'");
        }
    }

    [TestMethod]
    public void Project_ThrowsOnDuplicateNodeId()
    {
        Assert.ThrowsExactly<InvalidOperationException>(
            () => Project([new OrbNode("n1"), new OrbNode("n1")], []));
    }

    [TestMethod]
    public void Project_ThrowsOnDuplicateEdgeId()
    {
        // The node case above was covered from the start and this one never was, even though
        // both feed the same id-to-instance maps that events are resolved through: a duplicate
        // would make one of the two edges unreachable from every edge callback.
        Assert.ThrowsExactly<InvalidOperationException>(
            () => Project(
                [new OrbNode("n1"), new OrbNode("n2")],
                [new OrbEdge("e1", "n1", "n2"), new OrbEdge("e1", "n2", "n1")]));
    }
}
