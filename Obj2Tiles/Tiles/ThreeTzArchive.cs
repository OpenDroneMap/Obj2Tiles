using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Obj2Tiles.Tiles
{
    /// <summary>
    /// Packs a directory that contains a 3D Tiles tileset into a single 3D Tiles Archive (<c>.3tz</c>) file.
    /// </summary>
    /// <remarks>
    /// A 3TZ file is a regular ZIP archive (per the 3D Tiles Archive Format v1.3 specification,
    /// https://github.com/erikdahlstrom/3tz-specification) with two extra requirements: a valid
    /// <c>tileset.json</c> at the archive root, and a trailing index file named <c>@3dtilesIndex1@</c> that
    /// maps the MD5 hash of every entry's path to the byte offset of its local file header, enabling
    /// O(log n) random access without reading the whole central directory.
    ///
    /// The heavy lifting (DEFLATE compression, CRC32, local/central headers) is delegated to
    /// <see cref="ZipArchive"/>. The archive is written in two passes: first every tileset file is added
    /// (pass 1, Create), then the index is appended as the last entry (pass 2, Update). The only datum the
    /// BCL does not expose is each entry's local-header offset, so it is recovered with a small read-only
    /// scan of the central directory between the two passes.
    ///
    /// NOTE: Zstandard (compression method 93), also allowed by the 3TZ spec, is not yet supported because
    /// the BCL ships no Zstandard codec; STORE and DEFLATE are used for now. ZIP64 (archives or files larger
    /// than 4 GB) is likewise out of scope and rejected explicitly.
    /// </remarks>
    public static class ThreeTzArchive
    {
        /// <summary>Name of the mandatory 3TZ index; it must be the last entry and stored uncompressed.</summary>
        public const string IndexFileName = "@3dtilesIndex1@";

        private const string TilesetJsonName = "tileset.json";
        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const uint CentralDirectorySignature = 0x02014b50;
        private const uint EndOfCentralDirectorySignature = 0x06054b50;

        /// <summary>
        /// Creates a <c>.3tz</c> archive at <paramref name="archivePath"/> from the tileset located in
        /// <paramref name="sourceDirectory"/>.
        /// </summary>
        /// <param name="sourceDirectory">Directory containing the tileset (must have a root <c>tileset.json</c>).</param>
        /// <param name="archivePath">Destination <c>.3tz</c> file path (overwritten if it already exists).</param>
        /// <param name="compressionLevel">Compression applied to content entries; the index is always stored.</param>
        /// <returns>The number of content entries written (excluding the index).</returns>
        public static int CreateFromDirectory(string sourceDirectory, string archivePath,
            CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory))
                throw new ArgumentException("Source directory is required.", nameof(sourceDirectory));
            if (string.IsNullOrWhiteSpace(archivePath))
                throw new ArgumentException("Archive path is required.", nameof(archivePath));
            if (!Directory.Exists(sourceDirectory))
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
            if (!File.Exists(Path.Combine(sourceDirectory, TilesetJsonName)))
                throw new ArgumentException(
                    $"A 3TZ archive requires a '{TilesetJsonName}' at the root of the source directory.",
                    nameof(sourceDirectory));

            // Normalize entry names to the ZIP convention: forward slashes, no leading slash.
            var files = Directory
                .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(full => (Full: full, Name: NormalizeEntryName(sourceDirectory, full)))
                .ToList();

            foreach (var (_, name) in files)
            {
                if (name.Equals(IndexFileName, StringComparison.Ordinal))
                    throw new ArgumentException(
                        $"The source directory already contains a reserved '{IndexFileName}' file.",
                        nameof(sourceDirectory));
                if (name.Contains(".3tz", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        $"3TZ entry names must not contain the substring '.3tz': '{name}'.",
                        nameof(sourceDirectory));
            }

            var parent = Path.GetDirectoryName(Path.GetFullPath(archivePath));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            if (File.Exists(archivePath))
                File.Delete(archivePath);

            // Pass 1: let ZipArchive write every content file (compression, CRC32 and sizes-in-header).
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                foreach (var (full, name) in files)
                    zip.CreateEntryFromFile(full, name, compressionLevel);
            }

            // Recover each entry's local-header offset (not exposed by the BCL) with a read-only scan.
            var offsets = ReadLocalHeaderOffsets(archivePath);

            var indexBytes = BuildIndex(offsets);

            // Pass 2: append the index as the last entry, stored (uncompressed) and without a comment.
            using (var zip = ZipFile.Open(archivePath, ZipArchiveMode.Update))
            {
                var indexEntry = zip.CreateEntry(IndexFileName, CompressionLevel.NoCompression);
                using var stream = indexEntry.Open();
                stream.Write(indexBytes, 0, indexBytes.Length);
            }

            return files.Count;
        }

        /// <summary>
        /// Maps a gzip-style numeric compression level (0-9) to a <see cref="CompressionLevel"/>:
        /// 0 = stored, 1-3 = fastest, 4-6 = optimal, 7-9 = smallest size.
        /// </summary>
        /// <param name="level">Numeric level in the inclusive range 0-9.</param>
        /// <returns>The matching <see cref="CompressionLevel"/>.</returns>
        public static CompressionLevel ResolveCompressionLevel(int level)
        {
            if (level is < 0 or > 9)
                throw new ArgumentOutOfRangeException(nameof(level), level,
                    "Compression level must be between 0 and 9.");

            return level switch
            {
                0 => CompressionLevel.NoCompression,
                <= 3 => CompressionLevel.Fastest,
                <= 6 => CompressionLevel.Optimal,
                _ => CompressionLevel.SmallestSize
            };
        }

        private static string NormalizeEntryName(string sourceDirectory, string fullPath)
        {
            var relative = Path.GetRelativePath(sourceDirectory, fullPath);
            return relative.Replace('\\', '/').TrimStart('/');
        }

        private static byte[] BuildIndex(IReadOnlyList<(string Name, long Offset)> offsets)
        {
            var entries = new List<(byte[] Hash, ulong Offset)>(offsets.Count);
            foreach (var (name, offset) in offsets)
                entries.Add((MD5.HashData(Encoding.UTF8.GetBytes(name)), (ulong)offset));

            // Sort ascending by the MD5 hash read as two little-endian 64-bit integers (high then low),
            // as mandated by the 3TZ specification so readers can binary-search the index.
            entries.Sort(static (a, b) =>
            {
                var hiA = BinaryPrimitives.ReadUInt64LittleEndian(a.Hash.AsSpan(0, 8));
                var hiB = BinaryPrimitives.ReadUInt64LittleEndian(b.Hash.AsSpan(0, 8));
                if (hiA != hiB) return hiA < hiB ? -1 : 1;
                var loA = BinaryPrimitives.ReadUInt64LittleEndian(a.Hash.AsSpan(8, 8));
                var loB = BinaryPrimitives.ReadUInt64LittleEndian(b.Hash.AsSpan(8, 8));
                return loA.CompareTo(loB);
            });

            // Each entry is 16 bytes of MD5 followed by an 8-byte little-endian offset; no header, no padding.
            var buffer = new byte[entries.Count * 24];
            var span = buffer.AsSpan();
            var pos = 0;
            foreach (var (hash, offset) in entries)
            {
                hash.CopyTo(span.Slice(pos, 16));
                BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(pos + 16, 8), offset);
                pos += 24;
            }

            return buffer;
        }

        /// <summary>
        /// Reads the ZIP central directory of <paramref name="archivePath"/> and returns the local-header
        /// offset of every entry - the single datum <see cref="ZipArchive"/> does not expose.
        /// </summary>
        private static List<(string Name, long Offset)> ReadLocalHeaderOffsets(string archivePath)
        {
            using var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(fs);

            var (centralDirectoryOffset, entryCount) = FindCentralDirectory(fs, reader);

            fs.Seek(centralDirectoryOffset, SeekOrigin.Begin);
            var result = new List<(string Name, long Offset)>(entryCount);

            for (var i = 0; i < entryCount; i++)
            {
                if (reader.ReadUInt32() != CentralDirectorySignature)
                    throw new InvalidDataException(
                        "Corrupt archive: central directory record signature mismatch.");

                reader.BaseStream.Seek(24, SeekOrigin.Current); // -> file name length field
                var nameLength = reader.ReadUInt16();
                var extraLength = reader.ReadUInt16();
                var commentLength = reader.ReadUInt16();
                reader.BaseStream.Seek(8, SeekOrigin.Current);  // skip disk#, internal & external attributes
                var localHeaderOffset = reader.ReadUInt32();
                var nameBytes = reader.ReadBytes(nameLength);
                reader.BaseStream.Seek(extraLength + commentLength, SeekOrigin.Current);

                result.Add((Encoding.UTF8.GetString(nameBytes), localHeaderOffset));
            }

            return result;
        }

        private static (long Offset, int Count) FindCentralDirectory(FileStream fs, BinaryReader reader)
        {
            // The End Of Central Directory record is 22 bytes plus an optional trailing comment.
            const int minEocd = 22;
            const int maxComment = ushort.MaxValue;
            var searchLength = (int)Math.Min(fs.Length, minEocd + maxComment);
            fs.Seek(fs.Length - searchLength, SeekOrigin.Begin);
            var tail = reader.ReadBytes(searchLength);

            for (var i = tail.Length - minEocd; i >= 0; i--)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i, 4)) != EndOfCentralDirectorySignature)
                    continue;

                var count = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 10, 2));
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i + 16, 4));

                if (count == ushort.MaxValue || offset == uint.MaxValue)
                    throw new NotSupportedException(
                        "3TZ archives that require ZIP64 (more than 65535 files or larger than 4 GB) are not supported yet.");

                return (offset, count);
            }

            throw new InvalidDataException("Could not locate the ZIP End Of Central Directory record.");
        }
    }
}
