using System.Text;

namespace SensorCollector;

// Read-only v40/v41 local Steam cache. Header/entry layout documented by
// https://github.com/xPaw/SteamAppInfo. Unknown formats fail closed.
internal static class SteamAppCache
{
    internal sealed record Metadata(string Name, string Type);
    public static Dictionary<uint, Metadata> Read(string path, HashSet<uint> wanted)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length > 64 * 1024 * 1024) throw new InvalidDataException("Steam cache exceeds the supported size.");
        using var snapshot = new MemoryStream(); file.CopyTo(snapshot); snapshot.Position = 0;
        using var reader = new BinaryReader(snapshot, Encoding.UTF8, leaveOpen: true);
        return Read(reader, wanted);
    }

    internal static Dictionary<uint, Metadata> Read(BinaryReader reader, HashSet<uint> wanted)
    {
        uint version = reader.ReadUInt32(); reader.ReadUInt32();
        if (version is not (0x07564428 or 0x07564429)) throw new InvalidDataException("Unsupported Steam appinfo version.");
        string[]? keys = null;
        long entriesEnd = reader.BaseStream.Length;
        if (version == 0x07564429)
        {
            long tableOffset = reader.ReadInt64(), entryOffset = reader.BaseStream.Position;
            if (tableOffset < entryOffset || tableOffset >= reader.BaseStream.Length) throw new InvalidDataException("Invalid Steam string table.");
            entriesEnd = tableOffset; reader.BaseStream.Position = tableOffset;
            uint count = reader.ReadUInt32();
            if (count > 100000) throw new InvalidDataException("Invalid Steam string count.");
            keys = new string[count];
            for (int i = 0; i < count; i++) keys[i] = String(reader);
            reader.BaseStream.Position = entryOffset;
        }
        var metadata = new Dictionary<uint, Metadata>();
        while (reader.BaseStream.Position + 4 <= entriesEnd)
        {
            uint appId = reader.ReadUInt32(); if (appId == 0) break;
            uint size = reader.ReadUInt32(); long end = reader.BaseStream.Position + size;
            if (size < 60 || size > 16 * 1024 * 1024 || end > entriesEnd) throw new InvalidDataException("Invalid Steam app entry size.");
            if (wanted.Contains(appId))
            {
                reader.BaseStream.Position += 60; // fixed v40/v41 metadata, including both SHA1 fields
                byte[] payload = reader.ReadBytes((int)(end - reader.BaseStream.Position));
                using var data = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
                var root = Object(data, keys, 0);
                if (root.GetValueOrDefault("appinfo") is Dictionary<string, object> app &&
                    app.GetValueOrDefault("common") is Dictionary<string, object> common &&
                    common.GetValueOrDefault("name") is string name && common.GetValueOrDefault("type") is string type)
                    metadata[appId] = new(name, type);
            }
            reader.BaseStream.Position = end;
        }
        return metadata;
    }

    private static Dictionary<string, object> Object(BinaryReader reader, string[]? keys, int depth)
    {
        if (depth > 32) throw new InvalidDataException("Steam cache nesting exceeds the limit.");
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            byte kind = reader.ReadByte(); if (kind == 8) return values;
            string key;
            if (keys is null) key = String(reader);
            else { uint index = reader.ReadUInt32(); if (index >= keys.Length) throw new InvalidDataException("Invalid Steam key index."); key = keys[index]; }
            object value = kind switch
            {
                0 => Object(reader, keys, depth + 1),
                1 => String(reader),
                2 => reader.ReadInt32(),
                3 => reader.ReadSingle(),
                4 or 6 => reader.ReadUInt32(),
                7 => reader.ReadUInt64(),
                10 => reader.ReadInt64(),
                _ => throw new InvalidDataException($"Unsupported Steam value type {kind}.")
            };
            values[key] = value;
        }
    }
    private static string String(BinaryReader reader)
    {
        using var bytes = new MemoryStream();
        for (int length = 0; length < 1024 * 1024; length++)
        {
            byte next = reader.ReadByte(); if (next == 0) return Encoding.UTF8.GetString(bytes.ToArray()); bytes.WriteByte(next);
        }
        throw new InvalidDataException("Steam cache string exceeds the limit.");
    }
}
