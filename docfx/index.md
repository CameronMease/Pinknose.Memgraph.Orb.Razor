---
_layout: landing
---

# Pinknose.Memgraph.Orb.Razor

A Blazor component library wrapping [`@memgraph/orb`](https://github.com/memgraph/orb) 1.0.2,
the force-directed graph visualization library from Memgraph. It gives you a typed `<OrbGraph>`
component driven by your own domain types instead of hand-written JS interop.

Start with [`OrbGraph<TNode, TEdge>`](xref:Pinknose.Memgraph.Orb.Razor.OrbGraph`2) — the
component itself — then [`IOrbNode`](xref:Pinknose.Memgraph.Orb.Razor.IOrbNode) and
[`IOrbEdge`](xref:Pinknose.Memgraph.Orb.Razor.IOrbEdge), which you implement on your own
types.

For usage guidance, the type-argument trap, and how to run the samples and tests, see the
[README](https://github.com/CameronMease/Pinknose.Memgraph.Orb.Razor#readme).
