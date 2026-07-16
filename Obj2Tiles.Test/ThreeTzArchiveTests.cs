using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using Obj2Tiles.Library.Geometry;
using Obj2Tiles.Stages;
using Obj2Tiles.Tiles;
using Shouldly;

namespace Obj2Tiles.Test;

public class ThreeTzArchiveTests
{
    private const string TestOutputPath = "TestOutput";

    // Signatures from the ZIP File Format Specification.
    private const uint LocalFileHeaderSignature = 0x04034b50;

    [SetUp]
    public void Setup() => Directory.CreateDirectory(TestOutputPath);

    private static string GetTestOutputPath(string testName)
    {
        var folder = Path.Combine(TestOutputPath, testName);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static readonly string[] SyntheticEntries =
        ["tileset.json", "LOD-0/a.b3dm", "LOD-0/b.b3dm", "root.b3dm"];

    private static string CreateSyntheticTileset(string testName)
    {
        var dir = GetTestOutputPath(testName + "-src");
        File.WriteAllText(Path.Combine(dir, "tileset.json"),
            "{\"asset\":{\"version\":\"1.0\"},\"geometricError\":1.0,\"root\":{\"geometricError\":0}}");
        var lod0 = Path.Combine(dir, "LOD-0");
        Directory.CreateDirectory(lod0);
        File.WriteAllBytes(Path.Combine(lod0, "a.b3dm"), CreateBytes(2048, 1));
        File.WriteAllBytes(Path.Combine(lod0, "b.b3dm"), CreateBytes(4096, 2));
        File.WriteAllBytes(Path.Combine(dir, "root.b3dm"), CreateBytes(1024, 3));
        return dir;
    }

    private static byte[] CreateBytes(int length, int seed)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static byte[] ReadIndexBytes(string archivePath)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        var index = zip.GetEntry(ThreeTzArchive.IndexFileName);
        index.ShouldNotBeNull();
        using var stream = index!.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static long? FindOffset(byte[] index, byte[] hash)
    {
        for (var i = 0; i < index.Length; i += 24)
            if (index.AsSpan(i, 16).SequenceEqual(hash))
                return (long)BinaryPrimitives.ReadUInt64LittleEndian(index.AsSpan(i + 16, 8));
        return null;
    }

    [Test]
    public void CreateFromDirectory_ProducesArchiveWithExpectedEntries()
    {
        var name = nameof(CreateFromDirectory_ProducesArchiveWithExpectedEntries);
        var src = CreateSyntheticTileset(name);
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        var count = ThreeTzArchive.CreateFromDirectory(src, archive);

        count.ShouldBe(4);
        File.Exists(archive).ShouldBeTrue();

        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in SyntheticEntries)
            zip.GetEntry(entry).ShouldNotBeNull($"missing entry '{entry}'");
        zip.GetEntry(ThreeTzArchive.IndexFileName).ShouldNotBeNull();
    }

    [Test]
    public void CreateFromDirectory_IndexIsLastEntry()
    {
        var name = nameof(CreateFromDirectory_IndexIsLastEntry);
        var src = CreateSyntheticTileset(name);
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        ThreeTzArchive.CreateFromDirectory(src, archive);

        using var zip = ZipFile.OpenRead(archive);
        zip.Entries[^1].FullName.ShouldBe(ThreeTzArchive.IndexFileName);
    }

    [Test]
    public void CreateFromDirectory_IndexIsStoredAndSized()
    {
        var name = nameof(CreateFromDirectory_IndexIsStoredAndSized);
        var src = CreateSyntheticTileset(name);
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        ThreeTzArchive.CreateFromDirectory(src, archive);

        using var zip = ZipFile.OpenRead(archive);
        var index = zip.GetEntry(ThreeTzArchive.IndexFileName);
        index.ShouldNotBeNull();
        // Stored (uncompressed) => compressed length equals the actual length.
        index!.CompressedLength.ShouldBe(index.Length);
        // 24 bytes per content entry (the index excludes itself).
        index.Length.ShouldBe(SyntheticEntries.Length * 24L);
    }

