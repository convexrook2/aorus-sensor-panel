using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rookline.AssetProtection;

namespace SensorPanel;

// Asset-preparation output, never a runtime render or a reduced substitute mesh.
// Only the current portrait is cached; ship cycling cannot accumulate decoded images.
internal sealed class ShipPoster : Image
{
    private static (string Id,BitmapSource Image,JsonElement Receipt)? cached;
    internal static void ReleaseCache()=>cached=null;
    private readonly string ship;
    private readonly JsonElement receipt;
    internal ShipPoster(string id)
    {
        ship=ShipProfile.CanonicalId(id);
        var asset=Load(ship);Source=asset.Image;receipt=asset.Receipt;
        Stretch=Stretch.Uniform;IsHitTestVisible=false;
        RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.HighQuality);
        ToolTip=ShipProfile.Resolve(ship).Name+" · Open holotable to inspect in 3D";
    }
    internal static BitmapSource GetImage(string ship)=>Load(ShipProfile.CanonicalId(ship)).Image;
    private static (BitmapSource Image,JsonElement Receipt) Load(string ship)
    {
        if(cached is {} previous && previous.Id==ship)return(previous.Image,previous.Receipt);
        using var index=JsonDocument.Parse(AppAssets.Read("FleetViewer/portraits/index.json"));
        var row=index.RootElement.GetProperty("portraits").EnumerateArray().Single(r=>r.GetProperty("id").GetString()==ship);
        using var catalog=JsonDocument.Parse(AppAssets.Read("FleetViewer/catalog.json"));
        string? modelHash=catalog.RootElement.EnumerateArray().Single(r=>r.GetProperty("id").GetString()==ship).GetProperty("sha256").GetString();
        if(row.GetProperty("sourceSha256").GetString()!=modelHash)
            throw new InvalidDataException("The ship portrait needs regenerating for the current model: "+ship);
        byte[] bytes=AppAssets.Read("FleetViewer/portraits/"+ship+".png");
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=row.GetProperty("sha256").GetString())
            throw new InvalidDataException("Ship portrait checksum mismatch: "+ship);
        using var stream=new MemoryStream(bytes,false);
        var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.StreamSource=stream;bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.EndInit();bitmap.Freeze();
        if(bitmap.PixelWidth!=row.GetProperty("width").GetInt32()||bitmap.PixelHeight!=row.GetProperty("height").GetInt32()
            ||bitmap.PixelWidth<1024||bitmap.PixelHeight<2048)throw new InvalidDataException("Ship portrait resolution mismatch: "+ship);
        var copy=row.Clone();cached=(ship,bitmap,copy);return(bitmap,copy);
    }
    internal object Diagnose()=>new { Id=ship,Presentation="Static three-quarter portrait",LiveRenderer=false,Receipt=receipt };
    internal void Check()
    {if(Source is not BitmapSource { IsFrozen:true,PixelWidth:>=1024,PixelHeight:>=2048 })throw new InvalidOperationException("The full-resolution ship portrait is missing.");}
}
