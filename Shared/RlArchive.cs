using System.IO;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Rookline.AssetProtection;

// RLAR v2 is a versioned, authenticated asset container. Runtime keys are
// necessarily recoverable from a client application: this is copying deterrence.
public sealed class RlArchive
{
    private static readonly byte[] Magic="RLAR\x02\0\0\0"u8.ToArray();
    private const int MaximumFile=256*1024*1024, MaximumIndex=2*1024*1024;
    private sealed record Entry(string Path,long Offset,int Stored,int Original,bool Gzip);
    private sealed record Index(string Product,Entry[] Entries);
    private readonly Dictionary<string,Entry> entries;
    private readonly string archivePath,product;
    private readonly byte[] dataKey;
    private readonly long dataStart;
    public IEnumerable<string> Paths=>entries.Keys;

    public RlArchive(string path,string product,byte[] key)
    {
        if(key.Length!=32)throw new ArgumentException("An RLAR v2 key must be 32 bytes.");
        archivePath=path;this.product=product;
        using var file=File.OpenRead(path);using var reader=new BinaryReader(file);
        if(!reader.ReadBytes(8).SequenceEqual(Magic))throw new InvalidDataException("Unsupported .rl archive version.");
        byte[] salt=Exact(reader,16),nonce=Exact(reader,12),tag=Exact(reader,16);
        int length=reader.ReadInt32();
        if(length<1||length>MaximumIndex||length>file.Length-file.Position)throw new InvalidDataException("Invalid .rl index length.");
        var indexKey=Derive(key,salt,product+"/index");
        byte[] json=new byte[length];
        Index index;
        try
        {
            using(var aes=new AesGcm(indexKey,16))aes.Decrypt(nonce,Exact(reader,length),tag,json,IndexAad(salt,length));
            index=JsonSerializer.Deserialize<Index>(json)??throw new InvalidDataException("Missing .rl index.");
        }
        finally{CryptographicOperations.ZeroMemory(indexKey);CryptographicOperations.ZeroMemory(json);}
        if(index.Product!=product||index.Entries.Length is <1 or >10000)throw new InvalidDataException("Wrong .rl product or entry count.");
        dataStart=file.Position;
        entries=new(StringComparer.OrdinalIgnoreCase);
        long end=0;
        foreach(var entry in index.Entries)
        {
            ValidatePath(entry.Path);
            if(entry.Offset!=end||entry.Original is <0 or >MaximumFile||entry.Stored is <28 or >MaximumFile+28
                ||entry.Stored>file.Length-dataStart-end||!entries.TryAdd(entry.Path,entry))throw new InvalidDataException("Invalid .rl entry table.");
            end=checked(end+entry.Stored);
        }
        if(dataStart+end!=file.Length)throw new InvalidDataException("Unexpected .rl trailing data.");
        dataKey=Derive(key,salt,product+"/data");
    }
    public bool Contains(string path){ValidatePath(path);return entries.ContainsKey(path);}
    public byte[] Read(string path)
    {
        ValidatePath(path);
        if(!entries.TryGetValue(path,out var entry))throw new FileNotFoundException("Asset absent from .rl archive.",path);
        using var file=File.OpenRead(archivePath);file.Position=dataStart+entry.Offset;
        using var reader=new BinaryReader(file);
        byte[] nonce=Exact(reader,12),tag=Exact(reader,16),plain=Exact(reader,entry.Stored-28);
        // Exact buffer overlap is supported: authenticate/decrypt in place so a
        // large model does not allocate a second model-sized managed buffer.
        try{using var aes=new AesGcm(dataKey,16);aes.Decrypt(nonce,plain,tag,plain,DataAad(product,entry));}
        catch{CryptographicOperations.ZeroMemory(plain);throw;}
        if(!entry.Gzip)
        {
            if(plain.Length!=entry.Original)throw new InvalidDataException("Asset length mismatch.");
            return plain;
        }
        try
        {
            using var gzip=new GZipStream(new MemoryStream(plain,false),CompressionMode.Decompress);
            byte[] raw=new byte[entry.Original];gzip.ReadExactly(raw);
            if(gzip.ReadByte()!=-1)throw new InvalidDataException("Asset exceeds its authenticated length.");
            return raw;
        }
        finally{CryptographicOperations.ZeroMemory(plain);}
    }
    public static void ValidatePath(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||path.Length>1024||path.Contains('\\')||path.Contains(':')||path.Contains('\0')
            ||path.Split('/').Any(part=>part is "" or "." or ".."))throw new InvalidDataException("Invalid .rl asset path.");
    }
    public static void Pack(string source,string output,string product,byte[] key,IEnumerable<string> roots)
    {
        if(key.Length!=32||string.IsNullOrWhiteSpace(product))throw new ArgumentException("Missing product or 256-bit build key.");
        source=System.IO.Path.GetFullPath(source);output=System.IO.Path.GetFullPath(output);
        var files=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string root in roots)
        {
            ValidatePath(root);
            string directory=System.IO.Path.Combine(source,root);
            if(!Directory.Exists(directory))throw new DirectoryNotFoundException(directory);
            if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Asset roots cannot be links.");
            foreach(var file in new DirectoryInfo(directory).EnumerateFiles("*",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}))
            {
                string relative=System.IO.Path.GetRelativePath(source,file.FullName).Replace('\\','/');ValidatePath(relative);
                if(file.Length>MaximumFile||!files.TryAdd(relative,file.FullName))throw new InvalidDataException("Oversized/duplicate asset: "+relative);
            }
        }
        if(files.Count is <1 or >10000)throw new InvalidDataException("Invalid asset count.");
        byte[] salt=RandomNumberGenerator.GetBytes(16),dataKey=Derive(key,salt,product+"/data"),indexKey=Derive(key,salt,product+"/index");
        string spool=output+"."+Guid.NewGuid().ToString("N")+".tmp";
        var entries=new List<Entry>();var nonces=new HashSet<string>();
        try
        {
            using(var data=File.Create(spool))
            foreach(var file in files)
            {
                byte[] raw=File.ReadAllBytes(file.Value),compressed;
                bool gzip=System.IO.Path.GetExtension(file.Key).ToLowerInvariant() is not (".gz" or ".png" or ".jpg" or ".jpeg" or ".woff2");
                if(gzip)
                {
                    using var buffer=new MemoryStream();using(var zip=new GZipStream(buffer,CompressionLevel.Optimal,true))zip.Write(raw);
                    compressed=buffer.ToArray();
                    if(compressed.Length>=raw.Length){compressed=raw;gzip=false;}
                }
                else compressed=raw;
                var entry=new Entry(file.Key,data.Position,checked(compressed.Length+28),raw.Length,gzip);
                byte[] nonce;do{nonce=RandomNumberGenerator.GetBytes(12);}while(!nonces.Add(Convert.ToHexString(nonce)));
                byte[] cipher=new byte[compressed.Length],tag=new byte[16];
                using(var aes=new AesGcm(dataKey,16))aes.Encrypt(nonce,compressed,cipher,tag,DataAad(product,entry));
                data.Write(nonce);data.Write(tag);data.Write(cipher);entries.Add(entry);
                CryptographicOperations.ZeroMemory(raw);if(!ReferenceEquals(raw,compressed))CryptographicOperations.ZeroMemory(compressed);
            }
            byte[] json=JsonSerializer.SerializeToUtf8Bytes(new Index(product,entries.ToArray()));
            if(json.Length>MaximumIndex)throw new InvalidDataException("Index too large.");
            byte[] indexNonce=RandomNumberGenerator.GetBytes(12),indexTag=new byte[16],indexCipher=new byte[json.Length];
            using(var aes=new AesGcm(indexKey,16))aes.Encrypt(indexNonce,json,indexCipher,indexTag,IndexAad(salt,json.Length));
            using(var archive=File.Create(output))
            using(var writer=new BinaryWriter(archive))
            {
                writer.Write(Magic);writer.Write(salt);writer.Write(indexNonce);writer.Write(indexTag);writer.Write(indexCipher.Length);writer.Write(indexCipher);
                using var data=File.OpenRead(spool);data.CopyTo(archive);
            }
            var verify=new RlArchive(output,product,key);
            foreach(var entry in files)
                if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(verify.Read(entry.Key)),SHA256.HashData(File.ReadAllBytes(entry.Value))))throw new InvalidDataException("Archive round-trip mismatch: "+entry.Key);
        }
        finally
        {
            // Only this invocation's exact temporary ciphertext spool is removed.
            if(File.Exists(spool))File.Delete(spool);
            CryptographicOperations.ZeroMemory(dataKey);CryptographicOperations.ZeroMemory(indexKey);
        }
    }
    private static byte[] Exact(BinaryReader reader,int length)
    {var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException("Truncated .rl archive.");return bytes;}
    private static byte[] Derive(byte[] key,byte[] salt,string info)=>HKDF.DeriveKey(HashAlgorithmName.SHA256,key,32,salt,Encoding.UTF8.GetBytes(info));
    private static byte[] IndexAad(byte[] salt,int length)
    {byte[] result=new byte[28];Magic.CopyTo(result,0);salt.CopyTo(result,8);BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(24),length);return result;}
    private static byte[] DataAad(string product,Entry entry)=>Encoding.UTF8.GetBytes(product+"\n"+entry.Path+"\n"+entry.Offset.ToString(CultureInfo.InvariantCulture)
        +"\n"+entry.Original.ToString(CultureInfo.InvariantCulture)+"\n"+entry.Stored.ToString(CultureInfo.InvariantCulture)+"\n"+(entry.Gzip?"1":"0"));
}
