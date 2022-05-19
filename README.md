
# Obj2Tiles - Converts OBJ file to 3D Tiles format

![license](https://img.shields.io/github/license/HeDo88TH/Obj2Tiles) 
![commits](https://img.shields.io/github/commit-activity/m/HeDo88TH/Obj2Tiles) 
![languages](https://img.shields.io/github/languages/top/HeDo88TH/Obj2Tiles)
[![Build & Test](https://github.com/HeDo88TH/Obj2Tiles/actions/workflows/build-test.yml/badge.svg)](https://github.com/HeDo88TH/Obj2Tiles/actions/workflows/build-test.yml)

Obj2Tiles is a full fledged tool to convert OBJ files to 3D Tiles format. 
It creates multiple LODs, splits the mesh and repacks the textures.

## Command line parameters

```
-i, --input            Required. Input OBJ file.
-o, --output           Required. Output folder.

-s, --stage            (Default: Tiling) Stage to stop at (Decimation, Splitting, Tiling)

-l, --lods             (Default: 3) How many levels of details

-d, --divisions        (Default: 2) How many tiles divisions
-z, --zsplit           (Default: false) Splits along z-axis too
-k, --keeptextures     (Default: false) Keeps original textures

--lat                  Latitude of the mesh
--lon                  Longitude of the mesh
--alt                  Altitude of the mesh (meters)

--use-system-temp      (Default: false) Uses the system temp folder
--keep-intermediate    (Default: false) Keeps the intermediate files (do not cleanup)

--help                 Display this help screen.
--version              Display version information.
```

The pipeline is composed of the following steps:

### Decimation

The source obj is decimated using the `Fast Quadric Mesh Simplification` algorithm (by [Mattias Edlund](https://github.com/Whinarn)). 
The algorithm was ported from .NET Framework 3.5 to .NET Core 6.0. The original repo is [here](https://github.com/Whinarn/MeshDecimator). 

You can specify how many LODs (levels of detail) you want to generate using the `--lods` parameter. The decimation levels are generated using this formula:

`quality[i] = 1 - ((i + 1) / lods)`

For example: with 5 LODs the program will use the following quality levels: 80%, 60%, 40%, 20%.
If you specify 1 LOD, the decimation will be skipped.

### Splitting

For every decimated mesh, the program splits it recursively along x, y and z axis (optional using the `--zsplit` flag).
Every split is a new mesh with repacked textures (to save space), the [bin pack](https://github.com/juj/RectangleBinPack/) algorithm is by [Jukka Jylänki](https://github.com/juj).
If you want to preserve the original textures, use the `--keeptextures` flag (not recommended)

You can control how many times the split is performed by using the `--divisions` flag. The model will be split into `divisions^2` meshes (or `divisions^3` if `--zsplit` is used).

### 3D Tiles conversion

Each split mesh is converted to B3DM format using [ObjConvert](https://github.com/SilentWave/ObjConvert). 
Then the `tileset.json` is generated using the resulting files. You can specify the `--lat` and `--lon` and `--alt` parameters to set the location of the model.

## Running

Obj2Tiles is built using [.NET Core 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0). Releases are available on [GitHub](https://github.com/HeDo88TH/Obj2Tiles/releases) for a multitude of platforms (win / linux / mac).
You can download the [latest release](https://github.com/HeDo88TH/Obj2Tiles/releases/latest) or compile it yourself using the following commands:

```
git clone https://github.com/HeDo88TH/Obj2Tiles.git
cd Obj2Tiles
dotnet build -c Release
```

------------

## Examples

You can download the test obj file [here](https://github.com/DroneDB/test_data/raw/master/brighton/odm_texturing.zip). 
The Brighton Beach textured model generated using [OpenDroneMap](https://github.com/OpenDroneMap/ODM).

### Basic usage (using defaults)

It runs all the pipeline stages and generates the `tileset.json` file in the output folder.

```
Obj2Tiles -i model.obj -o output
```

### Decimation

Stop the pipeline at the decimation stage and generate 8 LODs

```
Obj2Tiles --stage Decimation --lods 8 -i model.obj -o output
```

### Splitting

Stop the pipeline at the splitting stage and generate 3 divisions per axis

```
Obj2Tiles --stage Splitting --divisions 3 -i model.obj -o output
```

### Full pipeline

Run all the pipeline stages and generate the `tileset.json` file in the output folder.

```
Obj2Tiles --lods 8 --divisions 3 --lat 40.689434025350025 --lon -74.0444987716782 --alt 120 -i model.obj -o output
```

## Remarks

All the pipeline stages are multi threaded to speed up the process.
You can stop the pipeline at any stage by providing the `--stage` flag.
If you need to keep the intermediate files, use the `--keep-intermediate` flag.
You can use the `--use-system-temp` flag to use the system temp folder instead of the output folder.

## Gallery

![split-brighton](https://user-images.githubusercontent.com/7868983/169304507-5ccd970d-9fd2-4d09-81a1-e7f701cb913a.png)

![z-split](https://user-images.githubusercontent.com/7868983/169304532-7b46712a-7bb7-4c2e-a799-12df6c227ee9.png)


