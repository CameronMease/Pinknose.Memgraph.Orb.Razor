using System.Text.Json;

namespace Pinknose.Memgraph.Orb.Razor.Tests.Serialization;

[TestClass]
public class OrbSerializationTests
{
    [TestMethod]
    public void Payload_UsesCamelCaseAndOmitsNulls()
    {
        var payload = new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#c33" } }],
            Edges = []
        };

        var json = OrbJson.SerializeGraph(payload);

        Assert.AreEqual("""{"nodes":[{"id":"n1","style":{"color":"#c33"}}],"edges":[]}""", json);
    }

    [TestMethod]
    public void StylePayload_EmitsLabelWhereOrbExpectsIt()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Label = "Alice" } }],
            Edges = []
        });

        StringAssert.Contains(json, "\"style\":{\"label\":\"Alice\"}");
    }

    [TestMethod]
    public void NodePayload_NeverEmitsPositionKeys()
    {
        // Absence of x/y is what makes Orb's merge() preserve existing positions.
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1" }],
            Edges = []
        });

        Assert.IsFalse(json.Contains("\"x\""), json);
        Assert.IsFalse(json.Contains("\"y\""), json);
    }

    [TestMethod]
    public void Enums_SerializeAsOrbCamelCaseStrings()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Shape = OrbNodeShape.TriangleDown } }],
            Edges = []
        });

        StringAssert.Contains(json, "\"shape\":\"triangleDown\"");
    }

    [TestMethod]
    public void Settings_SerializeSparsely()
    {
        var json = OrbJson.SerializeSettings(new OrbSettings
        {
            Interaction = new OrbInteractionSettings { IsZoomEnabled = false }
        });

        Assert.AreEqual("""{"interaction":{"isZoomEnabled":false}}""", json);
    }

    [TestMethod]
    public void Layout_SerializesAsTypeAndOptions()
    {
        var json = OrbJson.SerializeSettings(new OrbSettings
        {
            Layout = new OrbGridLayout { RowGap = 40 }
        });

        Assert.AreEqual("""{"layout":{"type":"grid","options":{"rowGap":40}}}""", json);
    }

    [TestMethod]
    public void RenamedAndAcronymKeys_UseOrbsExactWireNames()
    {
        // Three keys do NOT follow from plain camelCase and are pinned with
        // [JsonPropertyName]. Each was a silent-drop bug before it was pinned:
        // Orb ignores an unrecognised key rather than failing.
        var json = OrbJson.SerializeSettings(new OrbSettings
        {
            Selection = new OrbSelectionSettings { IsDefaultSelectEnabled = false },
            Render = new OrbRenderSettings { Type = OrbRendererType.WebGl },
            Layout = new OrbForceLayout { UseGpu = true }
        });

        StringAssert.Contains(json, "\"strategy\":{\"isDefaultSelectEnabled\":false}");
        StringAssert.Contains(json, "\"type\":\"webgl\"");
        StringAssert.Contains(json, "\"useGPU\":true");

        Assert.IsFalse(json.Contains("\"selection\""), "Orb's key is 'strategy', not 'selection'");
        Assert.IsFalse(json.Contains("\"useGpu\""), "Orb's key is 'useGPU'");
        Assert.IsFalse(json.Contains("\"webGl\""), "Orb's renderer value is 'webgl'");
    }

    [TestMethod]
    public void LineStyle_SerializesSharedKindWithoutPattern()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [],
            Edges = [new OrbEdgePayload
            {
                Id = "e1", Start = "n1", End = "n2",
                Style = new OrbEdgeStylePayload { LineStyle = OrbEdgeLineStyle.Dashed }
            }]
        });

        StringAssert.Contains(json, "\"lineStyle\":{\"type\":\"dashed\"}");
    }

    [TestMethod]
    public void LineStyle_SerializesCustomPattern()
    {
        var json = OrbJson.SerializeGraph(new OrbGraphPayload
        {
            Nodes = [],
            Edges = [new OrbEdgePayload
            {
                Id = "e1", Start = "n1", End = "n2",
                Style = new OrbEdgeStylePayload { LineStyle = OrbEdgeLineStyle.Custom(4, 2) }
            }]
        });

        StringAssert.Contains(json, "\"lineStyle\":{\"type\":\"custom\",\"pattern\":[4,2]}");
    }

    [TestMethod]
    public void SerializeNode_ProducesTheSameShapeAsInsideAWholeGraph()
    {
        // The per-node serialization is what change detection compares, so it has to describe the
        // node exactly as the graph payload does. If the two ever diverge, a node could look
        // unchanged individually while its graph representation differs.
        var node = new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#fff" } };

        var alone = OrbJson.SerializeNode(node);
        var inGraph = OrbJson.SerializeGraph(new OrbGraphPayload { Nodes = [node], Edges = [] });

        StringAssert.Contains(inGraph, alone);
    }

    [TestMethod]
    public void SerializeNode_TwoNodesDifferingOnlyByStyle_SerializeDifferently()
    {
        var a = new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#fff" } };
        var b = new OrbNodePayload { Id = "n1", Style = new OrbNodeStylePayload { Color = "#000" } };

        Assert.AreNotEqual(OrbJson.SerializeNode(a), OrbJson.SerializeNode(b));
    }

    [TestMethod]
    public void SerializeEdge_IncludesEndpoints()
    {
        var edge = new OrbEdgePayload { Id = "e1", Start = "n1", End = "n2" };

        var json = OrbJson.SerializeEdge(edge);

        StringAssert.Contains(json, "\"start\":\"n1\"");
        StringAssert.Contains(json, "\"end\":\"n2\"");
    }
}
