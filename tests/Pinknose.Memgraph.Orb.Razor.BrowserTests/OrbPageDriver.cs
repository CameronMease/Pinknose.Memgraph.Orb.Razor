using System.Text.Json;
using Microsoft.Playwright;

namespace Pinknose.Memgraph.Orb.Razor.BrowserTests;

/// <summary>Reads and drives an OrbGraph rendered on a page.</summary>
// Everything here reaches into the graph through window.__orbTestView, the opt-in test seam
// the component exposes when the host carries data-orb-test. It lives in its own class
// because the trimmed-publish suite links this file and drives a published app with it: the
// two suites must agree on what "the graph rendered" and "click the first node" mean, or a
// trimming result cannot be compared against the dev-build result it is supposed to match.
internal sealed class OrbPageDriver(IPage page)
{
    private const string CountPaintedPixelsScript = """
        () => {
            const c = document.querySelector('.orb-graph canvas');
            if (!c) return -1;
            const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data;
            let painted = 0;
            for (let i = 3; i < d.length; i += 4) { if (d[i] !== 0) painted++; }
            return painted;
        }
        """;

    private const string ReadNodePositionsScript = """
        () => {
            const v = window.__orbTestView;
            return v ? JSON.stringify(v.data.getNodePositions()) : null;
        }
        """;

    // Shared by NodeHasPositionAsync (single check) and WaitForPositionAsync (polled via
    // WaitForFunctionAsync) so the two agree on what "has a position" means. A node can appear
    // in getNodePositions() with only its "id" -- no x/y -- when it exists but nothing has
    // assigned it a position yet (see the comment on NodeHasPositionAsync below), so presence
    // in the array is not enough; the x/y fields must actually be numbers.
    private const string HasPositionScript = """
        (id) => {
            const p = window.__orbTestView.data.getNodePositions().find((p) => p.id === id);
            return !!p && typeof p.x === 'number' && typeof p.y === 'number';
        }
        """;

    // getCenter() returns the node's position in Orb's *graph* (simulation) space, not canvas
    // pixels: Orb's canvas renderer draws with
    //   ctx.translate(transform.x, transform.y); ctx.scale(transform.k, transform.k);
    //   ctx.translate(width / 2, height / 2)   // OrbView always calls translateOriginToCenter()
    // so a graph point (gx, gy) lands on the canvas at
    //   (transform.x + transform.k * (gx + width / 2), transform.y + transform.k * (gy + height / 2)).
    // view._renderer is not exposed via a public getter but is a plain (non-#private) field,
    // so it is reachable from test code the same way window.__orbTestView already is.
    // Verified against orb.min.js 1.0.2 (`_render`, `getSimulationPosition`,
    // `translateOriginToCenter`).
    private const string FindNodePointScript = """
        () => {
            const v = window.__orbTestView;
            const n = v.data.getNodes()[0];
            const c = n.getCenter();
            const r = v._renderer;
            const t = r.transform;
            const x = t.x + t.k * (c.x + r.width / 2);
            const y = t.y + t.k * (c.y + r.height / 2);
            return JSON.stringify({ x, y });
        }
        """;

    /// <summary>Waits for the graph to be on screen and for its layout to stop moving.</summary>
    public async Task WaitForGraphAsync()
    {
        // WebAssembly has to download and start the runtime before it renders anything, which
        // is far slower than a Server circuit -- especially for the first page to hit it, and
        // slower again against a published build being served cold.
        await page.WaitForFunctionAsync(
            "() => !!document.querySelector('.orb-graph canvas')",
            null,
            new PageWaitForFunctionOptions { Timeout = 60_000 });

        // Let the force simulation settle so pixel and position reads are stable.
        await page.WaitForTimeoutAsync(2000);
    }

    public Task<int> CountPaintedPixelsAsync()
        => page.EvaluateAsync<int>(CountPaintedPixelsScript);

    public Task<int> CountNodesAsync()
        => page.EvaluateAsync<int>("() => window.__orbTestView.data.getNodeCount()");

    public Task<int> CountEdgesAsync()
        => page.EvaluateAsync<int>("() => window.__orbTestView.data.getEdgeCount()");

    public Task WaitForNodeCountAsync(int count)
        => page.WaitForFunctionAsync(
            $"() => window.__orbTestView.data.getNodeCount() === {count}");

    public Task<double[]> ReadRadiiAsync()
        => ReadDoublesAsync(
            "() => JSON.stringify(window.__orbTestView.data.getNodes().map(n => n.getRadius()))");

    public Task<double[]> ReadEdgeWidthsAsync()
        => ReadDoublesAsync(
            "() => JSON.stringify(window.__orbTestView.data.getEdges().map(e => e.getWidth()))");

