using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SilentWave.Obj2Gltf
{
    class CustomContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (property.DeclaringType == typeof(Gltf.GltfModel))
            {
                //TODO: just check if they are array like and skip them if empty
                //#if DEBUG
                //                if(member.GetType() .GetInterfaces().Contains(asd))
                //                {
                //                    System.Diagnostics.Debugger.Break();
                //                }
                //#endif
                if (property.PropertyName == nameof(Gltf.GltfModel.Images).ToLower())
                    property.ShouldSerialize = instance => (instance as Gltf.GltfModel).Images.Count > 0;
                else if (property.PropertyName == nameof(Gltf.GltfModel.Textures).ToLower())
                    property.ShouldSerialize = instance => (instance as Gltf.GltfModel).Textures.Count > 0;
                else if (property.PropertyName == nameof(Gltf.GltfModel.Samplers).ToLower())
                    property.ShouldSerialize = instance => (instance as Gltf.GltfModel).Samplers.Count > 0;
            }
            return property;
        }
    }
}