    [Test]
    public void CreateFromDirectory_IndexEntriesAreSortedAndPointToLocalHeaders()
    {
        var name = nameof(CreateFromDirectory_IndexEntriesAreSortedAndPointToLocalHeaders);
        var src = CreateSyntheticTileset(name);
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        ThreeTzArchive.CreateFromDirectory(src, archive);

        var index = ReadIndexBytes(archive);
        (index.Length % 24).ShouldBe(0);
        var entryCount = index.Length / 24;
        entryCount.ShouldBe(SyntheticEntries.Length);

        var raw = File.ReadAllBytes(archive);

        ulong prevHi = 0, prevLo = 0;
        var first = true;
        for (var i = 0; i < entryCount; i++)
        {
            var span = index.AsSpan(i * 24, 24);
            var hi = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0, 8));
            var lo = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(8, 8));
            var offset = (int)BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(16, 8));

            if (!first)
                (hi > prevHi || (hi == prevHi && lo >= prevLo))
                    .ShouldBeTrue("index entries must be sorted ascending by MD5 (little-endian hi, lo)");
            prevHi = hi;
            prevLo = lo;
            first = false;

            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset, 4))
                .ShouldBe(LocalFileHeaderSignature, "each index offset must point to a local file header");
        }

        // Every content path's MD5 must be present in the index.
        foreach (var entry in SyntheticEntries)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(entry));
            FindOffset(index, hash).ShouldNotBeNull($"index should contain '{entry}'");
        }
    }

    [Test]
    public void CreateFromDirectory_IndexLocatesTilesetJson()
    {
        var name = nameof(CreateFromDirectory_IndexLocatesTilesetJson);
        var src = CreateSyntheticTileset(name);
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        ThreeTzArchive.CreateFromDirectory(src, archive);

        var index = ReadIndexBytes(archive);
        var raw = File.ReadAllBytes(archive);

        var offset = FindOffset(index, MD5.HashData(Encoding.UTF8.GetBytes("tileset.json")));
        offset.ShouldNotBeNull();

        var pos = (int)offset!.Value;
        BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(pos, 4)).ShouldBe(LocalFileHeaderSignature);
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(pos + 26, 2));
        Encoding.UTF8.GetString(raw, pos + 30, nameLength).ShouldBe("tileset.json");
    }

    [Test]
    public void CreateFromDirectory_LocalHeadersCarrySizesWithoutDataDescriptor()
    {
        // The 3TZ spec requires every local file header to carry crc32 + sizes,
        // i.e. general-purpose bit flag 3 (0x0008, "data descriptor") must be unset.
        var name = nameof(CreateFromDirectory_LocalHeadersCarrySizesWithoutDataDescriptor);
        var src = CreateSyntheticTileset(name);
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        ThreeTzArchive.CreateFromDirectory(src, archive);

        var index = ReadIndexBytes(archive);
        var raw = File.ReadAllBytes(archive);

        for (var i = 0; i < index.Length; i += 24)
        {
            var offset = (int)BinaryPrimitives.ReadUInt64LittleEndian(index.AsSpan(i + 16, 8));
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset, 4)).ShouldBe(LocalFileHeaderSignature);

            var flags = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(offset + 6, 2));
            (flags & 0x0008).ShouldBe(0, "local file header must not use a data descriptor");
        }
    }

    [TestCase(CompressionLevel.Optimal)]
    [TestCase(CompressionLevel.NoCompression)]
    [TestCase(CompressionLevel.SmallestSize)]
    public void CreateFromDirectory_ContentRoundTrips(CompressionLevel level)
    {
        var name = nameof(CreateFromDirectory_ContentRoundTrips) + level;
        var src = CreateSyntheticTileset(name);
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        ThreeTzArchive.CreateFromDirectory(src, archive, level);

        using var zip = ZipFile.OpenRead(archive);
        foreach (var rel in SyntheticEntries)
        {
            var entry = zip.GetEntry(rel);
            entry.ShouldNotBeNull();
            using var stream = entry!.Open(); // reading validates the CRC32
            using var ms = new MemoryStream();
            stream.CopyTo(ms);

            var sourcePath = Path.Combine(src, rel.Replace('/', Path.DirectorySeparatorChar));
            ms.ToArray().ShouldBe(File.ReadAllBytes(sourcePath));
        }
    }

    [Test]
    public void CreateFromDirectory_MissingTilesetJson_Throws()
    {
        var name = nameof(CreateFromDirectory_MissingTilesetJson_Throws);
        var src = GetTestOutputPath(name + "-src");
        File.WriteAllBytes(Path.Combine(src, "root.b3dm"), CreateBytes(16, 9));
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        Should.Throw<ArgumentException>(() => ThreeTzArchive.CreateFromDirectory(src, archive));
    }

    [Test]
    public void CreateFromDirectory_EntryNameContaining3tz_Throws()
    {
        var name = nameof(CreateFromDirectory_EntryNameContaining3tz_Throws);
        var src = GetTestOutputPath(name + "-src");
        File.WriteAllText(Path.Combine(src, "tileset.json"), "{}");
        File.WriteAllBytes(Path.Combine(src, "nested.3tz.bin"), CreateBytes(16, 1));
        var archive = Path.Combine(GetTestOutputPath(name), "out.3tz");

        Should.Throw<ArgumentException>(() => ThreeTzArchive.CreateFromDirectory(src, archive));
    }

    [TestCase(0, CompressionLevel.NoCompression)]
    [TestCase(1, CompressionLevel.Fastest)]
    [TestCase(3, CompressionLevel.Fastest)]
    [TestCase(4, CompressionLevel.Optimal)]
    [TestCase(6, CompressionLevel.Optimal)]
    [TestCase(7, CompressionLevel.SmallestSize)]
    [TestCase(9, CompressionLevel.SmallestSize)]
    public void ResolveCompressionLevel_MapsRange(int level, CompressionLevel expected)
        => ThreeTzArchive.ResolveCompressionLevel(level).ShouldBe(expected);

    [TestCase(-1)]
    [TestCase(10)]
    public void ResolveCompressionLevel_OutOfRange_Throws(int level)
        => Should.Throw<ArgumentOutOfRangeException>(() => ThreeTzArchive.ResolveCompressionLevel(level));

    [Test]
    public void CreateFromDirectory_PacksRealTilesetOutput()
    {
        var name = nameof(CreateFromDirectory_PacksRealTilesetOutput);
        var tilesetDir = GetTestOutputPath(name + "-tileset");

        var boundsMapper = (from file in Directory.GetFiles("TestData/Tile1/LOD-0", "*.json")
            let bounds = JsonConvert.DeserializeObject<BoxDTO>(File.ReadAllText(file))
            select new
            {
                Bounds = new Box3(new Vertex3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z),
                    new Vertex3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z)),
                Name = Path.GetFileNameWithoutExtension(file)
            }).ToDictionary(item => item.Name, item => item.Bounds);

        StagesFacade.Tile("TestData/Tile1", tilesetDir, 1, 100, [boundsMapper]);

        File.Exists(Path.Combine(tilesetDir, "tileset.json")).ShouldBeTrue();

        var archive = Path.Combine(GetTestOutputPath(name), "tileset.3tz");
        ThreeTzArchive.CreateFromDirectory(tilesetDir, archive);

        using var zip = ZipFile.OpenRead(archive);
        zip.GetEntry("tileset.json").ShouldNotBeNull();
        zip.Entries[^1].FullName.ShouldBe(ThreeTzArchive.IndexFileName);

        // Every content entry reads back without a CRC error.
        foreach (var entry in zip.Entries.Where(e => e.FullName != ThreeTzArchive.IndexFileName))
        {
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Length.ShouldBe(entry.Length);
        }
    }
}
