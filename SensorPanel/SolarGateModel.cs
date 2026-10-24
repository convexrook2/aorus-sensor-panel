using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SensorPanel;

// Native map adaptation of the accepted v3 asset. It shares the source faces,
// but WPF lighting/materials are distinct from the full PBR ship showcase.
internal sealed class SolarGateModel
{
    internal const string AssetPath="FleetViewer/models/SolRing.glb.gz";
    internal const string DecodedSha256="c80193dd8327d6bda2a55664f34617b2910130f6e961ef772dc23293599283d1";
    private const int TextureSize=128;
    private sealed record Part(MeshGeometry3D Mesh,Material Material,bool Aperture);
    private sealed record Definition(Part[] Parts,Model3DGroup Frozen,string PackedHash,int Triangles);
    private static readonly Lazy<Definition> definition=new(Load);
    private static readonly (double X,double Y)[] noiseGrid=MakeNoise();
    private readonly WriteableBitmap field=new(TextureSize,TextureSize,96,96,PixelFormats.Bgra32,null);
    private readonly byte[] pixels=new byte[TextureSize*TextureSize*4];
    private double lastTime=double.NaN;
    private long fieldUpdates;
    internal Model3DGroup Model {get;}=new();
    internal static Model3DGroup FrozenModel=>definition.Value.Frozen;
    internal static string PackedHash=>definition.Value.PackedHash;

    internal SolarGateModel()
    {
        var aperture=new EmissiveMaterial(new ImageBrush(field){Stretch=Stretch.Fill});
        foreach(var part in definition.Value.Parts)
        {
            var material=part.Aperture?aperture:part.Material;
            Model.Children.Add(new GeometryModel3D(part.Mesh,material){BackMaterial=material});
        }
        // Small native inspection lights reveal the rim from either side.
        // The sixteen source portal spill lights remain in the untouched GLB.
        Model.Children.Add(new DirectionalLight(Color.FromRgb(180,198,211),new(.3,-.4,-1)));
        Model.Children.Add(new DirectionalLight(Color.FromRgb(75,112,132),new(-.4,.2,1)));
        Update(0,true);
    }

    internal void Update(double time,bool settle=false)
    {
        if(double.IsFinite(lastTime)&&(time==lastTime||!settle&&Math.Abs(time-lastTime)<.1))return;
        PaintField(pixels,time);field.WritePixels(new Int32Rect(0,0,TextureSize,TextureSize),pixels,TextureSize*4,0);
        lastTime=time;fieldUpdates++;
    }

    internal object Diagnose()=>new{source=AssetPath,decodedSha256=DecodedSha256,packedSha256=PackedHash,
        triangles=definition.Value.Triangles,nativeBatches=definition.Value.Parts.Length,
        aperture="Separate two-sided opaque animated transit field",fieldTime=lastTime,fieldUpdates,
        sourceUnmodified=true,nativeMaterialAdaptation=true};

