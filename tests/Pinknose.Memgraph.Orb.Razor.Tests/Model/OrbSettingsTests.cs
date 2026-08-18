namespace Pinknose.Memgraph.Orb.Razor.Tests.Model;

[TestClass]
public class OrbSettingsTests
{
    [TestMethod]
    public void Settings_DefaultToAllNull()
    {
        var settings = new OrbSettings();

        Assert.IsNull(settings.Render);
        Assert.IsNull(settings.Layout);
        Assert.IsNull(settings.ZoomFitTransitionMs);
    }

    [TestMethod]
    public void Layouts_ReportTheirOrbTypeDiscriminator()
    {
        Assert.AreEqual("force", new OrbForceLayout().LayoutType);
        Assert.AreEqual("grid", new OrbGridLayout().LayoutType);
        Assert.AreEqual("circular", new OrbCircularLayout().LayoutType);
        Assert.AreEqual("hierarchical", new OrbHierarchicalLayout().LayoutType);
    }

    [TestMethod]
    public void Layout_CarriesAnchorsFromTheBase()
    {
        var layout = new OrbGridLayout { RowGap = 40, AnchorX = OrbAnchor.Center };

        Assert.AreEqual(40d, layout.RowGap);
        Assert.AreEqual(OrbAnchor.Center, layout.AnchorX);
        Assert.IsNull(layout.AnchorY);
    }

    [TestMethod]
    public void ForceLayout_ExposesNestedOptionObjects()
    {
        var layout = new OrbForceLayout
        {
            IsPhysicsEnabled = true,
            Links = new OrbForceLinks { Distance = 120 },
            ManyBody = new OrbForceManyBody { Strength = -50 }
        };

        Assert.IsTrue(layout.IsPhysicsEnabled);
        Assert.AreEqual(120d, layout.Links!.Distance);
        Assert.AreEqual(-50d, layout.ManyBody!.Strength);
    }
}
