using System;

namespace SilentWave.Obj2Gltf.Gltf
{
    static class Extensions
    {
        // TODO: lock all those add operations in a way that is per model (eg add a static dictionary of model / lock )
        public static int AddBuffer(this GltfModel model, Buffer buffer)
        {
            var index = model.Buffers.Count;
            model.Buffers.Add(buffer);
            return index;
        }

        public static int AddBufferView(this GltfModel gltfModel, BufferView bufferView)
        {
            var bufferViewIndex = gltfModel.BufferViews.Count;
            gltfModel.BufferViews.Add(bufferView);
            return bufferViewIndex;
        }

        public static int AddAccessor(this GltfModel model, Accessor accessor)
        {
            var index = model.Accessors.Count;
            model.Accessors.Add(accessor);
            return index;
        }

        public static int AddImage(this GltfModel gltfModel, Image image)
        {
            var imageIndex = gltfModel.Images.Count;
            gltfModel.Images.Add(image);
            return imageIndex;
        }
    }
}