    private static Definition Load()
    {
        byte[] packed=Rookline.AssetProtection.AppAssets.Read(AssetPath);
        using var source=new MemoryStream(packed,false);using var gzip=new GZipStream(source,CompressionMode.Decompress);
        using var output=new MemoryStream();var buffer=new byte[65536];int read;
        while((read=gzip.Read(buffer))>0){if(output.Length+read>16*1024*1024)throw new InvalidDataException("Sol Ring exceeds its native asset budget.");output.Write(buffer,0,read);}
        byte[] glb=output.ToArray();
        if(!Convert.ToHexString(SHA256.HashData(glb)).Equals(DecodedSha256,StringComparison.OrdinalIgnoreCase)
            ||BitConverter.ToUInt32(glb,0)!=0x46546c67||BitConverter.ToUInt32(glb,4)!=2||BitConverter.ToUInt32(glb,8)!=glb.Length)
            throw new InvalidDataException("The native map requires the accepted Sol Ring v3 asset.");
        int jsonLength=BitConverter.ToInt32(glb,12),binaryHeader=20+jsonLength;
        if(BitConverter.ToUInt32(glb,16)!=0x4e4f534a||BitConverter.ToUInt32(glb,binaryHeader+4)!=0x004e4942)
            throw new InvalidDataException("Invalid Sol Ring GLB chunks.");
        using var document=JsonDocument.Parse(glb.AsMemory(20,jsonLength));var root=document.RootElement;
        int binaryStart=binaryHeader+8;var accessors=root.GetProperty("accessors");var views=root.GetProperty("bufferViews");
        var meshes=root.GetProperty("meshes");var materials=root.GetProperty("materials");
        if(meshes.GetArrayLength()!=11||materials.GetArrayLength()!=7)throw new InvalidDataException("Sol Ring source structure changed.");
        // This hash-pinned source has eleven identity mesh nodes, plus light nodes.
        var meshNodes=root.GetProperty("nodes").EnumerateArray().Where(n=>n.TryGetProperty("mesh",out _)).ToArray();
        if(meshNodes.Length!=11||meshNodes.Where((n,i)=>n.GetProperty("mesh").GetInt32()!=i
            ||n.TryGetProperty("matrix",out _)||n.TryGetProperty("translation",out _)||n.TryGetProperty("rotation",out _)||n.TryGetProperty("scale",out _)).Any())
            throw new InvalidDataException("Sol Ring mesh transforms require review.");
        var parts=new List<Part>();int triangles=0,apertures=0;
        double outerRadius=meshes.EnumerateArray().SelectMany(m=>m.GetProperty("primitives").EnumerateArray())
            .Select(p=>accessors[p.GetProperty("attributes").GetProperty("POSITION").GetInt32()])
            .Max(a=>a.GetProperty("max").EnumerateArray().Take(2).Max(v=>Math.Abs(v.GetDouble())));
        for(int meshIndex=0;meshIndex<meshes.GetArrayLength();meshIndex++)
        foreach(var primitive in meshes[meshIndex].GetProperty("primitives").EnumerateArray())
        {
            bool aperture=meshes[meshIndex].GetProperty("name").GetString()!.StartsWith("Active aperture",StringComparison.Ordinal);
            if(aperture)apertures++;
            var attributes=primitive.GetProperty("attributes");
            int positionId=attributes.GetProperty("POSITION").GetInt32(),normalId=attributes.GetProperty("NORMAL").GetInt32();
            var positionAccessor=accessors[positionId];int vertexCount=positionAccessor.GetProperty("count").GetInt32();
            int indexId=primitive.GetProperty("indices").GetInt32(),count=accessors[indexId].GetProperty("count").GetInt32();
            var material=NativeMaterial(materials[primitive.GetProperty("material").GetInt32()],aperture);
            if(count%3!=0||vertexCount>200000)throw new InvalidDataException("Invalid Sol Ring primitive.");
            triangles+=count/3;
            // Re-index each bounded batch; retain every original face and normal.
            for(int first=0;first<count;first+=18000)
            {
                var geometry=new MeshGeometry3D();var remap=new Dictionary<int,int>();
                for(int i=first;i<Math.Min(first+18000,count);i++)
                {
                    int index=Index(indexId,i);if(index<0||index>=vertexCount)throw new InvalidDataException("Invalid Sol Ring index.");
                    if(!remap.TryGetValue(index,out int local))
                    {
                        local=geometry.Positions.Count;remap.Add(index,local);
                        var p=Vector(positionId,index);var n=Vector(normalId,index);
                        geometry.Positions.Add(new(p.X/outerRadius,p.Y/outerRadius,p.Z/outerRadius));geometry.Normals.Add(n);
                        if(aperture)geometry.TextureCoordinates.Add(new(.5+p.X/5.412,.5-p.Y/5.412));
                    }
                    geometry.TriangleIndices.Add(local);
                }
                geometry.Freeze();parts.Add(new(geometry,material,aperture));
            }
        }
        if(triangles!=282272||apertures!=1)throw new InvalidDataException("Sol Ring faces or separate aperture changed.");
        var frozen=new Model3DGroup();
        foreach(var part in parts)frozen.Children.Add(new GeometryModel3D(part.Mesh,part.Material){BackMaterial=part.Material});
        frozen.Freeze();return new(parts.ToArray(),frozen,Convert.ToHexString(SHA256.HashData(packed)),triangles);

        int Offset(int id,int index,int width)
        {
            var a=accessors[id];var v=views[a.GetProperty("bufferView").GetInt32()];
            return checked(binaryStart+v.GetProperty("byteOffset").GetInt32()+(a.TryGetProperty("byteOffset",out var o)?o.GetInt32():0)
                +index*(v.TryGetProperty("byteStride",out var stride)?stride.GetInt32():width));
        }
        Vector3D Vector(int id,int index)
        {
            if(accessors[id].GetProperty("componentType").GetInt32()!=5126)throw new InvalidDataException("Unsupported Sol Ring vector.");
            int at=Offset(id,index,12);return new(BitConverter.ToSingle(glb,at),BitConverter.ToSingle(glb,at+4),BitConverter.ToSingle(glb,at+8));
        }
        int Index(int id,int index)=>accessors[id].GetProperty("componentType").GetInt32() switch
        {5123=>BitConverter.ToUInt16(glb,Offset(id,index,2)),5125=>checked((int)BitConverter.ToUInt32(glb,Offset(id,index,4))),_=>throw new InvalidDataException("Unsupported Sol Ring index type.")};
    }

