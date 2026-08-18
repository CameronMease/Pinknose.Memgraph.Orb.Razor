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
        CollectionAssert.AreEqual(new double[] { 4, 2, 1 }, custom.Pattern);
    }

    [TestMethod]
    public void LineStyle_CustomRejectsEmptyPattern()
    {
        Assert.ThrowsExactly<ArgumentException>(() => OrbEdgeLineStyle.Custom());
    }
}
