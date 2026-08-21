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

    [TestMethod]
    public void ChangedIds_ReturnsWhatIsNew()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1" },
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        CollectionAssert.AreEquivalent(new[] { "b" }, changed);
    }

    [TestMethod]
    public void ChangedIds_ReturnsWhatDiffers()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1" },
            new Dictionary<string, string> { ["a"] = "2" });

        CollectionAssert.AreEquivalent(new[] { "a" }, changed);
    }

    [TestMethod]
    public void ChangedIds_IgnoresWhatIsIdentical()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" },
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        Assert.AreEqual(0, changed.Length);
    }

    [TestMethod]
    public void ChangedIds_FirstUpdate_ReturnsEverything()
    {
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });

        CollectionAssert.AreEquivalent(new[] { "a", "b" }, changed);
    }

    [TestMethod]
    public void ChangedIds_IgnoresRemovals()
    {
        // Removal is RemovedIds' job. An id absent from current is not a change to send.
        var changed = OrbGraphDiff.ChangedIds(
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" },
            new Dictionary<string, string> { ["a"] = "1" });

        Assert.AreEqual(0, changed.Length);
    }
}
