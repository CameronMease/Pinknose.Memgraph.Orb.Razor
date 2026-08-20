namespace Pinknose.Memgraph.Orb.Razor;

internal sealed class OrbProjectionResult<TNode, TEdge>
{
    public required OrbGraphPayload Payload { get; init; }
    public required Dictionary<string, TNode> NodesById { get; init; }
    public required Dictionary<string, TEdge> EdgesById { get; init; }
    public required IReadOnlyList<string> DanglingEdgeIds { get; init; }
}

/// <summary>Projects consumer objects down to the handful of fields Orb needs.</summary>
internal static class OrbProjector
{
    public static OrbProjectionResult<TNode, TEdge> Project<TNode, TEdge>(
        IEnumerable<TNode> nodes,
        IEnumerable<TEdge> edges)
        where TNode : IOrbNode
        where TEdge : IOrbEdge
    {
        var nodesById = new Dictionary<string, TNode>(StringComparer.Ordinal);
        var nodePayloads = new List<OrbNodePayload>();

        foreach (var node in nodes)
        {
            if (!nodesById.TryAdd(node.Id, node))
            {
                throw new InvalidOperationException($"Duplicate node id '{node.Id}'.");
            }

            nodePayloads.Add(new OrbNodePayload
            {
                Id = node.Id,
                Style = MergeLabel(node.Style, node.Label)
            });
        }

        var edgesById = new Dictionary<string, TEdge>(StringComparer.Ordinal);
        var edgePayloads = new List<OrbEdgePayload>();
        var dangling = new List<string>();

        foreach (var edge in edges)
        {
            if (!edgesById.TryAdd(edge.Id, edge))
            {
                throw new InvalidOperationException($"Duplicate edge id '{edge.Id}'.");
            }

            if (!nodesById.ContainsKey(edge.Start) || !nodesById.ContainsKey(edge.End))
            {
                dangling.Add(edge.Id);
            }

            edgePayloads.Add(new OrbEdgePayload
            {
                Id = edge.Id,
                Start = edge.Start,
                End = edge.End,
                Style = MergeLabel(edge.Style, edge.Label)
            });
        }

        return new OrbProjectionResult<TNode, TEdge>
        {
            Payload = new OrbGraphPayload { Nodes = nodePayloads, Edges = edgePayloads },
            NodesById = nodesById,
            EdgesById = edgesById,
            DanglingEdgeIds = dangling
        };
    }

    // Copies into the wire DTO — the consumer's style instance is theirs, not ours.
    private static OrbNodeStylePayload? MergeLabel(OrbNodeStyle? style, string? label)
    {
        if (style is null && label is null)
        {
            return null;
        }

        return new OrbNodeStylePayload
        {
            Color = style?.Color,
            ColorHover = style?.ColorHover,
            ColorSelected = style?.ColorSelected,
            BorderColor = style?.BorderColor,
            BorderColorHover = style?.BorderColorHover,
            BorderColorSelected = style?.BorderColorSelected,
            BorderWidth = style?.BorderWidth,
            BorderWidthSelected = style?.BorderWidthSelected,
            FontBackgroundColor = style?.FontBackgroundColor,
            FontColor = style?.FontColor,
            FontFamily = style?.FontFamily,
            FontSize = style?.FontSize,
            ImageUrl = style?.ImageUrl,
            ImageUrlSelected = style?.ImageUrlSelected,
            ShadowColor = style?.ShadowColor,
            ShadowSize = style?.ShadowSize,
            ShadowOffsetX = style?.ShadowOffsetX,
            ShadowOffsetY = style?.ShadowOffsetY,
            Shape = style?.Shape,
            Size = style?.Size,
            Mass = style?.Mass,
            ZIndex = style?.ZIndex,
            Label = label
        };
    }

    private static OrbEdgeStylePayload? MergeLabel(OrbEdgeStyle? style, string? label)
    {
        if (style is null && label is null)
        {
            return null;
        }

        return new OrbEdgeStylePayload
        {
            Color = style?.Color,
            ColorHover = style?.ColorHover,
            ColorSelected = style?.ColorSelected,
            Width = style?.Width,
            WidthHover = style?.WidthHover,
            WidthSelected = style?.WidthSelected,
            ArrowSize = style?.ArrowSize,
            FontBackgroundColor = style?.FontBackgroundColor,
            FontColor = style?.FontColor,
            FontFamily = style?.FontFamily,
            FontSize = style?.FontSize,
            ShadowColor = style?.ShadowColor,
            ShadowSize = style?.ShadowSize,
            ShadowOffsetX = style?.ShadowOffsetX,
            ShadowOffsetY = style?.ShadowOffsetY,
            ZIndex = style?.ZIndex,
            LineStyle = style?.LineStyle,
            Label = label
        };
    }
}
