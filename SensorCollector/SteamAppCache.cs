using System.Text;

namespace SensorCollector;

// Read-only v40/v41 local Steam cache. Header/entry layout documented by
// https://github.com/xPaw/SteamAppInfo. Unknown formats fail closed.
internal static class SteamAppCache
{
    internal sealed record Metadata(string Name, string Type);
    private enum Key : byte { Other, AppInfo, Common, Name, Type }
    public static Dictionary<uint, Metadata> Read(string path, HashSet<uint> wanted)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (file.Length > 64 * 1024 * 1024) throw new InvalidDataException("Steam cache exceeds the supported size.");
        long length=file.Length;var written=File.GetLastWriteTimeUtc(path);
        using var reader = new BinaryReader(file, Encoding.UTF8, leaveOpen: true);
        var result=Read(reader,wanted);
        if(file.Length!=length||File.GetLastWriteTimeUtc(path)!=written)throw new IOException("Steam cache changed while reading; retry on the next catalog refresh.");
        return result;
    }

    internal static Dictionary<uint, Metadata> Read(BinaryReader reader, HashSet<uint> wanted)
    {
        uint version = reader.ReadUInt32(); reader.ReadUInt32();
        if (version is not (0x07564428 or 0x07564429)) throw new InvalidDataException("Unsupported Steam appinfo version.");
        Key[]? keys = null;
        long entriesEnd = reader.BaseStream.Length;
        if (version == 0x07564429)
        {
            long tableOffset = reader.ReadInt64(), entryOffset = reader.BaseStream.Position;
            if (tableOffset < entryOffset || tableOffset >= reader.BaseStream.Length) throw new InvalidDataException("Invalid Steam string table.");
            entriesEnd = tableOffset; reader.BaseStream.Position = tableOffset;
            uint count = reader.ReadUInt32();
            if (count > 100000) throw new InvalidDataException("Invalid Steam string count.");
            keys = new Key[count];
            for (int i = 0; i < count; i++) keys[i] = ReadKey(reader,reader.BaseStream.Length);
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
                string? name=null,type=null;
                Object(reader,keys,0,0,end,ref name,ref type);
                if(name is not null&&type is not null)metadata[appId]=new(name,type);
            }
            reader.BaseStream.Position = end;
        }
        return metadata;
    }

    // Only common/name and common/type are needed for game eligibility. Skip
    // other values in place instead of copying the cache and materializing
    // each application's full launch/configuration tree every 30 seconds.
    private static void Object(BinaryReader reader,Key[]? keys,int depth,int context,long end,ref string? name,ref string? type)
    {
        if (depth > 32) throw new InvalidDataException("Steam cache nesting exceeds the limit.");
        while (true)
        {
            if(reader.BaseStream.Position>=end)throw new InvalidDataException("Steam object exceeds its entry.");
            byte kind = reader.ReadByte(); if (kind == 8) return;
            Key key;
            if (keys is null) key = ReadKey(reader,end);
            else
            {
                if(reader.BaseStream.Position+4>end)throw new InvalidDataException("Steam key exceeds its entry.");
                uint index = reader.ReadUInt32(); if (index >= keys.Length) throw new InvalidDataException("Invalid Steam key index."); key = keys[index];
            }
            if(kind==0)
            {
                int next=context==0&&key==Key.AppInfo?1:context==1&&key==Key.Common?2:3;
                Object(reader,keys,depth+1,next,end,ref name,ref type);
            }
            else if(kind==1)
            {
                bool needed=context==2&&(key is Key.Name or Key.Type);
                string value=String(reader,needed,end);
                if(needed){if(key==Key.Name)name=value;else type=value;}
            }
            else
            {
                int bytes=kind switch{2 or 3 or 4 or 6=>4,7 or 10=>8,_=>throw new InvalidDataException($"Unsupported Steam value type {kind}.")};
                if(reader.BaseStream.Position+bytes>end)throw new InvalidDataException("Steam value exceeds its entry.");
                reader.BaseStream.Position+=bytes;
            }
        }
    }
    private static Key ReadKey(BinaryReader reader,long end)
    {
        // The grammar needs four ASCII keys, not a managed string for every
        // launch/configuration key in Steam's complete string table.
        Span<byte> key=stackalloc byte[7];
        for(int length=0;length<1024*1024;length++)
        {
            if(reader.BaseStream.Position>=end)throw new InvalidDataException("Steam key exceeds its entry.");
            byte next=reader.ReadByte();
            if(next==0)
            {
                if(length>key.Length)return Key.Other;
                var value=key[..length];
                return value.SequenceEqual("appinfo"u8)?Key.AppInfo:value.SequenceEqual("common"u8)?Key.Common:
                    value.SequenceEqual("name"u8)?Key.Name:value.SequenceEqual("type"u8)?Key.Type:Key.Other;
            }
            if(length<key.Length)key[length]=next is >=(byte)'A' and <=(byte)'Z'?(byte)(next+32):next;
        }
        throw new InvalidDataException("Steam cache key exceeds the limit.");
    }
    private static string String(BinaryReader reader,bool keep=true,long end=long.MaxValue)
    {
        using var bytes = keep?new MemoryStream():null;
        for (int length = 0; length < 1024 * 1024; length++)
        {
            if(reader.BaseStream.Position>=end)throw new InvalidDataException("Steam string exceeds its entry.");
            byte next = reader.ReadByte(); if (next == 0) return bytes is null?"":Encoding.UTF8.GetString(bytes.ToArray());bytes?.WriteByte(next);
        }
        throw new InvalidDataException("Steam cache string exceeds the limit.");
    }
}
