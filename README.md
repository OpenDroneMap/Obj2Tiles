
# Obj2Tiles - Converts OBJ file to 3D Tiles format

![license](https://img.shields.io/github/license/HeDo88TH/Obj2Tiles)
![commits](https://img.shields.io/github/commit-activity/m/HeDo88TH/Obj2Tiles)
![languages](https://img.shields.io/github/languages/top/HeDo88TH/Obj2Tiles)
[![Build & Test](https://github.com/OpenDroneMap/Obj2Tiles/actions/workflows/build-test.yml/badge.svg)](https://github.com/OpenDroneMap/Obj2Tiles/actions/workflows/build-test.yml)
[![Publish](https://github.com/OpenDroneMap/Obj2Tiles/actions/workflows/publish.yml/badge.svg)](https://github.com/OpenDroneMap/Obj2Tiles/actions/workflows/publish.yml)

Obj2Tiles is a fully-featured tool to convert OBJ files to 3D Tiles format.
It runs a three-stage pipeline: **Decimation** → **Splitting** → **Tiling**, creating multiple LODs, splitting the mesh into spatial tiles, and repacking textures.

### Vertex Colors

Obj2Tiles supports OBJ files with per-vertex colors (extended vertex format: `v x y z r g b`). Vertex colors are:
- **Parsed** from OBJ files by all three internal parsers (splitting, decimation, and glTF conversion stages)
- **Preserved** through mesh splitting (with correct interpolation at edge intersections) and LOD decimation
- **Exported** to glTF/GLB as `COLOR_0` attribute with automatic sRGB → linear RGB conversion per the glTF specification

## Installation

You can download precompiled binaries for Windows, Linux and macOS from https://github.com/OpenDroneMap/Obj2Tiles/releases.

## Usage

```
Obj2Tiles [options] <input.obj> <output-folder>
```

## Command line parameters

### Input / Output

| Parameter | Default | Description | Example |
|-----------|---------|-------------|---------|
| `Input` (pos. 0) |  | Input OBJ file (required) | `model.obj` |
| `Output` (pos. 1) |  | Output folder (required) | `./tileset-output` |

### Pipeline Control

| Parameter | Default | Description | Example |
|-----------|---------|-------------|---------|
| `-s, --stage` | `Tiling` | Stage to stop at: `Decimation`, `Splitting`, or `Tiling` | `--stage Splitting` |
| `-l, --lods` | `3` | Number of levels of detail to generate | `--lods 5` |

### Splitting

| Parameter | Default | Description | Example |
|-----------|---------|-------------|---------|
| `-d, --divisions` | `2` | Number of tile divisions per axis. The model is split into `divisions^2` tiles (or `divisions^3` with `--zsplit`) | `--divisions 3` |
| `-z, --zsplit` | `false` | Also split along the Z-axis (not just X and Y) | `--zsplit` |
| `-g, --split-strategy` | `VertexBaricenter` | How the split point is computed: `AbsoluteCenter` (bounding box center), `VertexBaricenter` (vertex average), or `VertexMedian` (vertex median, most balanced) | `--split-strategy VertexMedian` |
| `-k, --keeptextures` | `false` | Keep original textures instead of repacking them (not recommended) | `--keeptextures` |
| `--octree` | `false` | Use octree spatial subdivision: each LOD gets one additional division level, producing a proper parent-child tile hierarchy instead of per-tile LOD chains. Combine with `--zsplit` for a true 8-way octree | `--octree --zsplit` |
| `--lod-texture-scale` | `1.0` | Per-LOD texture downscale factor. LOD-0 always keeps full resolution; each subsequent LOD multiplies the previous atlas resolution by this factor. E.g. `0.5` gives LOD-1 at half resolution, LOD-2 at quarter, etc. Uses bicubic resampling; LOD-0 is PNG, coarser LODs are JPEG at quality 75 | `--lod-texture-scale 0.5` |

### Geo-referencing

| Parameter | Default | Description | Example |
|-----------|---------|-------------|---------|
| `--lat` |  | Latitude in WGS84 decimal degrees | `--lat 45.4642` |
| `--lon` |  | Longitude in WGS84 decimal degrees | `--lon 9.1903` |
| `--alt` | `0` | Altitude in meters above the WGS84 ellipsoid | `--alt 120` |
| `--scale` | `1` | Scale factor for local geometry (e.g. `1200.0/3937.0` for survey feet). Does NOT affect altitude or ECEF position | `--scale 0.3048` |
| `--local` | `false` | Local mode: no ECEF geo-referencing, uses an identity matrix. Use when you don't need globe placement | `--local` |
| `--y-up-to-z-up` | `false` | Apply a 90° rotation around the X-axis to convert Y-up OBJ files to Z-up (3D Tiles convention) | `--y-up-to-z-up` |

### Other

| Parameter | Default | Description | Example |
|-----------|---------|-------------|---------|
| `-e, --error` | `100` | Base geometric error value for the root tile in `tileset.json` | `--error 500` |
| `--use-system-temp` | `false` | Use the system temp folder for intermediate files instead of the output folder | `--use-system-temp` |
| `--keep-intermediate` | `false` | Keep intermediate files (decimated OBJs, split tiles) for debugging | `--keep-intermediate` |
| `--help` |  | Display help screen | `--help` |
| `--version` |  | Display version information | `--version` |

## Pipeline Stages

### 1. Decimation

The source OBJ is decimated using the **Fast Quadric Mesh Simplification** algorithm by [Mattias Edlund](https://github.com/Whinarn) (ported from .NET Framework 3.5 to .NET Core; original repo [here](https://github.com/Whinarn/MeshDecimator)).

The number of LODs is controlled by `--lods`. Decimation quality levels follow this formula:

```
quality[i] = 1 - ((i + 1) / lods)
```

For example, with 5 LODs the quality levels are: LOD-0 is the original (100%), followed by 80%, 60%, 40%, 20%.
If you specify 1 LOD, decimation is skipped entirely.

### 2. Splitting

For every decimated mesh, the program splits it recursively along the X and Y axes (and optionally Z with `--zsplit`). Each split produces a new mesh with repacked textures using the [MaxRects bin packing](https://github.com/juj/RectangleBinPack/) algorithm by [Jukka Jylänki](https://github.com/juj).

**Split strategies** (`--split-strategy`):

- **`VertexBaricenter`** (default): split point is the barycenter of the sub-mesh vertices. Adapts to geometry concentration, producing balanced tiles.
- **`AbsoluteCenter`**: split point is the bounding box center. Produces a spatially uniform grid but may yield uneven tiles for non-uniform geometry.
- **`VertexMedian`**: split point is the vertex median. Most balanced of all strategies - robust to outliers and skewed distributions. Uses a *pre-computed split plan* from LOD-0 vertices so all LODs share the same split points without redundant computation.

**Octree mode** (`--octree`):

By default, every LOD produces the same number of tiles arranged as per-tile chains in `tileset.json`. With `--octree`, each LOD receives one additional division level compared to the next coarser LOD:

| LOD | Divisions (`--divisions 2`, 3 LODs) | Tiles (XY) |
|-----|-------------------------------------|------------|
| 0 (finest) | 4 | 16 |
| 1 | 3 | 9 |
| 2 (coarsest) | 2 | 4 |

Coarser tiles become spatial parents of finer ones in `tileset.json`, producing a proper tree hierarchy. Combine `--octree` with `--zsplit` for a true 8-way octree.

**Texture downscaling** (`--lod-texture-scale`):

Each tile's texture atlas is repacked from the portion of the source texture within that tile. Use `--lod-texture-scale` to reduce atlas resolution for coarser LODs:

| LOD | Scale (`--lod-texture-scale 0.5`) | Example atlas (4096×4096 source, 16 tiles) |
|-----|------------------------------------|--------------------------------------------|
| 0 (finest) | 1.0 (always full) | 1024×1024 (original format) |
| 1 | 0.5 | 512×512 JPEG |
| 2 | 0.25 | 256×256 JPEG |

Downscaling uses ImageSharp's default resampler. LOD-0 preserves the original texture format; coarser LODs are JPEG at quality 75.

### 3. Tiling

Each split mesh is converted to B3DM (3D Tiles) format via an OBJ → glTF → GLB → B3DM conversion pipeline. Then `tileset.json` is generated with bounding volumes, geometric errors, and the ECEF transform matrix.

**Coordinate system & geo-referencing:**

The tiling stage places the model on the globe using an **ECEF** (Earth-Centered, Earth-Fixed) transformation matrix in `tileset.json`.

| Flags | Behavior |
|-------|----------|
| `--lat 45 --lon 9 --alt 100` | Full ECEF transform at the given WGS84 coordinates |
| *(no lat/lon)* | Falls back to default coordinates (Duomo di Milano, 45.46°N 9.19°E) |
| `--local` | Identity matrix - no geo-referencing. Use for local viewers |

**Important notes:**
- `--scale` only affects local geometry size, **not** altitude or ECEF position. `--scale 100 --alt 17` places the model at 17 meters, not 1700.
- OBJ files typically use Y-up. Bounding volumes in `tileset.json` perform a Y↔Z swap internally (3D Tiles uses Z-up). If the model appears flipped, try `--y-up-to-z-up` for an additional 90° X-axis rotation.
- `--local` takes precedence over `--lat`/`--lon` (a warning is printed if both are specified).

## Examples

You can download a test OBJ file [here](https://github.com/DroneDB/test_data/raw/master/brighton/odm_texturing.zip)
(Brighton Beach textured model generated with [OpenDroneMap](https://github.com/OpenDroneMap/ODM)).

### Basic usage (defaults)

Run all pipeline stages and generate `tileset.json` in the output folder:

```bash
Obj2Tiles model.obj ./output
```

### Octree with texture downscaling (recommended for large models)

Produce a proper octree hierarchy with textures halved at each LOD step:

```bash
Obj2Tiles --octree --zsplit --lods 3 --divisions 2 --lod-texture-scale 0.5 --local model.obj ./output
```

### Geo-referenced model

Place the model at specific GPS coordinates with 8 LODs and 3 divisions:

```bash
Obj2Tiles --lods 8 --divisions 3 --lat 40.6894 --lon -74.0445 --alt 120 model.obj ./output
```

### Stop at decimation stage

Generate 8 decimated LODs without splitting or tiling:

```bash
Obj2Tiles --stage Decimation --lods 8 model.obj ./output
```

### Stop at splitting stage

Generate split tiles with 3 divisions per axis:

```bash
Obj2Tiles --stage Splitting --divisions 3 model.obj ./output
```

### Local mode (no geo-referencing)

For local 3D viewers that don't need globe placement:

```bash
Obj2Tiles --local model.obj ./output
```

### Balanced splitting with VertexMedian

Use the median-based split strategy for the most balanced tiles:

```bash
Obj2Tiles --split-strategy VertexMedian --lods 4 --divisions 2 --local model.obj ./output
```

### Survey feet to meters

Scale geometry from survey feet to meters:

```bash
Obj2Tiles --scale 0.3048 --lat 45.0 --lon 9.0 --alt 0 model.obj ./output
```

## Running

Obj2Tiles is built using [.NET 10.0](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). Binary releases are available on [GitHub](https://github.com/OpenDroneMap/Obj2Tiles/releases) for Windows, Linux, and macOS.

Download the [latest release](https://github.com/OpenDroneMap/Obj2Tiles/releases/latest) or compile from source:

```bash
git clone https://github.com/OpenDroneMap/Obj2Tiles.git
cd Obj2Tiles
dotnet build -c Release
```

### Docker

A Docker image is available for Linux (x64 and arm64) with a multi-stage build and trimming for minimal runtime footprint:

```bash
docker run --rm -v $(pwd):/data ghcr.io/opendronemap/obj2tiles model.obj /data/output
```

Or build locally:

```bash
docker build -t obj2tiles .
docker run --rm -v $(pwd):/data obj2tiles model.obj /data/output
```

## Rotating the model

After generating `tileset.json`, you can edit the 4x4 Transform matrix to add translation, rotation, and scaling. This is the matrix structure:

![TransformationMatrix1](https://user-images.githubusercontent.com/7868983/169370131-18575153-4023-4a82-8ffd-3b5e2476dce2.png)

The tiling stage uses this matrix to place the model at the requested geo location:

![Translation-Matrix1](https://user-images.githubusercontent.com/7868983/169369875-3e337eb2-4168-4b43-b9dc-fef2cf6aecb0.png)

You can add scaling:

![Scaling-Matrix1](https://user-images.githubusercontent.com/7868983/169370506-16878adf-ce0c-4ba7-a107-5315693b80d8.png)

Or rotation around any of the 3 axes:

![RotationX-Matrix1](https://user-images.githubusercontent.com/7868983/169370741-9ba79f00-90cf-429a-b5b4-26c8d3d3e355.png)

![RotationY-Matrix1](https://user-images.githubusercontent.com/7868983/169370750-6cb3b744-e2fb-4606-912a-49e4a03905ae.png)

![RotationZ-Matrix1](https://user-images.githubusercontent.com/7868983/169370755-03f016ca-ca8c-461d-a6e9-8643885cd624.png)

By combining these matrices, you can rotate, scale, and translate the model.
More details on [BrainVoyager](https://www.brainvoyager.com/bv/doc/UsersGuide/CoordsAndTransforms/SpatialTransformationMatrices.html).

## OBJ Format Support

The OBJ parser handles the following format features:

- **Vertex formats**: `v x y z` and `v x y z r g b` (vertex colors)
- **Face formats**: `f v/vt/vn`, `f v//vn`, `f v/vt`, and `f v` (geometry only)
- **Quads and n-gons**: Automatically triangulated using fan triangulation (Issue #60)
- **Line elements**: Gracefully skipped (Issue #64)
- **Scientific notation**: Coordinates like `1.5e-3` are parsed correctly
- **UV wrapping**: Texture coordinates outside [0,1] are wrapped for UDIM/mirroring workflows (Issue #35)
- **MTL options**: Full support for `-bm`, `-blendu`, `-blendv`, `-boost`, `-cc`, `-clamp`, `-imfchan`, `-mm`, `-texres`, `-type`, `-o`, `-s`, `-t` and other material map options
- **Path resolution**: Textures are resolved by progressively relaxing the base directory (MTL folder, OBJ folder, absolute path)

## Remarks

- All pipeline stages are multi-threaded for performance. Tile writing runs in parallel.
- Stop the pipeline at any stage with the `--stage` flag.
- Keep intermediate files with `--keep-intermediate` for debugging.
- Use `--use-system-temp` to store intermediate files in the system temp folder.

## Gallery

![cesium](https://user-images.githubusercontent.com/7868983/170308702-14b32953-c3fd-4eb5-8b86-b40688dc354e.png)

![split-brighton](https://user-images.githubusercontent.com/7868983/169304507-5ccd970d-9fd2-4d09-81a1-e7f701cb913a.png)

![z-split](https://user-images.githubusercontent.com/7868983/169304532-7b46712a-7bb7-4c2e-a799-12df6c227ee9.png)

