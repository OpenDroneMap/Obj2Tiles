using System.Diagnostics;
using MeshDecimatorCore;
using MeshDecimatorCore.Algorithms;
using MeshDecimatorCore.Math;
using Obj2Tiles.Stages.Model;
using Mesh = MeshDecimatorCore.Mesh;

namespace Obj2Tiles.Stages;

public static partial class StagesFacade
{
    public static async Task<DecimateResult> Decimate(string sourcePath, string destPath, int lods)
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

            tasks.Add(Task.Run(() => InternalDecimate(sourceObjMesh, destFile, quality)));
            
            destFiles.Add(destFile);
        }

        await Task.WhenAll(tasks);
        Console.WriteLine(" ?> Decimation done");
        
        Console.WriteLine(" -> Copying obj dependencies");
        Utils.CopyObjDependencies(sourcePath, destPath);
        Console.WriteLine(" ?> Dependencies copied");

        return new DecimateResult { DestFiles = destFiles.ToArray(), Bounds = bounds };

    }


    private static void InternalDecimate(ObjMesh sourceObjMesh, string destPath, float quality)
    {
        quality = MathHelper.Clamp01(quality);
        var sourceVertices = sourceObjMesh.Vertices;
        var sourceNormals = sourceObjMesh.Normals;
        var sourceTexCoords2D = sourceObjMesh.TexCoords2D;
        var sourceTexCoords3D = sourceObjMesh.TexCoords3D;
        var sourceSubMeshIndices = sourceObjMesh.SubMeshIndices;

        var sourceMesh = new Mesh(sourceVertices, sourceSubMeshIndices)
        {
            Normals = sourceNormals
        };

        if (sourceTexCoords2D != null)
        {
            sourceMesh.SetUVs(0, sourceTexCoords2D);
        }
        else if (sourceTexCoords3D != null)
        {
            sourceMesh.SetUVs(0, sourceTexCoords3D);
        }

        var currentTriangleCount = 0;
        for (var i = 0; i < sourceSubMeshIndices.Length; i++)
        {
            currentTriangleCount += sourceSubMeshIndices[i].Length / 3;
        }

        var targetTriangleCount = (int)Math.Ceiling(currentTriangleCount * quality);
        Console.WriteLine(" ?> Input: {0} vertices, {1} triangles (target {2})",
            sourceVertices.Length, currentTriangleCount, targetTriangleCount);

        var stopwatch = new Stopwatch();
        stopwatch.Reset();
        stopwatch.Start();

        var algorithm = new FastQuadricMeshSimplification
        {
            PreserveSeams = true,
            Verbose = true,
            PreserveBorders = true
        };

        var destMesh = MeshDecimation.DecimateMesh(algorithm, sourceMesh, targetTriangleCount);
        stopwatch.Stop();

        var destVertices = destMesh.Vertices;
        var destNormals = destMesh.Normals;
        var destIndices = destMesh.GetSubMeshIndices();

        var destObjMesh = new ObjMesh(destVertices, destIndices)
        {
            Normals = destNormals,
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

        var outputTriangleCount = 0;
        for (var i = 0; i < destIndices.Length; i++)
        {
            outputTriangleCount += (destIndices[i].Length / 3);
        }

        var reduction = (float)outputTriangleCount / currentTriangleCount;
        var timeTaken = (float)stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine(" ?> Output: {0} vertices, {1} triangles ({2} reduction; {3:0.0000} sec)",
            destVertices.Length, outputTriangleCount, reduction, timeTaken);
    }

}