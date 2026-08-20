using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pinknose.Memgraph.Orb.Razor.Tests.Model;

[TestClass]
public class OrbStyleTests
{
    [TestMethod]
    public void NodeStyle_DefaultsToAllNull()
    {
        var style = new OrbNodeStyle();

        Assert.IsNull(style.Color);
        Assert.IsNull(style.Size);
        Assert.IsNull(style.Shape);
    }

    [TestMethod]
    public void NodeStyle_HasNoLabelProperty()
    {
        // Label lives on IOrbNode, not the style bag — two ways to set one thing is a bug.
        Assert.IsNull(typeof(OrbNodeStyle).GetProperty("Label"));
        Assert.IsNull(typeof(OrbEdgeStyle).GetProperty("Label"));
    }

    [TestMethod]
    public void LineStyle_SharedInstancesCarryTheirKind()
    {
        Assert.AreEqual("solid", OrbEdgeLineStyle.Solid.Kind);
        Assert.AreEqual("dashed", OrbEdgeLineStyle.Dashed.Kind);
        Assert.AreEqual("dotted", OrbEdgeLineStyle.Dotted.Kind);
    }

    [TestMethod]
    public void LineStyle_CustomCarriesPattern()
    {
        var custom = OrbEdgeLineStyle.Custom(4, 2, 1);

        Assert.AreEqual("custom", custom.Kind);
        CollectionAssert.AreEqual(new double[] { 4, 2, 1 }, custom.Pattern!.ToArray());
    }

    [TestMethod]
    public void LineStyle_CustomRejectsEmptyPattern()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OrbEdgeLineStyle.Custom());
    }

    [TestMethod]
    public void Custom_DoesNotKeepTheCallersArray()
    {
        // The caller owns the array they passed; if the style holds onto it, their later edit
        // silently repaints an edge that was configured long ago -- and Custom looks immutable,
        // so nothing about the API suggests that could happen.
        var pattern = new double[] { 4, 2 };

        var style = OrbEdgeLineStyle.Custom(pattern);
        pattern[0] = 99;

        Assert.AreEqual(4d, style.Pattern![0]);
    }

    [TestMethod]
    public void Custom_DoesNotHandOutAMutableArray()
    {
        var style = OrbEdgeLineStyle.Custom(4, 2);

        // Reading the pattern must not be a way to edit it either.
        Assert.IsNotInstanceOfType<double[]>(
            style.Pattern,
            "Pattern hands back the live array, so a caller can rewrite the dash pattern in place");
    }

    [TestMethod]
    public void RendererType_SerializesToOrbsExactStrings()
    {
        // Guards a real trap: plain camelCase turns "WebGl" into "webGl", which Orb
        // ignores. The [JsonStringEnumMemberName] attribute is what makes this pass.
        var options = new JsonSerializerOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        Assert.AreEqual("\"webgl\"", JsonSerializer.Serialize(OrbRendererType.WebGl, options));
        Assert.AreEqual("\"canvas\"", JsonSerializer.Serialize(OrbRendererType.Canvas, options));
        Assert.AreEqual("\"triangleDown\"", JsonSerializer.Serialize(OrbNodeShape.TriangleDown, options));
    }
}
