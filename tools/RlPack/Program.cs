using System.Security.Cryptography;
using Rookline.AssetProtection;

try
{
    string Arg(string name){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:throw new ArgumentException("Missing "+name);}
    if(args[0]=="key")
    {
        string path=Path.GetFullPath(Arg("--key")),generated=Path.GetFullPath(Arg("--generated"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);Directory.CreateDirectory(Path.GetDirectoryName(generated)!);
        if(!File.Exists(path))
        {
            using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            file.Write(RandomNumberGenerator.GetBytes(32));
        }
        byte[] key=File.ReadAllBytes(path);if(key.Length!=32)throw new InvalidDataException("Invalid local build-key length.");
        string product=Arg("--product");
        if(product.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='.'))throw new ArgumentException("Invalid product identity.");
        string source="namespace Rookline.AssetProtection; internal static class ProductAssetKey { internal static readonly byte[] Bytes=System.Convert.FromBase64String(\""+Convert.ToBase64String(key)+"\"); } internal static class ProductAssetIdentity { internal const string Name=\""+product+"\"; }\n";
        if(!File.Exists(generated)||File.ReadAllText(generated)!=source)File.WriteAllText(generated,source);
        Console.WriteLine("RLAR v2 build key ready (key bytes not logged).");
    }
    else if(args[0]=="pack")
    {
        string root=Path.GetFullPath(Arg("--source")),output=Path.GetFullPath(Arg("--output")),product=Arg("--product");
        string[] roots=Arg("--roots").Split(';',StringSplitOptions.RemoveEmptyEntries);
        RlArchive.Pack(root,output,product,File.ReadAllBytes(Arg("--key")),roots);
        Console.WriteLine("PASS: encrypted .rl archive and every decrypted asset hash verified.");
        if(args.Contains("--remove-loose"))
        {
            // Remove only the explicit asset roots inside a fresh publish folder.
            if(!File.Exists(Path.Combine(root,"SensorPanel.runtimeconfig.json"))&&!File.Exists(Path.Combine(root,"FleetStudio.Desktop.runtimeconfig.json")))throw new InvalidOperationException("Loose cleanup requires an application publish directory.");
            if(!output.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Archive must be inside the publish directory.");
            foreach(string item in roots)
            {
                RlArchive.ValidatePath(item);string path=Path.GetFullPath(Path.Combine(root,item));
                if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!new[]{"FleetViewer","SolarNavigation","FleetStudio","Models","Ships","AORUS"}.Contains(item))throw new InvalidOperationException("Unapproved asset cleanup root.");
                Directory.Delete(path,true);
            }
        }
    }
    else if(args[0]=="verify")
    {
        string root=Path.GetFullPath(Arg("--source"));
        var archive=new RlArchive(Arg("--archive"),Arg("--product"),File.ReadAllBytes(Arg("--key")));
        int count=0;
        foreach(string path in archive.Paths)
        {
            if(!CryptographicOperations.FixedTimeEquals(SHA256.HashData(archive.Read(path)),SHA256.HashData(File.ReadAllBytes(Path.Combine(root,path)))))throw new InvalidDataException("Protected asset/source mismatch: "+path);
            count++;
        }
        Console.WriteLine("PASS: "+count+" encrypted assets match their exact source bytes.");
    }
    else if(args[0]=="self-test")RlArchiveChecks.Run(Arg("--output"));
    else throw new ArgumentException("Unknown RLAR command.");
    return 0;
}
catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}
