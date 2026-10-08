# Official 3D Tiles validation harness

Node.js wrapper around the official CesiumGS [`3d-tiles-validator`](https://github.com/CesiumGS/3d-tiles-validator)
(Apache-2.0) used by the Obj2Tiles quality gate.

## Setup

```sh
npm ci            # pinned 3d-tiles-validator@0.6.1 + gltf-validator + node-stream-zip + ktx-parse
```

`3d-tiles-validator`, `gltf-validator@2.0.0-dev.3.10` (the older validator bundled
inside the official one, invoked directly for the content drill-down),
`node-stream-zip@1.16.0` (`.3tz` reading) and `ktx-parse@0.7.1` (KTX2 container
verification) are all direct dependencies of this harness. Node >= 22 is required.

The .NET tests call this automatically (`CliHarness.RunValidator`) and run
`npm ci` once on demand; CI does it as an explicit cached step. Set
`OBJ2TILES_SKIP_VALIDATOR=1` to skip official validation locally (the
structural inspector still runs) or `OBJ2TILES_SKIP_NPM_INSTALL=1` to forbid
automatic installs.

## Usage

```sh
node validate.cjs <tileset.json | output.3tz> [--allowlist allowlist.json] [--report report.json] [--json]
```

Exit codes: `0` valid, `1` quality-gate violation (any ERROR, or a WARNING that
is not allowlisted), `2` harness error.

## What it checks

1. Runs `Validators.validateTilesetFile` (tileset structure, transforms,
   contents; unpacks `.3tz` internally).
2. The tileset validator collapses every per-content finding into one
   `CONTENT_VALIDATION_WARNING`. The harness drills into each flagged `.b3dm`
   (unwrapping its embedded GLB), re-validating with the bundled
   `gltf-validator`, and reports each finding as `GLTF_<CODE>` so the
   allowlist can act at glTF-issue granularity. Content types without a GLB
   payload keep the collapsed warning.
3. The bundled `gltf-validator` (2.0.0-dev) predates KTX2 ratification: it
   rejects `image/ktx2` mimeTypes and cannot sniff KTX2 payloads. Those two
   blind-spot findings are **not** allowlisted. Each one is attributed to its
   image through the issue's JSON pointer (`/images/N`) and dropped only when
   that image is a KTX2 container `ktx-parse` reads successfully (dimensions,
   mip levels, data format descriptor). Any other image, e.g. a truncated
   JPEG/PNG, keeps its warning and fails the gate. Dropped findings are
   reported under `verifiedContentIssues` in the JSON report.

## Allowlist (`allowlist.json`)

JSON array; every entry must carry a `reason` (it is a reviewed decision, not
noise suppression). Optional `messageIncludes` restricts an entry to warnings
whose message contains a substring. Keep this list tiny: the KTX2 gaps of the
stale bundled validator are resolved by active verification (step 3 above),
not by allowlisting.

```json
[{ "type": "GLTF_GLB_EXTRA_DATA", "reason": "..." }]
```

Rejections (errors or unallowlisted warnings) print the offending issues;
`--report` writes the full machine-readable report the CI uploads as artifact.
