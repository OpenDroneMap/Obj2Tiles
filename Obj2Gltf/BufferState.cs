using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SilentWave.Gltf;
using GltfBuffer = SilentWave.Gltf.Buffer;

namespace SilentWave.Obj2Gltf
{
    internal class BufferState : IDisposable
    {
        private Boolean disposedValue = false; // To detect redundant calls
        private readonly GltfModel _model;
        private readonly String _gltfFileNameNoExt;
        private readonly String _gltfFolder;
        private readonly Boolean _u32IndicesEnabled;

        /// <summary>
        /// This assumes the model is not already populated by buffers
        /// </summary>
        public BufferState(GltfModel model, String gltfPath, Boolean u32IndicesEnabled)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (String.IsNullOrWhiteSpace(gltfPath)) throw new ArgumentNullException(nameof(gltfPath));
            _gltfFileNameNoExt = Path.GetFileNameWithoutExtension(gltfPath);
            _gltfFolder = System.IO.Path.GetDirectoryName(gltfPath);
            _u32IndicesEnabled = u32IndicesEnabled;
        }

        public BinaryWriter PositionsStream { get; private set; }
        public GltfBuffer PositionsBuffer { get; private set; }
        public BufferView PositionsBufferView { get; private set; }
        public Int32? PositionsBufferViewIndex { get; private set; }
        public Accessor CurrentPositionsAccessor { get; private set; }

        public BinaryWriter NormalsStream { get; private set; }
        public GltfBuffer NormalsBuffer { get; private set; }
        public BufferView NormalsBufferView { get; private set; }
        public Int32? NormalsBufferViewIndex { get; private set; }
        public Accessor CurrentNormalsAccessor { get; private set; }

        public BinaryWriter UvsStream { get; private set; }
        public GltfBuffer UvsBuffer { get; private set; }
        public BufferView UvsBufferView { get; private set; }
        public Int32? UvsBufferViewIndex { get; private set; }
        public Accessor CurrentUvsAccessor { get; private set; }

        public BinaryWriter IndicesStream { get; private set; }
        public GltfBuffer IndicesBuffer { get; private set; }
        public BufferView IndicesBufferView { get; private set; }
        public Int32? IndicesBufferViewIndex { get; private set; }
        public Accessor CurrentIndicesAccessor { get; private set; }

        public void AddPosition(SVec3 position)
        {
            var accessor = CurrentPositionsAccessor;
            AddTobuffer(position, PositionsStream, PositionsBuffer, PositionsBufferView, accessor);
            accessor.Min[0] = position.X < accessor.Min[0] ? position.X : accessor.Min[0];
            accessor.Min[1] = position.Y < accessor.Min[1] ? position.Y : accessor.Min[1];
            accessor.Min[2] = position.Z < accessor.Min[2] ? position.Z : accessor.Min[2];

            accessor.Max[0] = position.X > accessor.Max[0] ? position.X : accessor.Max[0];
            accessor.Max[1] = position.Y > accessor.Max[1] ? position.Y : accessor.Max[1];
            accessor.Max[2] = position.Z > accessor.Max[2] ? position.Z : accessor.Max[2];
        }

        internal void AddNormal(SVec3 normal)
        {
            AddTobuffer(normal, NormalsStream, NormalsBuffer, NormalsBufferView, CurrentNormalsAccessor);
        }
        internal void AddUv(SVec2 uv)
        {
            AddTobuffer(uv, UvsStream, UvsBuffer, UvsBufferView, CurrentUvsAccessor);
        }
        internal void AddIndex(Int32 index)
        {
            var accessor = CurrentIndicesAccessor;
            if (_u32IndicesEnabled)
            {
                AddTobuffer(
                    (UInt32)index,
                    IndicesStream,
                    IndicesBuffer,
                    IndicesBufferView,
                    accessor);
            }
            else
            {
                AddTobuffer(
                    (UInt16)index,
                    IndicesStream,
                    IndicesBuffer,
                    IndicesBufferView,
                    accessor);
            }

            accessor.Min[0] = index < accessor.Min[0] ? index : accessor.Min[0];
            accessor.Max[0] = index > accessor.Max[0] ? index : accessor.Max[0];
        }

        private void AddTobuffer(SVec2 value, BinaryWriter sw, GltfBuffer buffer, BufferView bufferview, Accessor accessor)
        {
            value.WriteBytes(sw);
            buffer.ByteLength += 8;
            bufferview.ByteLength = buffer.ByteLength;
            accessor.Count++;
        }

        private void AddTobuffer(SVec3 value, BinaryWriter sw, GltfBuffer buffer, BufferView bufferview, Accessor accessor)
        {
            value.WriteBytes(sw);
            buffer.ByteLength += 12;
            bufferview.ByteLength = buffer.ByteLength;
            accessor.Count++;
        }

        private void AddTobuffer(UInt16 value, BinaryWriter sw, GltfBuffer buffer, BufferView bufferview, Accessor accessor)
        {
            sw.Write(value);
            buffer.ByteLength += 2;
            bufferview.ByteLength = buffer.ByteLength;
            accessor.Count++;
        }

        private void AddTobuffer(UInt32 value, BinaryWriter sw, GltfBuffer buffer, BufferView bufferview, Accessor accessor)
        {
            sw.Write(value);
            buffer.ByteLength += 4;
            bufferview.ByteLength = buffer.ByteLength;
            accessor.Count++;
        }

