namespace Pinknose.Memgraph.Orb.Razor.Tests.Model;

[TestClass]
public class OrbNodeTests
{
    private sealed record Person(string EmployeeId, string FullName) : IOrbNode
    {
        public string Id => EmployeeId;
        public string? Label => FullName;
    }

    [TestMethod]
    public void OrbNode_ExposesIdAndOptionalMembers()
    {
        var node = new OrbNode("n1") { Label = "Alice" };

        Assert.AreEqual("n1", node.Id);
        Assert.AreEqual("Alice", node.Label);
        Assert.IsNull(node.Style);
    }

    [TestMethod]
    public void OrbEdge_ExposesEndpoints()
    {
        var edge = new OrbEdge("e1", "n1", "n2") { Label = "KNOWS" };

        Assert.AreEqual("e1", edge.Id);
        Assert.AreEqual("n1", edge.Start);
        Assert.AreEqual("n2", edge.End);
        Assert.AreEqual("KNOWS", edge.Label);
    }

    [TestMethod]
    public void CustomType_ImplementingIOrbNode_NeedsOnlyId()
    {
        IOrbNode person = new Person("E-1", "Ada");

        Assert.AreEqual("E-1", person.Id);
        Assert.AreEqual("Ada", person.Label);
        Assert.IsNull(person.Style);          // default interface member
    }
}
