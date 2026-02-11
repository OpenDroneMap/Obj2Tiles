#region License

/*
MIT License

Copyright(c) 2017-2018 Mattias Edlund

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

#endregion

using System.Globalization;
using MeshDecimatorCore.Math;
using Obj2Tiles.Library.Geometry;

namespace Obj2Tiles.Stages.Model
{
    /// <summary>
    /// A very simple OBJ mesh.
    /// </summary>
    public sealed class ObjMesh
    {
        #region Consts

        private const int VertexInitialCapacity = 20000;
        private const int IndexInitialCapacity = 40000;

        #endregion

        #region Structs

        private struct FaceIndex : IEquatable<FaceIndex>
        {
            public readonly int vertexIndex;
            public readonly int texCoordIndex;
            public readonly int normalIndex;
            private readonly int hashCode;

            public FaceIndex(int vertexIndex, int texCoordIndex, int normalIndex)
            {
                this.vertexIndex = vertexIndex;
                this.texCoordIndex = texCoordIndex;
                this.normalIndex = normalIndex;
                this.hashCode = (vertexIndex ^ texCoordIndex << 2 ^ normalIndex >> 2);
            }

            public override int GetHashCode()
            {
                return hashCode;
            }

            public override bool Equals(object? obj)
            {
                if (obj is FaceIndex other)
                {
                    return (vertexIndex == other.vertexIndex && texCoordIndex == other.texCoordIndex &&
                            normalIndex == other.normalIndex);
                }

                return false;
            }

            public bool Equals(FaceIndex other)
            {
                return (vertexIndex == other.vertexIndex && texCoordIndex == other.texCoordIndex &&
                        normalIndex == other.normalIndex);
            }

            public override string ToString()
            {
                return $"{{Vertex:{vertexIndex}, TexCoord:{texCoordIndex}, Normal:{normalIndex}}}";
            }
        }

        #endregion

        #region Fields

        private Vector3d[] vertices = null;
        private Vector3[] normals = null;
        private Vector4[] vertexColors = null;
        private Vector2[] texCoords2D = null;
        private Vector3[] texCoords3D = null;
        private int[][] subMeshIndices = null;
        private string[] subMeshMaterials = null;

        private string[] materialLibraries = null;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the vertices for this mesh.
        /// </summary>
        public Vector3d[] Vertices
        {
            get => vertices;
            set => vertices = value;
        }

        /// <summary>
        /// Gets or sets the normals for this mesh.
        /// </summary>
        public Vector3[] Normals
        {
            get => normals;
            set => normals = value;
        }

        /// <summary>
        /// Gets or sets the vertex colors (RGBA) for this mesh.
        /// </summary>
        public Vector4[] VertexColors
        {
            get => vertexColors;
            set => vertexColors = value;
        }

        /// <summary>
        /// Gets or sets the 2D texture coordinates for this mesh.
        /// </summary>
        public Vector2[] TexCoords2D
        {
            get => texCoords2D;
            set
            {
                texCoords3D = null;
                texCoords2D = value;
            }
        }

        /// <summary>
        /// Gets or sets the 3D texture coordinates for this mesh.
        /// </summary>
        public Vector3[] TexCoords3D
        {
            get => texCoords3D;
            set
            {
                texCoords2D = null;
                texCoords3D = value;
            }
        }

        /// <summary>
        /// Gets the count of sub-meshes in this mesh.
        /// </summary>
        public int SubMeshCount => (subMeshIndices != null ? subMeshIndices.Length : 0);

        /// <summary>
        /// Gets or sets the combined triangle indices for this mesh.
        /// Note that setting this will remove any existing sub-meshes and turn it into just one sub-mesh.
        /// </summary>
        [Obsolete("Prefer to use the 'SubMeshIndices' property instead.", false)]
        public int[] Indices
        {
            get
            {
                if (subMeshIndices == null)
                    return null;

                int combinedIndexCount = 0;
                for (int i = 0; i < subMeshIndices.Length; i++)
                {
                    combinedIndexCount += subMeshIndices[i].Length;
                }

                var combinedIndices = new int[combinedIndexCount];
                int offset = 0;
                for (int i = 0; i < subMeshIndices.Length; i++)
                {
                    var indices = subMeshIndices[i];
                    Array.Copy(indices, 0, combinedIndices, offset, indices.Length);
                    offset += indices.Length;
                }

                return combinedIndices;
            }
            set
            {
                if (value != null)
                {
                    subMeshIndices = new int[][] { value };
                }
                else
                {
                    subMeshIndices = null;
                }
            }
        }

        /// <summary>
        /// Gets or sets the indices divided by sub-meshes.
        /// </summary>
        public int[][] SubMeshIndices
        {
            get => subMeshIndices;
            set
            {
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        if (value[i] == null)
                            throw new ArgumentException($"The sub-mesh index array at index {i} is null.", "value");
                    }
                }

                subMeshIndices = value;
            }
        }

        /// <summary>
        /// Gets or sets the names of each sub-mesh material.
        /// </summary>
        public string[] SubMeshMaterials
        {
            get => subMeshMaterials;
            set
            {
                if (value != null)
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        if (value[i] == null)
                            throw new ArgumentException($"The sub-mesh material name at index {i} is null.", "value");
                    }
                }

                subMeshMaterials = value;
            }
        }

        /// <summary>
        /// Gets or sets the paths to material libraries used by this mesh.
        /// </summary>
        public string[] MaterialLibraries
        {
            get => materialLibraries;
            set => materialLibraries = value;
        }

        public Box3 Bounds { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new OBJ mesh.
        /// </summary>
        public ObjMesh()
        {
        }

        /// <summary>
        /// Creates a new OBJ mesh.
        /// </summary>
        /// <param name="vertices">The mesh vertices.</param>
        /// <param name="indices">The mesh indices.</param>
        public ObjMesh(Vector3d[] vertices, int[] indices)
        {
            this.vertices = vertices;
            this.subMeshIndices = (indices != null ? new int[][] { indices } : null);
        }

        /// <summary>
        /// Creates a new OBJ mesh.
        /// </summary>
        /// <param name="vertices">The mesh vertices.</param>
        /// <param name="indices">The mesh indices.</param>
        public ObjMesh(Vector3d[] vertices, int[][] indices)
        {
            this.vertices = vertices;
            this.subMeshIndices = indices;
        }

        #endregion

        #region Public Methods

        #region Read File

        /// <summary>
        /// Reads an OBJ mesh from a file.
        /// Please note that this method only supports extremely simple OBJ meshes.
        /// </summary>
        /// <param name="path">The file path.</param>
        public void ReadFile(string path)
        {
            var materialLibraryList = new List<string>();
            var readVertexList = new List<Vector3d>(VertexInitialCapacity);
            List<Vector4> readColorList = null;
            List<Vector3> readNormalList = null;
            List<Vector3> readTexCoordList = null;
            var vertexList = new List<Vector3d>(VertexInitialCapacity);
            List<Vector4> colorList = null;
            List<Vector3> normalList = null;
            List<Vector3> texCoordList = null;
            var triangleIndexList = new List<int>(IndexInitialCapacity);
            var subMeshIndicesList = new List<int[]>();
            var subMeshMaterialList = new List<string>();
            var faceTable = new Dictionary<FaceIndex, int>(IndexInitialCapacity);
            var tempFaceList = new List<int>(6);
            bool texCoordsAre3D = false;

            string currentGroup = null;
            string currentObject = null;
            string currentMaterial = null;
            int newFaceIndex = 0;

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minZ = double.MaxValue, maxZ = double.MinValue;

            using (var reader = File.OpenText(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#')
                        continue;

                    string[] lineSplit = line.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    string firstPart = lineSplit[0];
                    if (string.Equals(firstPart, "v"))
                    {
                        if (lineSplit.Length < 4)
                            throw new InvalidDataException("Vertices needs at least 3 components.");

                        double.TryParse(lineSplit[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x);
                        double.TryParse(lineSplit[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y);
                        double.TryParse(lineSplit[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z);
                        readVertexList.Add(new Vector3d(x, y, z));

                        if (lineSplit.Length >= 7)
                        {
                            readColorList ??= new List<Vector4>(VertexInitialCapacity);
                            float.TryParse(lineSplit[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var cr);
                            float.TryParse(lineSplit[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var cg);
                            float.TryParse(lineSplit[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var cb);
                            var ca = lineSplit.Length >= 8 ? float.Parse(lineSplit[7], NumberStyles.Float, CultureInfo.InvariantCulture) : 1f;
                            readColorList.Add(new Vector4(cr, cg, cb, ca));
                        }

                        if (x < minX) minX = x; else if (x > maxX) maxX = x;
                        if (y < minY) minY = y; else if (y > maxY) maxY = y;
                        if (z < minZ) minZ = z; else if (z > maxZ) maxZ = z;

                    }
                    else if (string.Equals(firstPart, "vn"))
                    {
                        if (lineSplit.Length != 4)
                            throw new InvalidDataException("Normals must be 3 components.");

                        readNormalList ??= new List<Vector3>(VertexInitialCapacity);

                        float.TryParse(lineSplit[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f0);
                        float.TryParse(lineSplit[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var f1);
                        float.TryParse(lineSplit[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var f2);
                        readNormalList.Add(new Vector3(f0, f1, f2));
                    }
                    else if (string.Equals(firstPart, "vt"))
                    {
                        if (lineSplit.Length < 3)
                            throw new InvalidDataException("Texture coordinates needs at least 2 components.");

                        readTexCoordList ??= new List<Vector3>(VertexInitialCapacity);

                        float f2;
                        float.TryParse(lineSplit[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f0);
                        float.TryParse(lineSplit[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var f1);
                        if (lineSplit.Length > 3)
                        {
                            float.TryParse(lineSplit[3], NumberStyles.Float, CultureInfo.InvariantCulture, out f2);

                            if (!texCoordsAre3D && f2 != 0f)
                            {
                                texCoordsAre3D = true;
                            }
                        }
                        else
                        {
                            f2 = 0f;
                        }

                        readTexCoordList.Add(new Vector3(f0, f1, f2));
                    }
                    else if (string.Equals(firstPart, "f"))
                    {
                        if (lineSplit.Length < 4)
                            throw new InvalidDataException("Faces must have at least three indices.");

                        tempFaceList.Clear();
                        for (int i = 1; i < lineSplit.Length; i++)
                        {
                            string word = lineSplit[i];
                            int slashCount = CountOccurrences(word, '/');
                            int vertexIndex;
                            int texIndex;
                            int normalIndex;
                            if (slashCount == 0)
                            {
                                int.TryParse(word, out vertexIndex);
                                vertexIndex = ShiftIndex(vertexIndex, readVertexList.Count);
                                texIndex = -1;
                                normalIndex = -1;
                            }
                            else if (slashCount == 1)
                            {
                                int splitIndex = word.IndexOf('/');
                                string word1 = word.Substring(0, splitIndex);
                                string word2 = word.Substring(splitIndex + 1);
                                int.TryParse(word1, out vertexIndex);
                                int.TryParse(word2, out texIndex);
                                vertexIndex = ShiftIndex(vertexIndex, readVertexList.Count);
                                texIndex = ShiftIndex(texIndex, readTexCoordList.Count);
                                normalIndex = -1;
                            }
                            else if (slashCount == 2)
                            {
                                int splitIndex1 = word.IndexOf('/');
                                int splitIndex2 = word.IndexOf('/', splitIndex1 + 1);
                                string word1 = word.Substring(0, splitIndex1);
                                string word2 = word.Substring(splitIndex1 + 1, splitIndex2 - splitIndex1 - 1);
                                string word3 = word.Substring(splitIndex2 + 1);
                                int.TryParse(word1, out vertexIndex);
                                bool hasTexCoord = int.TryParse(word2, out texIndex);
                                int.TryParse(word3, out normalIndex);
                                vertexIndex = ShiftIndex(vertexIndex, readVertexList.Count);
                                if (hasTexCoord)
                                {
                                    texIndex = ShiftIndex(texIndex, readTexCoordList.Count);
                                }
                                else
                                {
                                    texIndex = -1;
                                }

                                normalIndex = ShiftIndex(normalIndex, readNormalList.Count);
                            }
                            else
                            {
                                throw new InvalidDataException(
                                    $"Invalid face data are supported (expected a maximum of two slashes, but found {slashCount}.");
                            }

                            var face = new FaceIndex(vertexIndex, texIndex, normalIndex);
                            if (faceTable.TryGetValue(face, out var faceIndex))
                            {
                                tempFaceList.Add(faceIndex);
                            }
                            else
                            {
                                faceTable[face] = newFaceIndex;
                                tempFaceList.Add(newFaceIndex);
                                ++newFaceIndex;

                                vertexList.Add(readVertexList[vertexIndex]);

                                if (readColorList != null)
                                {
                                    colorList ??= new List<Vector4>(VertexInitialCapacity);

                                    if (vertexIndex >= 0 && vertexIndex < readColorList.Count)
                                    {
                                        colorList.Add(readColorList[vertexIndex]);
                                    }
                                    else
                                    {
                                        colorList.Add(new Vector4(1f, 1f, 1f, 1f));
                                    }
                                }

                                if (readNormalList != null)
                                {
                                    normalList ??= new List<Vector3>(VertexInitialCapacity);

                                    if (normalIndex >= 0 && normalIndex < readNormalList.Count)
                                    {
                                        normalList.Add(readNormalList[normalIndex]);
                                    }
                                    else
                                    {
                                        normalList.Add(Vector3.zero);
                                    }
                                }

                                if (readTexCoordList != null)
                                {
                                    texCoordList ??= new List<Vector3>(VertexInitialCapacity);

                                    if (texIndex >= 0 && texIndex < readTexCoordList.Count)
                                    {
                                        texCoordList.Add(readTexCoordList[texIndex]);
                                    }
                                    else
                                    {
                                        texCoordList.Add(Vector3.zero);
                                    }
                                }
                            }
                        }

                        // Convert into triangles (currently we only support triangles and quads)
                        int faceIndexCount = tempFaceList.Count;
                        if (faceIndexCount >= 3 && faceIndexCount < 5)
                        {
                            triangleIndexList.Add(tempFaceList[0]);
                            triangleIndexList.Add(tempFaceList[1]);
                            triangleIndexList.Add(tempFaceList[2]);

                            if (faceIndexCount > 3)
                            {
                                triangleIndexList.Add(tempFaceList[2]);
                                triangleIndexList.Add(tempFaceList[3]);
                                triangleIndexList.Add(tempFaceList[0]);
                            }
                        }
                    }
                    else if (string.Equals(firstPart, "g"))
                    {
                        string groupName = string.Join(" ", lineSplit, 1, lineSplit.Length - 1);
                        currentGroup = groupName;
                    }
                    else if (string.Equals(firstPart, "o"))
                    {
                        string objectName = string.Join(" ", lineSplit, 1, lineSplit.Length - 1);
                        currentObject = objectName;
                    }
                    else if (string.Equals(firstPart, "mtllib"))
                    {
                        string materialLibraryPath = string.Join(" ", lineSplit, 1, lineSplit.Length - 1);
                        materialLibraryList.Add(materialLibraryPath);
                    }
                    else if (string.Equals(firstPart, "usemtl"))
                    {
                        string materialName = string.Join(" ", lineSplit, 1, lineSplit.Length - 1);
                        currentMaterial = materialName;

                        if (triangleIndexList.Count > 0)
                        {
                            subMeshIndicesList.Add(triangleIndexList.ToArray());
                            triangleIndexList.Clear();
                            /*
                            if (subMeshMaterialList.Count != subMeshIndicesList.Count)
                            {
                                subMeshMaterialList.Add("none");
                            }*/
                        }

                        subMeshMaterialList.Add(materialName);
                    }
                }
            }

            if (triangleIndexList.Count > 0)
            {
                subMeshIndicesList.Add(triangleIndexList.ToArray());
                triangleIndexList.Clear();

                /*
                if (currentMaterial == null)
                {
                    subMeshMaterialList.Add("none");
                }*/
            }

            int subMeshCount = subMeshIndicesList.Count;
            bool hasNormals = (readNormalList != null);
            bool hasTexCoords = (readTexCoordList != null);
            bool hasColors = (colorList != null);
            int vertexCount = vertexList.Count;
            var processedVertexList = new List<Vector3d>(vertexCount);
            var processedColorList = (hasColors ? new List<Vector4>(vertexCount) : null);
            var processedNormalList = (hasNormals ? new List<Vector3>(vertexCount) : null);
            var processedTexCoordList = (hasTexCoords ? new List<Vector3>(vertexCount) : null);
            var processedIndices = new List<int[]>(subMeshCount);
            var indexMappings = new Dictionary<int, int>(IndexInitialCapacity);

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                var indices = subMeshIndicesList[subMeshIndex];
                for (int i = 0; i < indices.Length; i++)
                {
                    int index = indices[i];
                    if (indexMappings.TryGetValue(index, out var mappedIndex))
                    {
                        indices[i] = mappedIndex;
                    }
                    else
                    {
                        processedVertexList.Add(vertexList[index]);
                        if (hasColors)
                        {
                            processedColorList.Add(colorList[index]);
                        }

                        if (hasNormals)
                        {
                            processedNormalList.Add(normalList[index]);
                        }

                        if (hasTexCoords)
                        {
                            processedTexCoordList.Add(texCoordList[index]);
                        }

                        mappedIndex = processedVertexList.Count - 1;
                        indexMappings[index] = mappedIndex;
                        indices[i] = mappedIndex;
                    }
                }

                processedIndices.Add(indices);
            }

            vertices = processedVertexList.ToArray();
            vertexColors = (processedColorList != null ? processedColorList.ToArray() : null);
            normals = (processedNormalList != null ? processedNormalList.ToArray() : null);

            if (processedTexCoordList != null)
            {
                if (texCoordsAre3D)
                {
                    texCoords3D = processedTexCoordList.ToArray();
                }
                else
                {
                    int texCoordCount = processedTexCoordList.Count;
                    texCoords2D = new Vector2[texCoordCount];
                    for (int i = 0; i < texCoordCount; i++)
                    {
                        var texCoord = processedTexCoordList[i];
                        texCoords2D[i] = new Vector2(texCoord.x, texCoord.y);
                    }
                }
            }
            else
            {
                texCoords2D = null;
                texCoords3D = null;
            }

            subMeshIndices = processedIndices.ToArray();
            subMeshMaterials = subMeshMaterialList.Any() ? subMeshMaterialList.ToArray() : null;
            materialLibraries = materialLibraryList.Any() ? materialLibraryList.ToArray() : null;
            Bounds = new Box3(minX, minY, minZ, maxX, maxY, maxZ);
        }

        #endregion

        #region Write File

        /// <summary>
        /// Writes this OBJ mesh to a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        public void WriteFile(string path)
        {
            if (vertices == null)
                throw new InvalidOperationException("There are no vertices to write for this mesh.");
            if (subMeshIndices == null)
                throw new InvalidOperationException("There are no indices to write for this mesh.");
            if (subMeshMaterials != null && subMeshMaterials.Length != subMeshIndices.Length)
                throw new InvalidOperationException(
                    "The number of sub-mesh material names does not match the count of sub-mesh index arrays.");

            // TODO: Optimize the output by sharing vertices, normals, etc

            using (StreamWriter writer = File.CreateText(path))
            {
                if (materialLibraries != null && materialLibraries.Length > 0)
                {
                    for (int i = 0; i < materialLibraries.Length; i++)
                    {
                        string materialLibraryPath = materialLibraries[i];
                        writer.Write("mtllib ");
                        writer.WriteLine(materialLibraryPath);
                    }

                    writer.WriteLine();
                }

                WriteVertices(writer, vertices, vertexColors);
                WriteNormals(writer, normals);
                WriteTextureCoords(writer, texCoords2D, texCoords3D);

                bool hasTexCoords = (texCoords2D != null || texCoords3D != null);
                bool hasNormals = (normals != null);
                WriteSubMeshes(writer, subMeshIndices, subMeshMaterials, hasTexCoords, hasNormals);
            }
        }

        #endregion

        #endregion

        #region Private Methods

        private static void WriteVertices(TextWriter writer, Vector3d[] vertices, Vector4[] colors)
        {
            bool hasColors = (colors != null && colors.Length == vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                var vertex = vertices[i];
                writer.Write("v ");
                writer.Write(vertex.x.ToString("g", CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(vertex.y.ToString("g", CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(vertex.z.ToString("g", CultureInfo.InvariantCulture));
                if (hasColors)
                {
                    var c = colors[i];
                    writer.Write(' ');
                    writer.Write(c.x.ToString("g", CultureInfo.InvariantCulture));
                    writer.Write(' ');
                    writer.Write(c.y.ToString("g", CultureInfo.InvariantCulture));
                    writer.Write(' ');
                    writer.Write(c.z.ToString("g", CultureInfo.InvariantCulture));
                }
                writer.WriteLine();
            }
        }

        private static void WriteNormals(TextWriter writer, Vector3[] normals)
        {
            if (normals == null)
                return;

            for (int i = 0; i < normals.Length; i++)
            {
                var normal = normals[i];
                writer.Write("vn ");
                writer.Write(normal.x.ToString("g", CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(normal.y.ToString("g", CultureInfo.InvariantCulture));
                writer.Write(' ');
                writer.Write(normal.z.ToString("g", CultureInfo.InvariantCulture));
                writer.WriteLine();
            }
        }

        private static void WriteTextureCoords(TextWriter writer, Vector2[] texCoords2D, Vector3[] texCoords3D)
        {
            if (texCoords2D != null)
            {
                for (int i = 0; i < texCoords2D.Length; i++)
                {
                    var texCoord = texCoords2D[i];
                    writer.Write("vt ");
                    writer.Write(texCoord.x.ToString("g", CultureInfo.InvariantCulture));
                    writer.Write(' ');
                    writer.Write(texCoord.y.ToString("g", CultureInfo.InvariantCulture));
                    writer.WriteLine();
                }
            }
            else if (texCoords3D != null)
            {
                for (int i = 0; i < texCoords3D.Length; i++)
                {
                    var texCoord = texCoords3D[i];
                    writer.Write("vt ");
                    writer.Write(texCoord.x.ToString("g", CultureInfo.InvariantCulture));
                    writer.Write(' ');
                    writer.Write(texCoord.y.ToString("g", CultureInfo.InvariantCulture));
                    writer.Write(' ');
                    writer.Write(texCoord.z.ToString("g", CultureInfo.InvariantCulture));
                    writer.WriteLine();
                }
            }
        }

        private static void WriteSubMeshes(TextWriter writer, int[][] subMeshIndices, string[] subMeshMaterials,
            bool hasTexCoords, bool hasNormals)
        {
            for (int subMeshIndex = 0; subMeshIndex < subMeshIndices.Length; subMeshIndex++)
            {
                var indices = subMeshIndices[subMeshIndex];

                writer.WriteLine();
                writer.WriteLine("# Sub-mesh {0}", (subMeshIndex + 1));

                if (subMeshMaterials != null)
                {
                    string materialName = subMeshMaterials[subMeshIndex];
                    writer.Write("usemtl ");
                    writer.WriteLine(materialName);
                }

                WriteFaces(writer, indices, hasTexCoords, hasNormals);
            }
        }

        private static void WriteFaces(TextWriter writer, int[] indices, bool hasTexCoords, bool hasNormals)
        {
            for (int i = 0; i < indices.Length; i += 3)
            {
                int v0 = indices[i] + 1;
                int v1 = indices[i + 1] + 1;
                int v2 = indices[i + 2] + 1;

                writer.Write("f ");

                if (hasTexCoords && hasNormals)
                {
                    writer.Write(v0);
                    writer.Write('/');
                    writer.Write(v0);
                    writer.Write('/');
                    writer.Write(v0);
                    writer.Write(' ');
                    writer.Write(v1);
                    writer.Write('/');
                    writer.Write(v1);
                    writer.Write('/');
                    writer.Write(v1);
                    writer.Write(' ');
                    writer.Write(v2);
                    writer.Write('/');
                    writer.Write(v2);
                    writer.Write('/');
                    writer.Write(v2);
                }
                else if (hasTexCoords)
                {
                    writer.Write(v0);
                    writer.Write('/');
                    writer.Write(v0);
                    writer.Write(' ');
                    writer.Write(v1);
                    writer.Write('/');
                    writer.Write(v1);
                    writer.Write(' ');
                    writer.Write(v2);
                    writer.Write('/');
                    writer.Write(v2);
                }
                else if (hasNormals)
                {
                    writer.Write(v0);
                    writer.Write('/');
                    writer.Write('/');
                    writer.Write(v0);
                    writer.Write(' ');
                    writer.Write(v1);
                    writer.Write('/');
                    writer.Write('/');
                    writer.Write(v1);
                    writer.Write(' ');
                    writer.Write(v2);
                    writer.Write('/');
                    writer.Write('/');
                    writer.Write(v2);
                }
                else
                {
                    writer.Write(v0);
                    writer.Write(' ');
                    writer.Write(v1);
                    writer.Write(' ');
                    writer.Write(v2);
                }

                writer.WriteLine();
            }
        }

        private static int ShiftIndex(int value, int count)
        {
            return value < 0 ? count + value : value - 1;
        }

        private static int CountOccurrences(string text, char character)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == character)
                {
                    ++count;
                }
            }

            return count;
        }

        #endregion
    }
}