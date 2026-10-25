using System.IO;
using Microsoft.Web.WebView2.Core;

namespace Rookline.AssetProtection;

internal static class AppAssets
{
    private static readonly Lazy<RlArchive?> archive=new(()=>File.Exists(Path.Combine(AppContext.BaseDirectory,"assets.rl"))
        ? new RlArchive(Path.Combine(AppContext.BaseDirectory,"assets.rl"),ProductAssetIdentity.Name,ProductAssetKey.Bytes):null);
    internal static bool Protected=>archive.Value is not null;
    internal static bool Exists(string path)
    {RlArchive.ValidatePath(path);return archive.Value?.Contains(path)??File.Exists(Path.Combine(AppContext.BaseDirectory,path));}
    internal static byte[] Read(string path)
    {RlArchive.ValidatePath(path);return archive.Value?.Read(path)??File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,path));}
    internal static int ReadInto(string path,Span<byte> destination)
    {
        RlArchive.ValidatePath(path);
        if(archive.Value is {} packed)return packed.ReadInto(path,destination);
        using var file=File.OpenRead(Path.Combine(AppContext.BaseDirectory,path));
        if(file.Length>destination.Length)throw new InvalidDataException("Asset destination is too small.");
        int length=checked((int)file.Length);file.ReadExactly(destination[..length]);return length;
    }
    internal static Stream Open(string path)=>new MemoryStream(Read(path),false);
    internal static void Host(CoreWebView2 core,string host,string root)
    {
        if(!Protected)
        {
            core.SetVirtualHostNameToFolderMapping(host,Path.Combine(AppContext.BaseDirectory,root),CoreWebView2HostResourceAccessKind.Deny);
            return;
        }
        core.Settings.AreDevToolsEnabled=false;
        core.AddWebResourceRequestedFilter("https://"+host+"/*",CoreWebView2WebResourceContext.All);
        core.WebResourceRequested+=async(_,e)=>
        {
            var deferral=e.GetDeferral();
            try
            {
                var uri=new Uri(e.Request.Uri);
                if(uri.Scheme!="https"||uri.Host!=host||e.Request.Method is not ("GET" or "HEAD"))throw new InvalidDataException("Unsupported asset request.");
                string relative=Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
                RlArchive.ValidatePath(relative);
                var bytes=await Task.Run(()=>Read(root+"/"+relative));
                string type=Path.GetExtension(relative).ToLowerInvariant() switch
                {
                    ".html"=>"text/html; charset=utf-8",".js"=>"application/javascript; charset=utf-8",
                    ".css"=>"text/css; charset=utf-8",".json"=>"application/json; charset=utf-8",
                    ".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",".svg"=>"image/svg+xml",
                    ".woff2"=>"font/woff2",".woff"=>"font/woff",".ttf"=>"font/ttf",_=>"application/octet-stream"
                };
                e.Response=core.Environment.CreateWebResourceResponse(e.Request.Method=="HEAD"?null:new MemoryStream(bytes,false),200,"OK",
                    "Content-Type: "+type+"\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n");
            }
            catch(FileNotFoundException){e.Response=core.Environment.CreateWebResourceResponse(null,404,"Not Found","Cache-Control: no-store\r\n");}
            catch(Exception){e.Response=core.Environment.CreateWebResourceResponse(null,500,"Protected asset unavailable","Cache-Control: no-store\r\n");}
            finally{deferral.Complete();}
        };
    }
}
