# Contributing

Everything needed to work *on* the library. For using it, see the
[README](README.md); for the API itself, see the
[documentation site](https://cameronmease.github.io/Pinknose.Memgraph.Orb.Razor/).

```bash
dotnet build
```

## Sample host

`samples/Pinknose.Memgraph.Orb.Razor.SampleHost` serves two pages:

| Route | Render mode |
| --- | --- |
| `/orb-server` | `InteractiveServer` |
| `/orb-wasm` | `InteractiveWebAssembly` (component lives in the `.Client` project) |

Both render the same `OrbDemoView` component and are deliberately kept to nothing but
`<OrbDemoView />`, so the only difference between them is the render mode — which is what makes
"passes on one, fails on the other" a meaningful signal. A unit test enforces that. Add demo
features to `OrbDemoView`, never to a page. Both pages take a `?styling=` parameter —
`full` (the default), `labels` for labels with no styles, or `none` for neither — and a button
that cycles the same three modes.

```bash
dotnet run --project samples/Pinknose.Memgraph.Orb.Razor.SampleHost
```

## Running the tests

```bash
dotnet test tests/Pinknose.Memgraph.Orb.Razor.Tests
```

```bash
dotnet test tests/Pinknose.Memgraph.Orb.Razor.BrowserTests
```

The browser suite starts the sample host itself and drives both routes with Playwright.

A third suite covers a **trimmed** WebAssembly publish — the case a `dotnet run` build cannot
reach, since it never trims. It is opt-in because it wipes the Release artifacts and publishes
from scratch (forcing ILLink to actually run, without which its warnings would be cached away
and the suite would report a cleaner library than it has). Set `ORB_TRIM_TESTS=1` to run it;
without that its tests report inconclusive and nothing is published:

```bash
ORB_TRIM_TESTS=1 dotnet test tests/Pinknose.Memgraph.Orb.Razor.TrimmedPublishTests
```

CI runs the first two suites on every push and pull request. The trimmed suite runs nightly and
on demand (Actions → trimmed-publish → Run workflow), since each run publishes from scratch.

## API documentation

The public API is documented with XML comments, which ship in the package (so consumers get
IntelliSense) and are rendered into a browsable site by [DocFX](https://dotnet.github.io/docfx/):

```bash
dotnet tool restore
```

```bash
dotnet docfx docfx/docfx.json --serve
```

DocFX reads the **compiled assembly** rather than the source, which matters for a Razor class
library: the component's parameters, events and methods live in `OrbGraph.razor`, and DocFX's
source-based metadata step does not compile `.razor` files — pointed at the project it produced
a page for `OrbGraph` with no members at all. So `docfx/docfx.json` points at
`bin/Release/net10.0`, and the library must be built in Release first. CI builds the site on
every push with warnings as errors, so a broken cross-reference fails the build, and the
`docs` workflow publishes it to
[GitHub Pages](https://cameronmease.github.io/Pinknose.Memgraph.Orb.Razor/) on every push to
`master`.

## Changing the public API

The public surface is recorded in `Pinknose.Memgraph.Orb.Razor/PublicAPI.Shipped.txt` and
`PublicAPI.Unshipped.txt`, and `Microsoft.CodeAnalysis.PublicApiAnalyzers` **fails the build**
when the code and those files disagree. Adding a public member is an error until you record it;
so is deleting one that is still listed.

The error message contains the exact line to add, in the form
`Namespace.Type.Member.get -> string!`. Put new entries in `PublicAPI.Unshipped.txt`; when you
cut a release, move them into `PublicAPI.Shipped.txt`. Everything is currently unshipped,
because nothing has been released yet.

This exists because a published NuGet version can never be replaced. Without it, a rename or a
signature change reaches a tag as easily as any other edit.

## Releasing

The version is not stored in a file. [MinVer](https://github.com/adamralph/minver) derives it
from the nearest `v`-prefixed git tag, so tagging is the whole release action:

```bash
git tag v0.1.0
```

```bash
git push origin v0.1.0
```

That triggers the `release` workflow, which runs the unit and packaging tests, packs, refuses
to continue if the packed version does not match the tag, uploads the packages as run
artifacts, and opens a GitHub Release with generated notes.

**Publishing to NuGet.org is currently disabled** — the upload step is commented out in
`.github/workflows/release.yml` while the library is pre-release, so tagging exercises the
whole chain without pushing anything public. Enabling it means uncommenting that step and
adding a `NUGET_API_KEY` repository secret.

Commits that are not on a tag build as `0.1.0-alpha.N`, so nothing can accidentally pack as a
release. Versioning is independent of the vendored Orb version — bumping Orb is at minimum a
minor release here, since it changes rendering defaults and the bundle's integrity hash.
