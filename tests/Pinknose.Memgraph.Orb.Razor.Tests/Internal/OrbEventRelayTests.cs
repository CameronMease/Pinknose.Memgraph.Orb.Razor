namespace Pinknose.Memgraph.Orb.Razor.Tests.Internal;

[TestClass]
public class OrbEventRelayTests
{
    [TestMethod]
    public async Task HandleOrbEvent_ForwardsTypeAndPayload()
    {
        string? seenType = null;
        OrbEventPayload? seenPayload = null;

        var relay = new OrbEventRelay((type, payload) =>
        {
            seenType = type;
            seenPayload = payload;
            return Task.CompletedTask;
        });

        await relay.HandleOrbEvent("node-click", new OrbEventPayload
        {
            Id = "n1", LocalX = 1, LocalY = 2, GlobalX = 3, GlobalY = 4
        });

        Assert.AreEqual("node-click", seenType);
        Assert.AreEqual("n1", seenPayload!.Id);
        Assert.AreEqual(1d, seenPayload.LocalX);
        Assert.AreEqual(4d, seenPayload.GlobalY);
    }

    [TestMethod]
    public void EventArgs_CarryTheOriginalInstance()
    {
        var alice = new OrbNode("n1");
        var args = new OrbNodeEventArgs<OrbNode>
        {
            Node = alice,
            LocalPoint = new OrbPoint(1, 2),
            GlobalPoint = new OrbPoint(3, 4)
        };

        Assert.AreSame(alice, args.Node);
        Assert.AreEqual(1d, args.LocalPoint.X);
    }
}
