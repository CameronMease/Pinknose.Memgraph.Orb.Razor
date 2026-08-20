namespace Pinknose.Memgraph.Orb.Razor.Tests.Internal;

[TestClass]
public class OrbGraphDiffTests
{
    [TestMethod]
    public void RemovedIds_ReturnsWhatDisappeared()
    {
        var removed = OrbGraphDiff.RemovedIds(["a", "b", "c"], ["a", "c"]);

        CollectionAssert.AreEquivalent(new[] { "b" }, removed);
    }

    [TestMethod]
    public void RemovedIds_IgnoresAdditions()
    {
        var removed = OrbGraphDiff.RemovedIds(["a"], ["a", "b"]);

        Assert.AreEqual(0, removed.Length);
    }

    [TestMethod]
    public void RemovedIds_HandlesEmptyPrevious()
    {
        var removed = OrbGraphDiff.RemovedIds([], ["a"]);

        Assert.AreEqual(0, removed.Length);
    }

    [TestMethod]
    public void RemovedIds_ReturnsAllWhenCurrentIsEmpty()
    {
        var removed = OrbGraphDiff.RemovedIds(["a", "b"], []);

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, removed);
    }
}
