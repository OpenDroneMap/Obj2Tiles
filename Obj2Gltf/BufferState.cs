using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SilentWave.Obj2Gltf.Gltf;
using GltfBuffer = SilentWave.Obj2Gltf.Gltf.Buffer;

namespace SilentWave.Obj2Gltf
{
    internal class BufferState : IDisposable
    {
        private bool disposedValue = false; // To detect redundant calls
        private readonly GltfModel _model;
        private readonly string _gltfFileNameNoExt;
        private readonly string _gltfFolder;
        private readonly bool _u32IndicesEnabled;

        /// <summary>
        /// This assumes the model is not already populated by buffers
        /// </summary>
        public BufferState(GltfModel model, string gltfPath, bool u32IndicesEnabled)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(gltfPath)) throw new ArgumentNullException(nameof(gltfPath));
            _gltfFileNameNoExt = Path.GetFileNameWithoutExtension(gltfPath);
            _gltfFolder = System.IO.Path.GetDirectoryName(gltfPath);
            _u32IndicesEnabled = u32IndicesEnabled;
        }

        public BinaryWriter PositionsStream { get; private set; }
        public GltfBuffer PositionsBuffer { get; private set; }
        public BufferView PositionsBufferView { get; private set; }
        public int? PositionsBufferViewIndex { get; private set; }
        public Accessor CurrentPositionsAccessor { get; private set; }

        public BinaryWriter NormalsStream { get; private set; }
        public GltfBuffer NormalsBuffer { get; private set; }
        public BufferView NormalsBufferView { get; private set; }
        public int? NormalsBufferViewIndex { get; private set; }
        public Accessor CurrentNormalsAccessor { get; private set; }

        public BinaryWriter UvsStream { get; private set; }
        public GltfBuffer UvsBuffer { get; private set; }
        public BufferView UvsBufferView { get; private set; }
        public int? UvsBufferViewIndex { get; private set; }
        public Accessor CurrentUvsAccessor { get; private set; }

        public BinaryWriter IndicesStream { get; private set; }
        public GltfBuffer IndicesBuffer { get; private set; }
        public BufferView IndicesBufferView { get; private set; }
        public int? IndicesBufferViewIndex { get; private set; }
        public Accessor CurrentIndicesAccessor { get; private set; }

        public BinaryWriter ColorsStream { get; private set; }
        public GltfBuffer ColorsBuffer { get; private set; }
        public BufferView ColorsBufferView { get; private set; }
        public int? ColorsBufferViewIndex { get; private set; }
        public Accessor CurrentColorsAccessor { get; private set; }

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
        internal void AddColor(SVec3 color)
        {
            AddTobuffer(color, ColorsStream, ColorsBuffer, ColorsBufferView, CurrentColorsAccessor);
        }
        internal void AddUv(SVec2 uv)
        {
            AddTobuffer(uv, UvsStream, UvsBuffer, UvsBufferView, CurrentUvsAccessor);
        }
        internal void AddIndex(int index)
        {
            var accessor = CurrentIndicesAccessor;
            if (_u32IndicesEnabled)
            {
                AddTobuffer(
                    (uint)index,
                    IndicesStream,
                    IndicesBuffer,
                    IndicesBufferView,
                    accessor);
            }
            else
            {
                AddTobuffer(
                    (ushort)index,
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

        private void AddTobuffer(ushort value, BinaryWriter sw, GltfBuffer buffer, BufferView bufferview, Accessor accessor)
        {
            sw.Write(value);
            buffer.ByteLength += 2;
            bufferview.ByteLength = buffer.ByteLength;
            accessor.Count++;
        }

        private void AddTobuffer(uint value, BinaryWriter sw, GltfBuffer buffer, BufferView bufferview, Accessor accessor)
        {
            sw.Write(value);
            buffer.ByteLength += 4;
            bufferview.ByteLength = buffer.ByteLength;
            accessor.Count++;
        }

        internal int MakePositionAccessor(string name)
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
                Min = new float[] { float.MaxValue, float.MaxValue, float.MaxValue },   // any number must be smaller
                Max = new float[] { float.MinValue, float.MinValue, float.MinValue },   // any number must be bigger
                Type = AccessorType.VEC3,
                ComponentType = ComponentType.F32,
                BufferView = PositionsBufferViewIndex.Value,
                ByteOffset = PositionsBuffer.ByteLength
            };
            return _model.AddAccessor(CurrentPositionsAccessor);
        }

        public int MakeNormalAccessors(string name)
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
                    Target = BufferViewTarget.ARRAY_BUFFER
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

        public int MakeColorsAccessor(string name)
        {
            if (ColorsBufferView == null)
            {
                var colorsFileName = _gltfFileNameNoExt + "_Colors.bin";
                var stream = File.Create(Path.Combine(_gltfFolder, colorsFileName));
                ColorsStream = new BinaryWriter(stream);
                ColorsBuffer = new GltfBuffer() { Name = "Colors", Uri = colorsFileName };
                ColorsBufferView = new BufferView
                {
                    Name = "Colors",
                    Buffer = _model.AddBuffer(ColorsBuffer),
                    ByteStride = 12,
                    Target = BufferViewTarget.ARRAY_BUFFER
                };
                ColorsBufferViewIndex = _model.AddBufferView(ColorsBufferView);
            }
            CurrentColorsAccessor = new Accessor
            {
                Name = name,
                Type = AccessorType.VEC3,
                ComponentType = ComponentType.F32,
                BufferView = ColorsBufferViewIndex.Value,
                ByteOffset = ColorsBuffer.ByteLength
            };
            return _model.AddAccessor(CurrentColorsAccessor);
        }

        internal int MakeUvAccessor(string name)
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
                    Target = BufferViewTarget.ARRAY_BUFFER
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

        internal int MakeIndicesAccessor(string name)
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    PositionsStream?.Dispose();
                    NormalsStream?.Dispose();
                    ColorsStream?.Dispose();
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
