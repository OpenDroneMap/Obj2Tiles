# Copilot Instructions — Obj2Tiles

## Project Overview

Obj2Tiles is a .NET 10 CLI tool that converts OBJ files to [3D Tiles](https://www.ogc.org/standard/3dtiles/) format. It runs a three-stage pipeline: **Decimation → Splitting → Tiling**. The user can stop at any stage via `--stage`.

## Architecture

```
CLI (Program.cs + Options.cs)  ← CommandLineParser
 └─ StagesFacade (partial static class, one file per stage)
     ├─ DecimationStage  → MeshDecimatorCore (Fast Quadric Mesh Simplification)
     ├─ SplitStage       → Obj2Tiles.Library (IMesh hierarchy, recursive axis splits)
     └─ TilingStage      → Utils.ConvertB3dm: OBJ → glTF → GLB → B3DM
                            ├─ Obj2Gltf/Converter (OBJ→glTF)
                            ├─ Obj2Gltf/Gltf2GlbConverter (glTF→GLB)
                            └─ Obj2Tiles/Tiles/B3dm (GLB→B3DM wrapper)
```

**Key design decision**: Two independent OBJ parsers exist — `Stages/Model/ObjMesh` (used only by decimation to interface with `MeshDecimatorCore`) and `Obj2Tiles.Library/Geometry/MeshUtils.LoadMesh` (used by splitting, returns `IMesh`). Do not mix them.

### Project Structure

| Project | Purpose |
|---------|---------|
| `Obj2Tiles/` | CLI entry point, pipeline orchestration, B3DM format, `Utils.ConvertB3dm` |
| `Obj2Tiles.Library/` | Core geometry types (`IMesh`, `Mesh`, `MeshT`), recursive mesh splitting, texture repacking via `MaxRectanglesBinPack`, materials |
| `Obj2Gltf/` | OBJ→glTF→GLB conversion (namespace `SilentWave.Obj2Gltf`) |
| `MeshDecimatorCore/` | Mesh decimation algorithm (ported from Whinarn/MeshDecimator) |
| `Obj2Tiles.Common/` | Shared utilities: HTTP downloads, temp file/folder management, test helpers (`TestFS`, `TestArea`) |

### Mesh Type Hierarchy

```
IMesh (Obj2Tiles.Library/Geometry/IMesh.cs)
├── Mesh  — geometry only (vertices + Face)
└── MeshT — geometry + textures (vertices + texture coords + FaceT + Material[])
```

`MeshT.WriteObj` handles texture repacking using `MaxRectanglesBinPack` and `SixLabors.ImageSharp`. The `Split` method on both types cuts along an axis, classifying triangles relative to a threshold and computing edge intersections via `CutEdge`/`CutEdgePerc`.

## Build & Run

```bash
dotnet build                                    # build entire solution
dotnet build Obj2Tiles/Obj2Tiles.csproj         # build CLI only
dotnet test                                     # run all tests
dotnet run --project Obj2Tiles -- model.obj ./output  # run CLI
```

Target framework is **net10.0** with `InvariantGlobalization=true` and nullable enabled.

## Testing

- **Framework**: NUnit 4.x with `NUnit3TestAdapter`
- **Assertions**: [Shouldly](https://github.com/shouldly/shouldly) (e.g., `value.ShouldBe(expected)`)
- **Test projects**: `Obj2Tiles.Library.Test/` (geometry/mesh tests) and `Obj2Tiles.Test/` (pipeline stage smoke tests)
- **Test data**: Static files in `TestData/` directories (copied via `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`). Remote datasets downloaded via `TestFS` (from `Obj2Tiles.Common`) with built-in caching.
- **Output convention**: Tests create `TestOutput/{testName}` folders, cleared before each run.
- **`[Explicit]`** attribute marks tests requiring large/local datasets not in the repo.

## Conventions & Patterns

- **Partial static class `StagesFacade`**: Each pipeline stage is a separate file (`DecimationStage.cs`, `SplitStage.cs`, `TilingStage.cs`) contributing to the same static class. Follow this pattern when adding stages.
- **Coordinate swap**: Y↔Z swap occurs in `Utils.ToBoundingVolume` and `GpsCoords.ToEcefTransform`. 3D Tiles uses Z-up; OBJ files may be Y-up.
- **JSON serialization**: `Newtonsoft.Json` for tileset/glTF models; `System.Text.Json` attributes on geometry DTOs (`Vertex3`, `Box3`). Do not unify — each has different requirements.
- **Image processing**: All image operations use `SixLabors.ImageSharp` (`Image<Rgba32>`). Texture loading is cached via `TexturesCache` (thread-safe `ConcurrentDictionary`).
- **Async parallelism**: LOD decimation runs in parallel via `Task.WhenAll`. Recursive mesh splitting uses `ConcurrentBag<IMesh>`. Split operations per-LOD also run in parallel.
- **`FormattingStreamWriter`**: Custom `StreamWriter` subclass (in `Common.cs`) that forces `InvariantCulture` — always use it when writing OBJ files to ensure decimal separator consistency.
- **Extension method `AddIndex<T>`** (in `Extenders.cs`): Adds an element to a collection/dictionary and returns its index. Used extensively during vertex merging in split operations.

## CI/CD

- **Build & Test** (`.github/workflows/build-test.yml`): Runs on push/PR to `master` — restore → build → test on `ubuntu-latest` with .NET 9.0.x.
- **Publish** (`.github/workflows/publish.yml`): Triggered by `v*` tags. Publishes self-contained binaries for `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64` via `.pubxml` profiles, zips them, and creates a GitHub Release.
