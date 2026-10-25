using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rookline.AssetProtection;

internal static class RlArchiveChecks
{
    internal static void Run(string output)
    {
        string directory=Path.Combine(Path.GetFullPath(output),Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory,"input","assets"));
        string source=Path.Combine(directory,"input"),archive=Path.Combine(directory,"assets.rl");
        byte[] key=RandomNumberGenerator.GetBytes(32),model=RandomNumberGenerator.GetBytes(1024*1024);
        byte[] script=Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("export const asset='private source fixture';\n",500)));
        File.WriteAllBytes(Path.Combine(source,"assets","model.glb.gz"),model);
        File.WriteAllBytes(Path.Combine(source,"assets","viewer.js"),script);
        RlArchive.Pack(source,archive,"Test.Product",key,["assets"]);
        var reader=new RlArchive(archive,"Test.Product",key);
        if(!reader.Read("assets/model.glb.gz").SequenceEqual(model)||!reader.Read("assets/viewer.js").SequenceEqual(script))throw new Exception("Round-trip failed.");
        var checks=new List<string>{"binary/text round-trip"};
        long before=GC.GetAllocatedBytesForCurrentThread();
        byte[] loaded=reader.Read("assets/model.glb.gz");
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        if(loaded.Length!=model.Length||allocated>model.Length+128*1024)throw new Exception("Large asset read allocated a second model-sized buffer.");
        checks.Add("one model-sized decrypt buffer");
        var reusable=new byte[model.Length+64];
        before=GC.GetAllocatedBytesForCurrentThread();
        int read=reader.ReadInto("assets/model.glb.gz",reusable);
        allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        if(read!=model.Length||!reusable.AsSpan(0,read).SequenceEqual(model)||allocated>32*1024)
            throw new Exception("Reusable asset read changed bytes or allocated a payload-sized buffer.");
        if(reader.ReadInto("assets/viewer.js",reusable)!=script.Length||!reusable.AsSpan(0,script.Length).SequenceEqual(script))
            throw new Exception("Reusable compressed-text read changed source bytes.");
        checks.Add("caller-owned authenticated binary/text buffer");
        void Reject(string name,Action action)
        {
            try{action();}catch(Exception ex) when(ex is CryptographicException or InvalidDataException or EndOfStreamException or FileNotFoundException){checks.Add(name);return;}
            throw new Exception("Rejected-input check was accepted: "+name);
        }
        Reject("wrong product",()=>new RlArchive(archive,"Other.Product",key));
        Reject("wrong key",()=>new RlArchive(archive,"Test.Product",RandomNumberGenerator.GetBytes(32)));
        Reject("path traversal",()=>reader.Read("assets/../viewer.js"));
        Reject("missing asset",()=>reader.Read("assets/missing.js"));
        Reject("undersized destination",()=>reader.ReadInto("assets/model.glb.gz",new byte[64]));
        Reject("buffer path traversal",()=>reader.ReadInto("assets/../viewer.js",reusable));
        byte[] original=File.ReadAllBytes(archive),tampered=original.ToArray();tampered[40]^=1;
        string bad=Path.Combine(directory,"tampered.rl");File.WriteAllBytes(bad,tampered);
        Reject("authenticated index tamper",()=>new RlArchive(bad,"Test.Product",key));
        tampered=original.ToArray();tampered[^1]^=1;File.WriteAllBytes(bad,tampered);
        Reject("authenticated payload tamper",()=>new RlArchive(bad,"Test.Product",key).Read("assets/viewer.js"));
        Reject("buffer authenticated payload tamper",()=>new RlArchive(bad,"Test.Product",key).ReadInto("assets/viewer.js",reusable));
        File.WriteAllBytes(bad,original[..6]);Reject("truncation",()=>new RlArchive(bad,"Test.Product",key));
        string second=Path.Combine(directory,"second.rl");RlArchive.Pack(source,second,"Test.Product",key,["assets"]);
        if(original.SequenceEqual(File.ReadAllBytes(second)))throw new Exception("Archive nonce/salt reused.");
        if(Encoding.UTF8.GetString(original).Contains("private source fixture",StringComparison.Ordinal))throw new Exception("Plaintext fixture leaked.");
        checks.Add("fresh archive salt/nonces");checks.Add("no plaintext fixture in archive");
        var receipt=new{Passed=true,Checks=checks,Directory=directory};
        Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("PASS: "+checks.Count+" RLAR v2 round-trip/rejection checks.");
    }
}
