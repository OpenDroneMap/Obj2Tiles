using System.Diagnostics;
using MeshDecimatorCore;
using MeshDecimatorCore.Math;
using Obj2Tiles.Model;

namespace Obj2Tiles.Stages;

public class DecimationStage : IStage
{
    public readonly string SourcePath;
    public readonly string DestPath;
    public readonly float Quality;

    public DecimationStage(string sourcePath, string destPath, float quality = 0.5f)
    {
        SourcePath = sourcePath;
        DestPath = destPath;
        Quality = quality;
    }

    private void InternalRun()
    {
        try
        {
            var sourcePath = Path.GetFullPath(SourcePath);
            var destPath = Path.GetFullPath(DestPath);

            var quality = Quality;
            
            quality = MathHelper.Clamp01(quality);
            var sourceObjMesh = new ObjMesh();
            sourceObjMesh.ReadFile(sourcePath);
            var sourceVertices = sourceObjMesh.Vertices;
            var sourceNormals = sourceObjMesh.Normals;
            var sourceTexCoords2D = sourceObjMesh.TexCoords2D;
            var sourceTexCoords3D = sourceObjMesh.TexCoords3D;
            var sourceSubMeshIndices = sourceObjMesh.SubMeshIndices;

            var sourceMesh = new Mesh(sourceVertices, sourceSubMeshIndices);
            sourceMesh.Normals = sourceNormals;

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
                currentTriangleCount += (sourceSubMeshIndices[i].Length / 3);
            }

            var targetTriangleCount = (int)Math.Ceiling(currentTriangleCount * quality);
            Console.WriteLine(" ?> Input: {0} vertices, {1} triangles (target {2})",
                sourceVertices.Length, currentTriangleCount, targetTriangleCount);

            Console.WriteLine(" -> Decimating mesh");
            
            var stopwatch = new Stopwatch();
            stopwatch.Reset();
            stopwatch.Start();

            var algorithm = MeshDecimation.CreateAlgorithm(Algorithm.Default);
            algorithm.Verbose = true;
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

            var reduction = (float)outputTriangleCount / (float)currentTriangleCount;
            var timeTaken = (float)stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine(" ?> Output: {0} vertices, {1} triangles ({2} reduction; {3:0.0000} sec)",
                destVertices.Length, outputTriangleCount, reduction, timeTaken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error decimating mesh. Reason: {0}", ex.Message);
        }
    }
    
    public Task Run()
    {
        return Task.Run(InternalRun);
    }
}