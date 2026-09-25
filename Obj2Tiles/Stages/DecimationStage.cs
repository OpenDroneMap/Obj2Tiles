using System.Diagnostics;
using MeshDecimatorCore;
using MeshDecimatorCore.Algorithms;
using MeshDecimatorCore.Math;
using Obj2Tiles.Stages.Model;
using Mesh = MeshDecimatorCore.Mesh;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{
    public static async Task<DecimateResult> Decimate(string sourcePath, string destPath, int lods,
        DecimationMode mode = DecimationMode.Standard, bool ignoreNormalMaps = false)
    {

        var qualities = Enumerable.Range(0, lods - 1).Select(i => 1.0f - ((i + 1) / (float)lods)).ToArray();

        var sourceObjMesh = new ObjMesh();
        sourceObjMesh.ReadFile(sourcePath);
        var bounds = sourceObjMesh.Bounds;

        var fileName = Path.GetFileName(sourcePath);
        var originalSourceFile = Path.Combine(destPath, fileName);
        File.Copy(sourcePath, originalSourceFile, true);

        var destFiles = new List<string> { originalSourceFile };

        var tasks = new List<Task>();

        for (var index = 0; index < qualities.Length; index++)
        {
            var quality = qualities[index];
            var destFile = Path.Combine(destPath, Path.GetFileNameWithoutExtension(sourcePath) + "_" + index + ".obj");

            if (File.Exists(destFile))
                File.Delete(destFile);

            Console.WriteLine(" -> Decimating mesh {0} with quality {1:0.00}", fileName, quality);

            tasks.Add(Task.Run(() => InternalDecimate(sourceObjMesh, destFile, quality, mode)));

            destFiles.Add(destFile);
        }

        await Task.WhenAll(tasks);
        Console.WriteLine(" ?> Decimation done");

        Console.WriteLine(" -> Copying obj dependencies");
        Utils.CopyObjDependencies(sourcePath, destPath, ignoreNormalMaps);
        Console.WriteLine(" ?> Dependencies copied");

        return new DecimateResult { DestFiles = destFiles.ToArray(), Bounds = bounds };

    }


    private static void InternalDecimate(ObjMesh sourceObjMesh, string destPath, float quality, DecimationMode mode)
    {
        quality = MathHelper.Clamp01(quality);
        var sourceVertices = sourceObjMesh.Vertices;
        var sourceNormals = sourceObjMesh.Normals;
        var sourceTexCoords2D = sourceObjMesh.TexCoords2D;
        var sourceTexCoords3D = sourceObjMesh.TexCoords3D;
        var sourceSubMeshIndices = sourceObjMesh.SubMeshIndices;
        var hasTextures = sourceTexCoords2D != null || sourceTexCoords3D != null;

        var sourceMesh = new Mesh(sourceVertices!, sourceSubMeshIndices!)
        {
            Normals = sourceNormals,
            Colors = sourceObjMesh.VertexColors
        };

        if (sourceTexCoords2D != null)
        {
            sourceMesh.SetUVs(0, sourceTexCoords2D);
        }
        else if (sourceTexCoords3D != null)
        {
            sourceMesh.SetUVs(0, sourceTexCoords3D);
        }

        var currentTriangleCount = sourceSubMeshIndices!.Sum(t => t.Length / 3);

        var targetTriangleCount = (int)Math.Ceiling(currentTriangleCount * quality);
        Console.WriteLine(" ?> Input: {0} vertices, {1} triangles (target {2})",
            sourceVertices!.Length, currentTriangleCount, targetTriangleCount);

        var stopwatch = new Stopwatch();
        stopwatch.Reset();
        stopwatch.Start();

        bool enableSmartLink;
        bool preserveUVSeamEdges;
        bool preserveUVFoldoverEdges;
        bool preserveBorderEdges;
        double aggressiveness;
        int maxIterations;

        switch (mode)
        {
            case DecimationMode.Aggressive:
                enableSmartLink = true;
                preserveUVSeamEdges = false;
                preserveUVFoldoverEdges = false;
                preserveBorderEdges = quality > 0.2f;
                aggressiveness = 7.0;
                maxIterations = 100;
                break;
            case DecimationMode.Quality:
                // Note: this is substantially equivalent to
                // enableSmartLink = true, preserveUVSeamEdges = true, preserveUVFoldoverEdges = true
                // ... but faster.
                enableSmartLink = false;
                preserveUVSeamEdges = false;
                preserveUVFoldoverEdges = false;
                preserveBorderEdges = true;
                // Less aggressive, more steps - slower but possibly slightly better
                aggressiveness = 5.0;
                maxIterations = 300;
                break;
            case DecimationMode.Standard:
            default:
                enableSmartLink = true;
                // With textures we should almost always preserve UV-seams and UV-foldovers
                // as not doing so will generate visible distortion.
                // Without textures the distortion effect is only related to normals
                // and is usually milder, so we can tolerate it when quality is low.
                preserveUVSeamEdges = hasTextures || quality > 0.5f;
                preserveUVFoldoverEdges = hasTextures || quality > 0.5f;
                preserveBorderEdges = quality > 0.2f;
                aggressiveness = 7.0;
                maxIterations = 100;
                break;
        }

        var algorithm = new FastQuadricMeshSimplification
        {
            Verbose = true,
            Options = new SimplificationOptions
            {
                EnableSmartLink = enableSmartLink,
                PreserveUVSeamEdges = preserveUVSeamEdges,
                PreserveBorderEdges = preserveBorderEdges,
                PreserveUVFoldoverEdges = preserveUVFoldoverEdges,
                PreserveSurfaceCurvature = true,
                Aggressiveness = aggressiveness,
                MaxIterationCount = maxIterations,
                // double.Epsilon is the smallest representable positive double (~4.9e-324), not a
                // usable welding tolerance - this is the double-precision machine epsilon (C/C++
                // DBL_EPSILON), matching SimplificationOptions.Default.
                VertexLinkDistance = 2.2204460492503131E-16
            }
        };

        var destMesh = MeshDecimation.DecimateMesh(algorithm, sourceMesh, targetTriangleCount);
        stopwatch.Stop();

        var destVertices = destMesh.Vertices;
        var destNormals = destMesh.Normals;
        var destIndices = destMesh.GetSubMeshIndices();

        var destObjMesh = new ObjMesh(destVertices, destIndices)
        {
            Normals = destNormals,
            VertexColors = destMesh.Colors,
            MaterialLibraries = sourceObjMesh.MaterialLibraries,
            SubMeshMaterials = sourceObjMesh.SubMeshMaterials
        };

        if (sourceTexCoords2D != null)
        {
            var destUVs = destMesh.GetUVs2D(0);
            destObjMesh.TexCoords2D = destUVs;
        }
        else if (sourceTexCoords3D != null)
        {
            var destUVs = destMesh.GetUVs3D(0);
            destObjMesh.TexCoords3D = destUVs;
        }

        destObjMesh.WriteFile(destPath);

        var outputTriangleCount = destIndices.Sum(t => (t.Length / 3));

        if (outputTriangleCount >= currentTriangleCount * 0.95)
        {
            Console.WriteLine(" ?> WARNING: LOD at quality {0:0.00} could only reduce to {1}/{2} triangles",
                quality, outputTriangleCount, currentTriangleCount);
        }

        var reduction = (float)outputTriangleCount / currentTriangleCount;
        var timeTaken = (float)stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine(" ?> Output: {0} vertices, {1} triangles ({2} reduction; {3:0.0000} sec)",
            destVertices.Length, outputTriangleCount, reduction, timeTaken);
    }

}

/// <summary>
/// Controls how aggressively the decimation stage simplifies geometry around UV seams.
/// </summary>
public enum DecimationMode
{
    /// <summary>
    /// Maximizes triangle reduction: UV seams and foldover edges are allowed to collapse
    /// like any other edge. Can produce texture-mapping artifacts on models with UV seams.
    /// </summary>
    Aggressive,

    /// <summary>
    /// Preserves UV seam and foldover edges (avoiding texture-mapping artifacts) when the
    /// mesh has textures, or when the quality target is above 0.5 even without textures;
    /// otherwise behaves like <see cref="Aggressive"/>.
    /// </summary>
    Standard,

    /// <summary>
    /// Disables smart-link vertex welding entirely: every UV seam is treated as a plain
    /// mesh border rather than being welded and classified as a seam/foldover edge (same
    /// <see cref="MeshDecimatorCore.SimplificationOptions.PreserveBorderEdges"/> rule as
    /// the other modes then applies to it). Safest for texture fidelity, at the cost of
    /// less aggressive decimation around seams.
    /// </summary>
    Quality
}