    private static Material NativeMaterial(JsonElement source,bool aperture)
    {
        Material material;
        if(aperture)
        {
            var pixels=new byte[TextureSize*TextureSize*4];PaintField(pixels,0);
            var bitmap=BitmapSource.Create(TextureSize,TextureSize,96,96,PixelFormats.Bgra32,null,pixels,TextureSize*4);bitmap.Freeze();
            material=new EmissiveMaterial(new ImageBrush(bitmap));
        }
        else
        {
            var pbr=source.GetProperty("pbrMetallicRoughness");var colour=pbr.GetProperty("baseColorFactor");
            var group=new MaterialGroup();group.Children.Add(new DiffuseMaterial(Brush(colour)));
            group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(85,106,116)),28));
            if(source.TryGetProperty("emissiveFactor",out var emissive))group.Children.Add(new EmissiveMaterial(Brush(emissive)));
            material=group;
        }
        material.Freeze();return material;
        static SolidColorBrush Brush(JsonElement rgb)=>new(Color.FromRgb(Srgb(rgb[0].GetDouble()),Srgb(rgb[1].GetDouble()),Srgb(rgb[2].GetDouble())));
    }
    private static byte Srgb(double linear)=>(byte)Math.Clamp(Math.Round(255*(linear<=.0031308?12.92*linear:1.055*Math.Pow(linear,1/2.4)-.055)),0,255);
    private static (double,double)[] MakeNoise()
    {
        var values=new (double,double)[64*64];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)values[y*64+x]=(Hash(x*127.1+y*311.7),Hash(x*269.5+y*183.3));
        return values;
        static double Hash(double n){double value=Math.Sin(n)*43758.5453;return value-Math.Floor(value);}
    }
    private static (double X,double Y) Hash(int x,int y)=>noiseGrid[(y&63)*64+(x&63)];
    private static double Noise(double x,double y)
    {
        int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);double fx=x-ix,fy=y-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
        double a=Hash(ix,iy).X,b=Hash(ix+1,iy).X,c=Hash(ix,iy+1).X,d=Hash(ix+1,iy+1).X;
        return (a+(b-a)*fx)*(1-fy)+(c+(d-c)*fx)*fy;
    }
    private static void PaintField(byte[] pixels,double time)
    {
        // Same warped Voronoi veins, blue veil and linear colours as portal.js.
        // Animate the sites once per frame, rather than using trig per pixel.
        const int side=20,origin=-10;var sites=new Point[side*side];
        for(int y=0;y<side;y++)for(int x=0;x<side;x++)
        {
            var hash=Hash(x+origin,y+origin);sites[y*side+x]=new(x+origin+.5+.35*Math.Sin(time*.12+Math.Tau*hash.X),y+origin+.5+.35*Math.Sin(time*.12+Math.Tau*hash.Y));
        }
        for(int y=0;y<TextureSize;y++)for(int x=0;x<TextureSize;x++)
        {
            double qx=((x+.5)/TextureSize*2-1)*2.9+time*.00375,qy=(1-(y+.5)/TextureSize*2)*2.9-time*.0025;
            qx+=Noise(qx*1.9+time*.016,qy*1.9+time*.016)*.65;
            qy+=Noise(qx+4.1-time*.012,qy+4.1-time*.012)*.65;
            // Periodically wrap the precomputed grid during long-running use.
            qx-=8*Math.Floor((qx+4)/8);qy-=8*Math.Floor((qy+4)/8);
            int cx=(int)Math.Floor(qx),cy=(int)Math.Floor(qy);Point nearest=new();double best=double.PositiveInfinity;int cellX=0,cellY=0;
            for(int j=-1;j<=1;j++)for(int i=-1;i<=1;i++)
            {
                var site=sites[(cy+j-origin)*side+cx+i-origin];double dx=site.X-qx,dy=site.Y-qy,distance=dx*dx+dy*dy;
                if(distance<best){best=distance;nearest=site;cellX=cx+i;cellY=cy+j;}
            }
            double edge=10;
            for(int j=-2;j<=2;j++)for(int i=-2;i<=2;i++)
            {
                var site=sites[(cellY+j-origin)*side+cellX+i-origin];var direction=site-nearest;double length=direction.Length;
                if(length>.0001)edge=Math.Min(edge,((site.X+nearest.X)*.5-qx)*direction.X/length+((site.Y+nearest.Y)*.5-qy)*direction.Y/length);
            }
            double vein=Math.Exp(-edge*18),veil=Noise(qx*1.3-time*.018,qy*1.3-time*.018)*.45;int p=(y*TextureSize+x)*4;
            pixels[p]=Srgb(.024+(.085-.024)*veil+.062*vein);pixels[p+1]=Srgb(.006+(.050-.006)*veil+.046*vein);
            pixels[p+2]=Srgb(.001+(.009-.001)*veil+.005*vein);pixels[p+3]=255;
        }
    }
}
