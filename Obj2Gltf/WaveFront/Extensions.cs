using System;
using System.Collections.Generic;
using System.Text;

namespace SilentWave.Obj2Gltf.WaveFront
{
    static class Extensions
    {
        public static bool RequiresUint32Indices(this ObjModel objModel) => objModel.Vertices.Count > 65534 || objModel.Uvs.Count > 65534;
    }
}
