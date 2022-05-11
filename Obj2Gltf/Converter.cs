using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using SilentWave.Obj2Gltf.WaveFront;
using SilentWave.Gltf;
using System.IO;
using Newtonsoft.Json;

namespace SilentWave.Obj2Gltf
{
    /// <summary>
    /// A delegate to get an existing texture index or add it to the list
    /// </summary>
    /// <param name="texturePath">The path where the texture can be found</param>
    /// <returns>The texture index (zero based)</returns>
    public delegate Int32 GetOrAddTexture(String texturePath);

    /// <summary>
    /// obj2gltf converter
    /// </summary>
    public class Converter
    {
        public static Converter MakeDefault() => new Converter(new ObjParser(), new MtlParser());

        private readonly ObjParser _objParser;
        private readonly IMtlParser _mtlParser;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="objPath">obj file path</param>
        /// <param name="options"></param>
        public Converter(ObjParser objParser, IMtlParser mtlParser)
        {
            _objParser = objParser ?? throw new ArgumentNullException(nameof(objParser));
            _mtlParser = mtlParser ?? throw new ArgumentNullException(nameof(mtlParser));
        }

        public void Convert(String objPath, String gltfPath, GltfConverterOptions options = null)
        {
            if (String.IsNullOrWhiteSpace(objPath))
                throw new ArgumentNullException(nameof(objPath));

            options = options ?? new GltfConverterOptions();

            var objModel = _objParser.Parse(objPath, options.RemoveDegenerateFaces, options.ObjEncoding);
            var objFolder = Path.GetDirectoryName(objPath);


            if (!String.IsNullOrEmpty(objModel.MatFilename))
            {
                var matFile = Path.Combine(objFolder, objModel.MatFilename);
                var mats = _mtlParser.ParseAsync(matFile).Result;
                objModel.Materials.AddRange(mats);
            }
            Convert(objModel, gltfPath, options);
            if (options.DeleteOriginals)
            {
                if (!String.IsNullOrEmpty(objModel.MatFilename))
                {
                    var matFile = Path.Combine(objFolder, objModel.MatFilename);
                    File.Delete(matFile);
                }
                File.Delete(objPath);
            }
        }

        private void Convert(ObjModel objModel, String outputFile, GltfConverterOptions options = null)
        {
            if (objModel == null) throw new ArgumentNullException(nameof(objModel));
            options = options ?? new GltfConverterOptions();

            var u32IndicesEnabled = objModel.RequiresUint32Indices();

            var gltfModel = new GltfModel();
            using (var bufferState = new BufferState(gltfModel, outputFile, u32IndicesEnabled))
            {
                gltfModel.Scenes.Add(new Scene());
                gltfModel.Materials.AddRange(objModel.Materials.Select(x => ConvertMaterial(x, t => GetTextureIndex(gltfModel, t))));

                var meshes = objModel.Geometries.ToArray();
                var meshesLength = meshes.Length;
                for (var i = 0; i < meshesLength; i++)
                {
                    var mesh = meshes[i];
                    if (!mesh.Faces.Any()) continue;
                    var meshIndex = AddMesh(gltfModel, objModel, bufferState, mesh);
                    AddNode(gltfModel, mesh.Id, meshIndex, null);
                }
            }

            if (gltfModel.Images.Count > 0)
            {
                gltfModel.Samplers.Add(new TextureSampler
                {
                    MagFilter = MagnificationFilterKind.Linear,
                    MinFilter = MinificationFilterKind.NearestMipmapLinear,
                    WrapS = TextureWrappingMode.Repeat,
                    WrapT = TextureWrappingMode.Repeat
                });
            }

            WriteFile(gltfModel, outputFile);
        }

        /// <summary>
        /// write converted data to file
        /// </summary>
        /// <param name="outputFile"></param>
        private void WriteFile(GltfModel gltfModel, String outputFile)
        {
            if (gltfModel == null) throw new ArgumentNullException();
            using (var file = File.CreateText(outputFile))
            {
                ToJson(gltfModel, file);
            }
        }

        private static void ToJson(Object model, StreamWriter sw)
        {
            var serializer = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented,
                ContractResolver = new CustomContractResolver()
            };
            serializer.Serialize(sw, model);
        }