        internal Int32 MakePositionAccessor(String name)
        {
            if (PositionsBufferView == null)
            {
                var positionsFile = _gltfFileNameNoExt + "_Positions.bin";
                var stream = File.Create(Path.Combine(_gltfFolder, positionsFile));
                PositionsStream = new BinaryWriter(stream);
                PositionsBuffer = new GltfBuffer() { Name = "Positions", Uri = positionsFile };
                PositionsBufferView = new BufferView
                {
                    Name = "Positions",
                    ByteStride = 12,
                    Buffer = _model.AddBuffer(PositionsBuffer),
                    Target = BufferViewTarget.ARRAY_BUFFER
                };
                PositionsBufferViewIndex = _model.AddBufferView(PositionsBufferView);
            }
            CurrentPositionsAccessor = new Accessor
            {
                Name = name,
                Min = new Single[] { Single.MaxValue, Single.MaxValue, Single.MaxValue },   // any number must be smaller
                Max = new Single[] { Single.MinValue, Single.MinValue, Single.MinValue },   // any number must be bigger
                Type = AccessorType.VEC3,
                ComponentType = ComponentType.F32,
                BufferView = PositionsBufferViewIndex.Value,
                ByteOffset = PositionsBuffer.ByteLength
            };
            return _model.AddAccessor(CurrentPositionsAccessor);
        }

        public Int32 MakeNormalAccessors(String name)
        {
            if (NormalsBufferView == null)
            {
                var normalsFileName = _gltfFileNameNoExt + "_Normals.bin";
                var stream = File.Create(Path.Combine(_gltfFolder, normalsFileName));
                NormalsStream = new BinaryWriter(stream);
                NormalsBuffer = new GltfBuffer() { Name = "Normals", Uri = normalsFileName };
                NormalsBufferView = new BufferView
                {
                    Name = "Normals",
                    Buffer = _model.AddBuffer(NormalsBuffer),
                    ByteStride = 12,
                    //Target = BufferViewTarget.ARRAY_BUFFER
                };
                NormalsBufferViewIndex = _model.AddBufferView(NormalsBufferView);
            }
            CurrentNormalsAccessor = new Accessor
            {
                Name = name,
                //Min = new Single[] { 0, 0, 0 },
                //Max = new Single[] { 0, 0, 0 },
                Type = AccessorType.VEC3,
                ComponentType = ComponentType.F32,
                BufferView = NormalsBufferViewIndex.Value,
                ByteOffset = NormalsBuffer.ByteLength
            };
            return _model.AddAccessor(CurrentNormalsAccessor);
        }

        internal Int32 MakeUvAccessor(String name)
        {
            if (UvsBufferView == null)
            {
                var UvsFileName = _gltfFileNameNoExt + "_Uvs.bin";
                var stream = File.Create(Path.Combine(_gltfFolder, UvsFileName));
                UvsStream = new BinaryWriter(stream);
                UvsBuffer = new GltfBuffer() { Name = "Uvs", Uri = UvsFileName };
                UvsBufferView = new BufferView
                {
                    Name = "Uvs",
                    Buffer = _model.AddBuffer(UvsBuffer),
                    ByteStride = 8,
                    //Target = BufferViewTarget.ARRAY_BUFFER
                };
                UvsBufferViewIndex = _model.AddBufferView(UvsBufferView);
            }
            CurrentUvsAccessor = new Accessor
            {
                Name = name,
                //Min = new Single[] { 0, 0 },
                //Max = new Single[] { 0, 0 },
                Type = AccessorType.VEC2,
                ComponentType = ComponentType.F32,
                BufferView = UvsBufferViewIndex.Value,
                ByteOffset = UvsBuffer.ByteLength
            };
            return _model.AddAccessor(CurrentUvsAccessor);
        }

        internal Int32 MakeIndicesAccessor(String name)
        {
            if (IndicesBufferView == null)
            {
                var indicessFileName = _gltfFileNameNoExt + "_Indices.bin";
                var stream = File.Create(Path.Combine(_gltfFolder, indicessFileName));
                IndicesStream = new BinaryWriter(stream);
                IndicesBuffer = new GltfBuffer() { Name = "Indices", Uri = indicessFileName };
                IndicesBufferView = new BufferView()
                {
                    Name = "Indexes",
                    //ByteStride = _u32IndicesEnabled ? 8 : 4,
                    Buffer = _model.AddBuffer(IndicesBuffer),
                    Target = BufferViewTarget.ELEMENT_ARRAY_BUFFER,
                };
                IndicesBufferViewIndex = _model.AddBufferView(IndicesBufferView);
            }

            CurrentIndicesAccessor = new Accessor
            {
                Name = name,
                Type = AccessorType.SCALAR,
                ComponentType = _u32IndicesEnabled ? ComponentType.U32 : ComponentType.U16,
                BufferView = IndicesBufferViewIndex.Value,
                ByteOffset = IndicesBuffer.ByteLength,
                Min = new[] { 0f },
                Max = new[] { 0f }
            };
            return _model.AddAccessor(CurrentIndicesAccessor);
        }

        protected virtual void Dispose(Boolean disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PositionsStream?.Dispose();
                    NormalsStream?.Dispose();
                    UvsStream?.Dispose();
                    IndicesStream?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
    }
}
