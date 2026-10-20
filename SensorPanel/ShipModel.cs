using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SensorPanel;

internal sealed record ShipModelPart(MeshGeometry3D Mesh, Material Original, bool DoubleSided,
    string MaterialName, Color? SolidEmission);

internal static class ShipModel
{
    private static readonly Dictionary<string, IReadOnlyList<ShipModelPart>> cache = new();
    private sealed record MaterialInfo(string Name, string? Texture, float[] Colour, float[] Emissive, bool DoubleSided, string Alpha, string? EmissiveTexture);
    private sealed record SceneInfo(int Format, int Triangles, int Parts, MaterialInfo[] Materials);
    public static IReadOnlyList<ShipModelPart> Load(string ship, bool cacheResult = true)
    {
        ship=ShipProfile.CanonicalId(ship);
        if (cache.TryGetValue(ship, out var existing)) return existing;
        using var asset=Rookline.AssetProtection.AppAssets.Open("Models/"+ship+".ship");
        using var archive=new ZipArchive(asset, ZipArchiveMode.Read);
        using var json=Required(archive, "scene.json", 100_000).Open();
        var scene=JsonSerializer.Deserialize<SceneInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive=true })
            ?? throw new InvalidDataException("Missing ship metadata.");
        if (scene.Format!=1 || scene.Triangles is < 1 or > 150000 || scene.Parts is < 1 or > 200 || scene.Materials.Length is < 1 or > 100)
            throw new InvalidDataException("Ship scene exceeded its native render budget.");
        var materials=scene.Materials.Select(m => MakeMaterial(m, archive)).ToArray();
        using var geometry=Required(archive, "mesh.bin", 40_000_000).Open();
        using var reader=new BinaryReader(geometry);
        if (reader.ReadUInt32()!=0x31435348 || reader.ReadInt32()!=scene.Parts) throw new InvalidDataException("Invalid ship scene header.");
        var parts=new List<ShipModelPart>(); int triangles=0;
        for (int part=0; part<scene.Parts; part++)
        {
            int material=reader.ReadInt32(), vertices=reader.ReadInt32(), indices=reader.ReadInt32();
            if (material<0 || material>=materials.Length || vertices is < 3 or > 20000 || indices is < 3 or > 60000 || indices%3!=0)
                throw new InvalidDataException("Invalid ship part bounds.");
            var mesh=new MeshGeometry3D { Positions=new Point3DCollection(vertices), Normals=new Vector3DCollection(vertices),
                TextureCoordinates=new PointCollection(vertices), TriangleIndices=new Int32Collection(indices) };
            double Number() { float value=reader.ReadSingle(); return float.IsFinite(value) ? value : throw new InvalidDataException("Nonfinite ship coordinate."); }
            for (int i=0; i<vertices; i++)
            {
                mesh.Positions.Add(new(Number(), Number(), Number()));
                mesh.Normals.Add(new(Number(), Number(), Number()));
                mesh.TextureCoordinates.Add(new(Number(), Number()));
            }
            if (mesh.Normals.All(n => n.LengthSquared<1e-12)) mesh.Normals.Clear();
            for (int i=0; i<indices; i++) { int index=reader.ReadInt32(); if (index<0 || index>=vertices) throw new InvalidDataException("Invalid ship triangle."); mesh.TriangleIndices.Add(index); }
            mesh.Freeze(); triangles+=indices/3;
            var info=scene.Materials[material];
            Color? emission=info.EmissiveTexture is null && info.Emissive.Any(c=>c>0) ? Colour(info.Emissive) : null;
            parts.Add(new(mesh, materials[material], info.DoubleSided, info.Name, emission));
        }
        if (triangles!=scene.Triangles || geometry.ReadByte()!=-1) throw new InvalidDataException("Ship geometry does not match its manifest.");
        if (cacheResult) cache.Add(ship, parts);
        return parts;
    }
    private static ZipArchiveEntry Required(ZipArchive archive, string name, long limit)
    {
        var entry=archive.GetEntry(name) ?? throw new InvalidDataException("Missing ship asset: "+name);
        if (entry.Length>limit) throw new InvalidDataException("Ship asset exceeded its decode budget.");
        return entry;
    }
    private static Color Colour(float[] components, bool alpha=false)
    {
        if (components.Length!=(alpha ? 4 : 3) || components.Any(v => !float.IsFinite(v) || v<0 || v>1)) throw new InvalidDataException("Invalid original material colour.");
        return Color.FromScRgb(alpha ? components[3] : 1, components[0], components[1], components[2]);
    }
    private static Material MakeMaterial(MaterialInfo info, ZipArchive archive)
    {
        Brush ink;
        if (info.Texture is { } file)
        {
            using var image=Required(archive, file, 8_000_000).Open();
            using var pixels=new MemoryStream(); image.CopyTo(pixels); pixels.Position=0;
            var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.StreamSource=pixels;
            bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth=1024; bitmap.EndInit(); bitmap.Freeze();
            if (bitmap.PixelHeight>4096) throw new InvalidDataException("Unexpected original texture dimensions.");
            ink=new ImageBrush(bitmap) { TileMode=TileMode.Tile, ViewportUnits=BrushMappingMode.Absolute, Viewport=new Rect(0,0,1,1), Stretch=Stretch.Fill };
        }
        else ink=new SolidColorBrush(Colour(info.Colour, true));
        ink.Freeze();
        var material=new MaterialGroup(); material.Children.Add(new DiffuseMaterial(ink));
        var emission=Colour(info.Emissive);
        if (info.EmissiveTexture is { } emissionFile)
        {
            using var image=Required(archive, emissionFile, 8_000_000).Open();
            using var pixels=new MemoryStream(); image.CopyTo(pixels); pixels.Position=0;
            var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.StreamSource=pixels;
            bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth=1024; bitmap.EndInit(); bitmap.Freeze();
            var glow=new ImageBrush(bitmap) { TileMode=TileMode.Tile, ViewportUnits=BrushMappingMode.Absolute, Viewport=new Rect(0,0,1,1), Stretch=Stretch.Fill };
            glow.Freeze(); material.Children.Add(new EmissiveMaterial(glow));
        }
        else if (emission.R!=0 || emission.G!=0 || emission.B!=0) material.Children.Add(new EmissiveMaterial(new SolidColorBrush(emission)));
        material.Freeze(); return material;
    }
}
