using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SensorPanel;

/// <summary>Belter's original pixels and packed geometry, in the native schematic.</summary>
internal static class SolarBodyAssets
{
    private static readonly Dictionary<string, Model3DGroup> cache = new();
    private static readonly Dictionary<string, string> sources = new();
    private static readonly MeshGeometry3D sphere = Sphere();
    private static readonly string root = Path.Combine(AppContext.BaseDirectory, "SolarNavigation");
    internal static IReadOnlyDictionary<string, string> Sources => sources;

    internal static Model3DGroup Load(string id)
    {
        if (cache.TryGetValue(id, out var existing)) return existing;
        var model = new Model3DGroup();
        if (id is "Eros" or "Phoebe" or "Tycho" or "Medina" or "SolGate")
            Packed(model, id == "SolGate" ? "solgate" : id.ToLowerInvariant(), id == "Eros" ? 8.4 : 1);
        else
        {
            string file = id == "Luna" ? "moon" : id.ToLowerInvariant();
            Material material = id == "Sun" ? new EmissiveMaterial(Texture(file + ".jpg")) : new DiffuseMaterial(Texture(file + ".jpg"));
            material.Freeze();
            model.Children.Add(new GeometryModel3D(sphere, material));
            if (id == "Saturn")
            {
                var ring = new DiffuseMaterial(Texture("saturn_ring.png")); ring.Freeze();
                model.Children.Add(new GeometryModel3D(Ring(), ring) { BackMaterial = ring });
            }
            if (id == "Earth")
            {
                var clouds = new DiffuseMaterial(Clouds()); clouds.Freeze();
                model.Children.Add(new GeometryModel3D(sphere, clouds) { Transform = new ScaleTransform3D(1.009, 1.009, 1.009) });
            }
        }
        model.Freeze(); cache.Add(id, model); return model;
    }

