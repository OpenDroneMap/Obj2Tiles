using System;
using System.Collections.Generic;
using System.Text;

namespace SilentWave.Gltf
{
    /// <summary>
    /// The component data type.
    /// </summary>
    public enum ComponentType
    {
        /// Corresponds to `GL_BYTE`.
        I8 = 5120,

        /// Corresponds to `GL_UNSIGNED_BYTE`.
        U8,

        /// Corresponds to `GL_SHORT`.
        I16,

        /// Corresponds to `GL_UNSIGNED_SHORT`.
        U16,

        /// Corresponds to `GL_UNSIGNED_INT`.
        U32 = 5125,

        /// Corresponds to `GL_FLOAT`.
        F32,
    }
}