        private Boolean CheckWindingCorrect(SVec3 a, SVec3 b, SVec3 c, SVec3 normal)
        {
            var ba = new SVec3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            var ca = new SVec3(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
            var cross = SVec3.Cross(ba, ca);

            return SVec3.Dot(normal, cross) > 0;
        }


        #region Materials

        /// <summary>
        /// Translate the blinn-phong model to the pbr metallic-roughness model
        /// Roughness factor is a combination of specular intensity and shininess
        /// Metallic factor is 0.0
        /// Textures are not converted for now
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static Double Luminance(FactorColor color)
        {
            return color.Red * 0.2125 + color.Green * 0.7154 + color.Blue * 0.0721;
        }

        private Int32 AddTexture(GltfModel gltfModel, String textureFilename)
        {
            var image = new Image
            {
                Name = textureFilename,
                Uri = textureFilename
            };
            var imageIndex = gltfModel.AddImage(image);

            var textureIndex = gltfModel.Textures.Count;
            var t = new Gltf.Texture
            {
                Name = textureFilename,
                Source = imageIndex,
                Sampler = 0
            };
            gltfModel.Textures.Add(t);
            return textureIndex;
        }



        private Gltf.Material GetDefault(String name = "default", AlphaMode mode = AlphaMode.OPAQUE)
        {
            return new Gltf.Material
            {
                AlphaMode = mode,
                Name = name,
                //EmissiveFactor = new double[] { 1, 1, 1 },
                PbrMetallicRoughness = new PbrMetallicRoughness
                {
                    BaseColorFactor = new Double[] { 0.5, 0.5, 0.5, 1 },
                    MetallicFactor = 1.0,
                    RoughnessFactor = 0.0
                }
            };
        }

        private static Double Clamp(Double val, Double min, Double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="mat"></param>
        /// <returns>roughnessFactor</returns>
        private static Double ConvertTraditional2MetallicRoughness(WaveFront.Material mat)
        {
            // Transform from 0-1000 range to 0-1 range. Then invert.
            //var roughnessFactor = mat.SpecularExponent; // options.metallicRoughness ? 1.0 : 0.0;
            //roughnessFactor = roughnessFactor / 1000.0;
            var roughnessFactor = 1.0 - mat.SpecularExponent / 1000.0;
            roughnessFactor = Clamp(roughnessFactor, 0.0, 1.0);

            if (mat.Specular == null || mat.Specular.Color == null)
            {
                mat.Specular = new Reflectivity(new FactorColor());
                return roughnessFactor;
            }
            // Translate the blinn-phong model to the pbr metallic-roughness model
            // Roughness factor is a combination of specular intensity and shininess
            // Metallic factor is 0.0
            // Textures are not converted for now
            var specularIntensity = Luminance(mat.Specular.Color);


            // Low specular intensity values should produce a rough material even if shininess is high.
            if (specularIntensity < 0.1)
            {
                roughnessFactor *= (1.0 - specularIntensity);
            }

            var metallicFactor = 0.0;
            mat.Specular = new Reflectivity(new FactorColor(metallicFactor));
            return roughnessFactor;
        }

        private Int32 AddMaterial(GltfModel gltfModel, Gltf.Material material)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));
            var matIndex = gltfModel.Materials.Count;
            gltfModel.Materials.Add(material);
            return matIndex;
        }

        Int32 GetTextureIndex(GltfModel gltfModel, String path)
        {
            for (var i = 0; i < gltfModel.Textures.Count; i++)
            {
                if (path == gltfModel.Textures[i].Name)
                {
                    return i;
                }
            }
            return AddTexture(gltfModel, path);
        }

        public static Gltf.Material ConvertMaterial(WaveFront.Material mat, GetOrAddTexture getOrAddTextureFunction)
        {
            var roughnessFactor = ConvertTraditional2MetallicRoughness(mat);

            var gMat = new Gltf.Material
            {
                Name = mat.Name,
                AlphaMode = AlphaMode.OPAQUE
            };

            var alpha = mat.GetAlpha();
            var metallicFactor = 0.0;
            if (mat.Specular != null && mat.Specular.Color != null)
            {
                metallicFactor = mat.Specular.Color.Red;
            }
            gMat.PbrMetallicRoughness = new PbrMetallicRoughness
            {
                RoughnessFactor = roughnessFactor,
                MetallicFactor = metallicFactor
            };
            if (mat.Diffuse != null)
            {
                gMat.PbrMetallicRoughness.BaseColorFactor = mat.Diffuse.Color.ToArray(alpha);
            }
            else if (mat.Ambient != null)
            {
                gMat.PbrMetallicRoughness.BaseColorFactor = mat.Ambient.Color.ToArray(alpha);
            }
            else
            {
                gMat.PbrMetallicRoughness.BaseColorFactor = new Double[] { 0.7, 0.7, 0.7, alpha };
            }


            var hasTexture = !String.IsNullOrEmpty(mat.DiffuseTextureFile);
            if (hasTexture)
            {
                var index = getOrAddTextureFunction(mat.DiffuseTextureFile);
                gMat.PbrMetallicRoughness.BaseColorTexture = new TextureReferenceInfo
                {
                    Index = index
                };
            }

            if (mat.Emissive != null && mat.Emissive.Color != null)
            {
                gMat.EmissiveFactor = mat.Emissive.Color.ToArray();
            }

            if (alpha < 1.0)
            {
                gMat.AlphaMode = AlphaMode.BLEND;
                gMat.DoubleSided = true;
            }

            return gMat;
        }

        //TODO: move to gltf model ?!?
        private Int32 GetMaterialIndex(GltfModel gltfModel, String matName)
        {
            for (var i = 0; i < gltfModel.Materials.Count; i++)
            {
                if (gltfModel.Materials[i].Name == matName)
                {
                    return i;
                }
            }
            return -1;
        }

        #endregion Materials

        #region Meshes

        private Int32 AddMesh(GltfModel gltfModel, ObjModel objModel, BufferState buffer, Geometry mesh)
        {
            var ps = AddVertexAttributes(gltfModel, objModel, buffer, mesh);

            var m = new Mesh
            {
                Name = mesh.Id,
                Primitives = ps
            };
            var meshIndex = gltfModel.Meshes.Count;
            gltfModel.Meshes.Add(m);
            return meshIndex;
        }

        private List<Primitive> AddVertexAttributes(GltfModel gltfModel,
                                                    ObjModel objModel,
                                                    BufferState bufferState,
                                                    Geometry mesh)
        {
            var facesGroup = mesh.Faces.GroupBy(c => c.MatName);
            var faces = new List<Face>();
            foreach (var fg in facesGroup)
            {
                var matName = fg.Key;
                var f = new Face(matName);
                foreach (var ff in fg)
                {
                    f.Triangles.AddRange(ff.Triangles);
                }
                if (f.Triangles.Count > 0)
                {
                    faces.Add(f);
                }
            }

            var hasPositions = faces.Count > 0;

            // Vertex attributes are shared by all primitives in the mesh
            var name0 = mesh.Id;

            var ps = new List<Primitive>(faces.Count * 2);
            var index = 0;
            foreach (var f in faces)
            {
                var faceName = name0;
                if (index > 0)
                {
                    faceName = name0 + "_" + index;
                }

                var hasUvs = f.Triangles.Any(d => d.V1.T > 0);
                var hasNormals = f.Triangles.Any(d => d.V1.N > 0);

                var materialIndex = GetMaterialIndexOrDefault(gltfModel, objModel, f.MatName);
                var material = materialIndex < objModel.Materials.Count ? objModel.Materials[materialIndex] : null;
                var materialHasTexture = material?.DiffuseTextureFile != null;


                // every primitive need their own vertex indices(v,t,n)
                var faceVertexCache = new Dictionary<String, Int32>();
                var faceVertexCount = 0;

                //List<int[]> indiceList = new List<int[]>(faces.Count * 2);
                //var matIndexList = new List<int>(faces.Count * 2);

                var atts = new Dictionary<String, Int32>();
                var indicesAccessorIndex = bufferState.MakeIndicesAccessor(faceName + "_indices");
                var accessorIndex = bufferState.MakePositionAccessor(faceName + "_positions");
                atts.Add("POSITION", accessorIndex);

                if (hasNormals)
                {
                    var normalsAccessorIndex = bufferState.MakeNormalAccessors(faceName + "_normals");
                    atts.Add("NORMAL", normalsAccessorIndex);
                }

                if (materialHasTexture)
                {
                    if (hasUvs)
                    {
                        var uvAccessorIndex = bufferState.MakeUvAccessor(faceName + "_texcoords");
                        atts.Add("TEXCOORD_0", uvAccessorIndex);
                    }
                    else
                    {
                        var gMat = gltfModel.Materials[materialIndex];
                        if (gMat.PbrMetallicRoughness.BaseColorTexture != null)
                        {
                            gMat.PbrMetallicRoughness.BaseColorTexture = null;
                        }
                    }
                }

                // f is a primitive
                var iList = new List<Int32>(f.Triangles.Count * 3 * 2); // primitive indices
                foreach (var triangle in f.Triangles)
                {
                    var v1Index = triangle.V1.V - 1;
                    var v2Index = triangle.V2.V - 1;
                    var v3Index = triangle.V3.V - 1;
                    var v1 = objModel.Vertices[v1Index];
                    var v2 = objModel.Vertices[v2Index];
                    var v3 = objModel.Vertices[v3Index];

                    SVec3 n1 = new SVec3(), n2 = new SVec3(), n3 = new SVec3();
                    if (triangle.V1.N > 0) // hasNormals
                    {
                        var n1Index = triangle.V1.N - 1;
                        var n2Index = triangle.V2.N - 1;
                        var n3Index = triangle.V3.N - 1;
                        n1 = objModel.Normals[n1Index];
                        n2 = objModel.Normals[n2Index];
                        n3 = objModel.Normals[n3Index];
                    }

                    SVec2 t1 = new SVec2(), t2 = new SVec2(), t3 = new SVec2();
                    if (materialHasTexture)
                    {
                        if (triangle.V1.T > 0) // hasUvs
                        {
                            var t1Index = triangle.V1.T - 1;
                            var t2Index = triangle.V2.T - 1;
                            var t3Index = triangle.V3.T - 1;
                            t1 = objModel.Uvs[t1Index];
                            t2 = objModel.Uvs[t2Index];
                            t3 = objModel.Uvs[t3Index];
                        }
                    }

                    var v1Str = triangle.V1.ToString();
                    if (!faceVertexCache.ContainsKey(v1Str))
                    {
                        faceVertexCache.Add(v1Str, faceVertexCount++);

                        bufferState.AddPosition(v1);

                        if (triangle.V1.N > 0) // hasNormals
                        {
                            bufferState.AddNormal(n1);
                        }
                        if (materialHasTexture)
                        {
                            if (triangle.V1.T > 0) // hasUvs
                            {
                                var uv = new SVec2(t1.U, 1 - t1.V);
                                bufferState.AddUv(uv);
                            }
                        }
                    }

                    var v2Str = triangle.V2.ToString();
                    if (!faceVertexCache.ContainsKey(v2Str))
                    {
                        faceVertexCache.Add(v2Str, faceVertexCount++);

                        bufferState.AddPosition(v2);
                        if (triangle.V2.N > 0) // hasNormals
                        {
                            bufferState.AddNormal(n2);
                        }
                        if (materialHasTexture)
                        {
                            if (triangle.V2.T > 0) // hasUvs
                            {

                                var uv = new SVec2(t2.U, 1 - t2.V);
                                bufferState.AddUv(uv);
                            }
                        }
                    }

                    var v3Str = triangle.V3.ToString();
                    if (!faceVertexCache.ContainsKey(v3Str))
                    {
                        faceVertexCache.Add(v3Str, faceVertexCount++);

                        bufferState.AddPosition(v3);
                        if (triangle.V3.N > 0) // hasNormals
                        {
                            bufferState.AddNormal(n3);
                        }
                        if (materialHasTexture)
                        {
                            if (triangle.V3.T > 0) // hasUvs
                            {
                                var uv = new SVec2(t3.U, 1 - t3.V);
                                bufferState.AddUv(uv);
                            }
                        }
                    }

                    // Vertex Indices
                    var correctWinding = CheckWindingCorrect(v1, v2, v3, n1);
                    if (correctWinding)
                    {
                        iList.AddRange(new[] {
                            faceVertexCache[v1Str],
                            faceVertexCache[v2Str],
                            faceVertexCache[v3Str]
                        });
                    }
                    else
                    {
                        iList.AddRange(new[] {
                            faceVertexCache[v1Str],
                            faceVertexCache[v3Str],
                            faceVertexCache[v2Str]
                        });
                    }
                }

                foreach (var i in iList)
                {
                    bufferState.AddIndex(i);
                }

                var p = new Primitive
                {
                    Attributes = atts,
                    Indices = indicesAccessorIndex,
                    Material = materialIndex,
                    Mode = MeshMode.Triangles
                };
                ps.Add(p);


                index++;
            }

            return ps;
        }

        private Int32 GetMaterialIndexOrDefault(GltfModel gltfModel, ObjModel objModel, String materialName)
        {
            if (String.IsNullOrEmpty(materialName)) materialName = "default";

            var materialIndex = GetMaterialIndex(gltfModel, materialName);
            if (materialIndex == -1)
            {
                var objMaterial = objModel.Materials.FirstOrDefault(c => c.Name == materialName);
                if (objMaterial == null)
                {
                    materialName = "default";
                    materialIndex = GetMaterialIndex(gltfModel, materialName);
                    if (materialIndex == -1)
                    {
                        var gMat = GetDefault();
                        materialIndex = AddMaterial(gltfModel, gMat);
                    }
                    else
                    {
#if DEBUG
                        System.Diagnostics.Debugger.Break();
#endif
                    }
                }
                else
                {
                    var gMat = ConvertMaterial(objMaterial, t => GetTextureIndex(gltfModel, t));
                    materialIndex = AddMaterial(gltfModel, gMat);
                }
            }

            return materialIndex;
        }

        private Int32 AddNode(GltfModel gltfModel, String name, Int32? meshIndex, Int32? parentIndex = null)
        {
            var node = new Node { Name = name, Mesh = meshIndex };
            var nodeIndex = gltfModel.Nodes.Count;
            gltfModel.Nodes.Add(node);
            //if (parentIndex != null)
            //{
            //    var pNode = _model.Nodes[parentIndex.Value];
            //    //TODO:
            //}
            //else
            //{

            //}
            gltfModel.Scenes[gltfModel.Scene].Nodes.Add(nodeIndex);

            return nodeIndex;
        }

        #endregion Meshes
    }
}
