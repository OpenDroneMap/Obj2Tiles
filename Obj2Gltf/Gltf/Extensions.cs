using System;
using System.Collections.Generic;
using System.Text;

namespace SilentWave.Gltf
{
    static class Extensions
    {
        // TODO: lock all those add operations in a way that is per model (eg add a static dictionary of model / lock )
        public static Int32 AddBuffer(this GltfModel model, Buffer buffer)
        {
            var index = model.Buffers.Count;
            model.Buffers.Add(buffer);
            return index;
        }

        public static Int32 AddBufferView(this GltfModel gltfModel, BufferView bufferView)
        {
            var bufferViewIndex = gltfModel.BufferViews.Count;
            gltfModel.BufferViews.Add(bufferView);
            return bufferViewIndex;
        }

        public static Int32 AddAccessor(this GltfModel model, Accessor accessor)
        {
            var index = model.Accessors.Count;
            model.Accessors.Add(accessor);
            return index;
        }

        public static Int32 AddImage(this GltfModel gltfModel, Image image)
        {
            var imageIndex = gltfModel.Images.Count;
            gltfModel.Images.Add(image);
            return imageIndex;
        }
    }
}