    private static string Asset(string relative)
    {
        string path = Path.Combine(root, relative);
        if (!sources.ContainsKey(relative)) sources.Add(relative, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        return path;
    }
    private static BitmapImage Bitmap(string file)
    {
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri(Asset("textures/" + file));
        bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 2048; bitmap.EndInit(); bitmap.Freeze();
        return bitmap;
    }
    private static ImageBrush Texture(string file) => Image(Bitmap(file));
    private static ImageBrush Image(ImageSource bitmap)
    {
        var brush = new ImageBrush(bitmap) { Stretch = Stretch.Fill, TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute, Viewport = new Rect(0, 0, 1, 1) };
        brush.Freeze(); return brush;
    }
    private static ImageBrush Clouds()
    {
        // The supplied JPEG is a white cloud mask on black, not an opaque surface.
        var image = new FormatConvertedBitmap(Bitmap("earth_clouds.jpg"), PixelFormats.Bgra32, null, 0);
        int stride = image.PixelWidth * 4; var pixels = new byte[stride * image.PixelHeight]; image.CopyPixels(pixels, stride, 0);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte alpha = (byte)((pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3 * .74);
            pixels[i] = pixels[i + 1] = pixels[i + 2] = 255; pixels[i + 3] = alpha;
        }
        var result = BitmapSource.Create(image.PixelWidth, image.PixelHeight, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze(); return Image(result);
    }

    private static void Packed(Model3DGroup model, string name, double sourceRadiusKm)
    {
        using var file = File.OpenRead(Asset("models/" + name + ".fnm")); using var reader = new BinaryReader(file);
        string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        int version = magic switch { "FNM1" => 1, "FNM2" => 2, "FNM3" => 3, _ => throw new InvalidDataException("Unknown solar mesh format.") };
        int vertices = reader.ReadInt32(), triangles = reader.ReadInt32();
        if (vertices is < 3 or > 200000 || triangles is < 1 or > 150000) throw new InvalidDataException("Solar mesh exceeded its geometry budget.");
        long colourBytes = version >= 2 ? (vertices * 3L + 3) & ~3L : 0;
        long expected = 12 + vertices * 12L + triangles * 12L + colourBytes + (version >= 3 ? vertices * 8L : 0);
        if (file.Length != expected) throw new InvalidDataException("Solar mesh length differs from its header.");
        var points = new Point3D[vertices]; var normals = new Vector3D[vertices]; var indices = new int[triangles * 3];
        double divisor = version == 1 ? sourceRadiusKm : 1;
        for (int i = 0; i < vertices; i++)
        {
            float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle();
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z)) throw new InvalidDataException("Invalid solar mesh vertex.");
            points[i] = new Point3D(x / divisor, z / divisor, y / divisor); // Belter Z-up to WPF Y-up.
        }
        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = reader.ReadInt32(), b = reader.ReadInt32(), c = reader.ReadInt32();
            if (a < 0 || b < 0 || c < 0 || a >= vertices || b >= vertices || c >= vertices) throw new InvalidDataException("Invalid solar mesh triangle.");
            // Swapping the axes also reverses handedness.
            indices[i] = a; indices[i + 1] = c; indices[i + 2] = b;
            var n = Vector3D.CrossProduct(points[c] - points[a], points[b] - points[a]);
            normals[a] += n; normals[b] += n; normals[c] += n;
        }
        for (int i = 0; i < vertices; i++) if (normals[i].LengthSquared > 1e-20) normals[i].Normalize();
        var colours = version >= 2 ? reader.ReadBytes(vertices * 3) : null;
        var atlas = new byte[64 * 64 * 4];
        for (int i = 0; i < 4096; i++)
        { atlas[i * 4] = (byte)((i & 15) * 17); atlas[i * 4 + 1] = (byte)(((i >> 4) & 15) * 17); atlas[i * 4 + 2] = (byte)((i >> 8) * 17); atlas[i * 4 + 3] = 255; }
        var palette = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, atlas, 256); palette.Freeze();
        Material material = new DiffuseMaterial(colours is null ? new SolidColorBrush(Color.FromRgb(169, 151, 128)) : Image(palette)); material.Freeze();
        if (name == "solgate")
        {
            var glow = Image(palette).Clone(); glow.Opacity = .45; glow.Freeze();
            var lit = new MaterialGroup(); lit.Children.Add(material); lit.Children.Add(new EmissiveMaterial(glow)); lit.Freeze(); material = lit;
        }
        // Retain the source faces; split only to stay inside WPF's per-mesh limits.
        for (int start = 0; start < indices.Length; start += 18000)
        {
            var mesh = new MeshGeometry3D(); int end = Math.Min(start + 18000, indices.Length);
            for (int i = start; i < end; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2]; var face = Vector3D.CrossProduct(points[b] - points[a], points[c] - points[a]);
                if (face.LengthSquared > 1e-20) face.Normalize();
                int colour = 0;
                if (colours is not null)
                    for (int channel = 0; channel < 3; channel++) colour |= ((colours[a * 3 + channel] + colours[b * 3 + channel] + colours[c * 3 + channel]) / 3 >> 4) << ((2 - channel) * 4);
                var uv = new Point((colour % 64 + .5) / 64, (colour / 64 + .5) / 64);
                foreach (int index in new[] { a, b, c })
                { mesh.TriangleIndices.Add(mesh.Positions.Count); mesh.Positions.Add(points[index]); mesh.Normals.Add(version == 1 ? face : normals[index]); mesh.TextureCoordinates.Add(uv); }
            }
            mesh.Freeze(); model.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = version == 1 ? material : null });
        }
    }

    private static MeshGeometry3D Sphere()
    {
        const int slices = 96, stacks = 48; var mesh = new MeshGeometry3D();
        for (int y = 0; y <= stacks; y++) for (int x = 0; x <= slices; x++)
        {
            double latitude = Math.PI * y / stacks, longitude = Math.Tau * x / slices;
            var p = new Point3D(Math.Sin(latitude) * Math.Cos(longitude), Math.Cos(latitude), Math.Sin(latitude) * Math.Sin(longitude));
            mesh.Positions.Add(p); mesh.Normals.Add((Vector3D)p); mesh.TextureCoordinates.Add(new((double)x / slices, (double)y / stacks));
            if (y == stacks || x == slices) continue;
            int a = y * (slices + 1) + x, b = a + slices + 1;
            foreach (int index in new[] { a, a + 1, b, a + 1, b + 1, b }) mesh.TriangleIndices.Add(index);
        }
        mesh.Freeze(); return mesh;
    }
    private static MeshGeometry3D Ring()
    {
        var mesh = new MeshGeometry3D();
        for (int i = 0; i <= 128; i++) for (int edge = 0; edge < 2; edge++)
        {
            double a = i * Math.Tau / 128, r = (edge == 0 ? 74500d : 140220d) / 58232;
            mesh.Positions.Add(new(r * Math.Cos(a), r * Math.Sin(a) * .45, r * Math.Sin(a) * .89)); mesh.TextureCoordinates.Add(new(edge, .5));
            if (i == 128 || edge != 0) continue;
            int n = i * 2; foreach (int index in new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 }) mesh.TriangleIndices.Add(index);
        }
        mesh.Freeze(); return mesh;
    }
    internal static object Check()
    {
        foreach (var model in cache.Values)
            if (!model.IsFrozen || model.Children.OfType<GeometryModel3D>().Any(p => p.Geometry is not MeshGeometry3D m || !m.IsFrozen || m.Positions.Count > 20000 || m.TriangleIndices.Count > 60000))
                throw new InvalidOperationException("A solar asset exceeds the native render budget.");
        if (!sources.ContainsKey("textures/earth.jpg") || !sources.ContainsKey("textures/saturn_ring.png") || !sources.ContainsKey("models/eros.fnm") || cache.Count != 19)
            throw new InvalidOperationException("The schematic is missing its detailed assets.");
        return new { NativeModels = cache.Count, SourceFiles = sources, OriginalErosTriangles = cache["Eros"].Children.OfType<GeometryModel3D>().Sum(m => ((MeshGeometry3D)m.Geometry).TriangleIndices.Count / 3), Source = "Belter Charts ba86075477fc529294a5e51cb4e1b0d183ff893a", StationGeometry = "Retained original panel models" };
    }
}