    public Task<string?[]> ReadLabelsAsync()
        => page.EvaluateAsync<string?[]>(
            "() => window.__orbTestView.data.getNodes().map(n => n.getLabel() ?? null)");

    /// <summary>The view's live settings, as Orb reports them.</summary>
    public async Task<JsonElement> ReadSettingsAsync()
    {
        var json = await page.EvaluateAsync<string>(
            "() => JSON.stringify(window.__orbTestView.getSettings())");

        return JsonDocument.Parse(json).RootElement.Clone();
    }

    public async Task ClickFirstNodeAsync()
    {
        var box = await page.Locator(".orb-graph canvas").BoundingBoxAsync();
        var position = await FindNodePointAsync();

        await page.Mouse.ClickAsync(box!.X + position.X, box.Y + position.Y);
    }

    public async Task<(float X, float Y)> FindNodePointAsync()
    {
        var json = await page.EvaluateAsync<string>(FindNodePointScript);

        var point = JsonDocument.Parse(json).RootElement;
        return ((float)point.GetProperty("x").GetDouble(),
                (float)point.GetProperty("y").GetDouble());
    }

    public async Task<(double X, double Y)> ReadPositionAsync(string id)
    {
        var json = await page.EvaluateAsync<string>(ReadNodePositionsScript);

        using var doc = JsonDocument.Parse(json);

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (element.GetProperty("id").GetString() == id)
            {
                // A node can be present in the snapshot with only its "id" -- x/y are omitted
                // by JSON.stringify, not nulled, whenever Orb hasn't assigned the node a
                // position yet (e.g. it was just added/merged and the simulator hasn't ticked).
                // That is a different failure from "no such node" and callers that hit it need
                // to know to wait (see WaitForPositionAsync/NodeHasPositionAsync) rather than
                // see a bare KeyNotFoundException surfacing from deep inside System.Text.Json.
                if (!element.TryGetProperty("x", out var xProp)
                    || !element.TryGetProperty("y", out var yProp))
                {
                    throw new InvalidOperationException(
                        $"Node '{id}' exists in the position snapshot but has no x/y yet " +
                        "(a position has not been assigned to it). Call WaitForPositionAsync " +
                        "(or poll NodeHasPositionAsync) before reading a position that may not " +
                        "exist yet -- e.g. right after adding or merging a node.");
                }

                return (xProp.GetDouble(), yProp.GetDouble());
            }
        }

        throw new InvalidOperationException($"No node with id '{id}' in the position snapshot.");
    }

    // Orb's clearPosition() sets a node's x/y to undefined rather than removing the node, so
    // ReadPositionAsync's GetProperty("x") is the wrong tool to detect that -- JSON.stringify
    // omits an undefined property rather than nulling it, and GetProperty throws on a missing
    // one. This checks presence directly instead.
    public Task<bool> NodeHasPositionAsync(string id)
        => page.EvaluateAsync<bool>(HasPositionScript, id);

    /// <summary>
    /// Polls until <paramref name="id"/> has a numeric x/y in Orb's position snapshot, or throws
    /// once <paramref name="timeoutMs"/> elapses. A node's entry into a position -- whether from
    /// a seed, Orb's own fallback layout, or the force simulation -- is not guaranteed to have
    /// happened by the time an add/merge call returns (its completion only means the node
    /// exists, not that it has been placed), so a caller that wants to read a just-created
    /// node's position must wait for one explicitly instead of assuming it is already there.
    /// </summary>
    public async Task WaitForPositionAsync(string id, int timeoutMs = 5000)
    {
        try
        {
            await page.WaitForFunctionAsync(
                HasPositionScript,
                id,
                new PageWaitForFunctionOptions { Timeout = timeoutMs });
        }
        catch (PlaywrightException ex)
        {
            // Playwright .NET 1.62 has no dedicated timeout exception type -- a timed-out
            // WaitForFunctionAsync throws the same PlaywrightException as any other page-script
            // failure, just with a "Timeout ...ms exceeded" message. Re-throw as
            // System.TimeoutException (qualified: ImplicitUsings' global `using System;` plus
            // this file's `using Microsoft.Playwright;` makes a bare "TimeoutException"
            // ambiguous) so callers get a specific, useful failure naming the node and the
            // timeout instead of a generic Playwright error.
            throw new System.TimeoutException(
                $"Node '{id}' did not receive a position within {timeoutMs}ms.", ex);
        }
    }

    public static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private async Task<double[]> ReadDoublesAsync(string script)
        => JsonSerializer.Deserialize<double[]>(await page.EvaluateAsync<string>(script))!;
}
