# Live demo on GitHub Pages — design

## Problem

The library has API documentation and a README, but nothing a visitor can *look* at. The
existing sample host needs a server process, so it cannot be linked from anywhere public.

## Constraint that shapes everything

GitHub Pages is static hosting. Blazor Server needs a host process and a SignalR circuit, so
the demo must be **standalone WebAssembly**, which publishes to plain files.

## Design

### New project

`samples/Pinknose.Memgraph.Orb.Razor.Demo`, standalone Blazor WebAssembly, referencing the
library by project reference so the demo fails to compile when the API changes rather than
failing silently in a browser.

**No `Router`.** The demo is one page. Routing on Pages requires a `404.html` copy of
`index.html` to make deep links work, which is a workaround for a problem the demo does not
have.

### Three Pages-specific hazards

1. **`.nojekyll`** must sit at the artifact root. Pages otherwise runs Jekyll, which skips
   directories starting with `_`, so `_framework/` 404s and Blazor never starts. This is the
   usual reason a Blazor-on-Pages deploy shows a blank page.
2. **Base href.** The demo is served from a sub-path, `/Pinknose.Memgraph.Orb.Razor/demo/`.
   Source keeps `<base href="/">` so local `dotnet run` works; the workflow rewrites it during
   publish. Getting this wrong produces a blank page and 404s, not a build failure.
3. **No compression negotiation.** Pages does not serve the `.br`/`.gz` variants Blazor
   publishes, so browsers download uncompressed assemblies. Works, just heavier.

### Layout of the deployed site

One Pages site, one artifact:

| Path | Content |
| --- | --- |
| `/` | DocFX API documentation |
| `/demo/` | This demo |

Docs stay at the root deliberately: the README **inside the published NuGet package** links
there, and a published package cannot be changed.

### What the demo shows

A ~40-node dependency graph of a plausible .NET solution — projects and packages, coloured by
kind, sized by dependents. Chosen because it is legible at a glance to the audience the library
is for, and because it produces a readable force layout rather than a hairball.

Controls, each chosen to exercise real API surface:

- Layout switcher across all four layouts (`OrbForceLayout`, `OrbGridLayout`,
  `OrbCircularLayout`, `OrbHierarchicalLayout`)
- Live setting toggles: labels, shadows, drag, zoom
- Recenter and zoom via the imperative methods
- An event log showing clicks and hovers carrying the caller's own domain instances
- SVG export through `GetSvgAsync`, which nothing else demonstrates

### Tests

Three Playwright smoke tests against a local dev build: the canvas renders, switching layout
moves nodes, clicking a node appends to the event log. Enough to catch the demo rotting after
an API change without doubling browser-suite time. Not run against the deployed site.

## Risks

The `docs` workflow has only ever deployed DocFX output. The first deploy after this change is
what proves the combined artifact works, and a wrong base href fails as a blank page rather
than a red build.
