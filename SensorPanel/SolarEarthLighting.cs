using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Diagnostics;

namespace SensorPanel;

/// <summary>Belter's day/night material equation, baked to a small native texture at most twice per second.</summary>
internal sealed class SolarEarthLighting
{
    private const int Width = 512, Height = 256;
    private readonly WriteableBitmap bitmap = new(Width, Height, 96, 96, PixelFormats.Bgra32, null);
    private readonly byte[] pixels = new byte[Width * Height * 4];
    private readonly float[] day = new float[Width * Height * 3], night = new float[Width * Height * 3];
    private readonly Vector3D[] normals = new Vector3D[Width * Height];
    private readonly byte[] gamma = new byte[4096];
    private long lastUpdate;
    private Vector3D lastDirection;
    private int updates, litNightPixels;
    internal GeometryModel3D Model { get; }

    internal SolarEarthLighting()
    {
        static byte[] Read(string file)
        {
            var source = SolarBodyAssets.Bitmap(file);
            var scaled = new TransformedBitmap(source, new ScaleTransform((double)Width / source.PixelWidth, (double)Height / source.PixelHeight));
            var converted = new FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[Width * Height * 4]; converted.CopyPixels(bytes, Width * 4, 0); return bytes;
        }
        var dayBytes = Read("earth.jpg"); var nightBytes = Read("earth_night.jpg");
        var linear = Enumerable.Range(0, 256).Select(i => i <= 10 ? i / 255d / 12.92 : Math.Pow((i / 255d + .055) / 1.055, 2.4)).ToArray();
        for (int i = 0; i < gamma.Length; i++)
        { double v = i / 4095d; gamma[i] = (byte)Math.Clamp((v <= .0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - .055) * 255, 0, 255); }
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            int index = y * Width + x; double latitude = Math.PI * (y + .5) / Height, longitude = Math.Tau * (x + .5) / Width;
            normals[index] = new(Math.Sin(latitude) * Math.Cos(longitude), Math.Cos(latitude), Math.Sin(latitude) * Math.Sin(longitude));
            for (int c = 0; c < 3; c++) { day[index * 3 + c] = (float)linear[dayBytes[index * 4 + c]]; night[index * 3 + c] = (float)linear[nightBytes[index * 4 + c]]; }
        }
        Model = new(SolarBodyAssets.Sphere(), new EmissiveMaterial(new ImageBrush(bitmap) { Stretch = Stretch.Fill }));
        Update(new(1, 0, 0));
    }

    internal void Update(Vector3D sunDirection,bool force=false)
    {
        if(updates>0&&sunDirection==lastDirection)return;
        long now = Stopwatch.GetTimestamp();
        if (!force&&updates > 0 && now - lastUpdate < Stopwatch.Frequency / 2) return;
        lastUpdate = now;lastDirection=sunDirection;updates++; litNightPixels = 0;
        for (int i = 0; i < normals.Length; i++)
        {
            double sunLit = Vector3D.DotProduct(normals[i], sunDirection);
            double t = Math.Clamp((sunLit + .08) / .30, 0, 1), blend = t * t * (3 - 2 * t);
            double dayFactor = (.08 + 1.25 * Math.Max(sunLit, 0)) * blend, nightFactor = 1.15 * (1 - blend);
            for (int c = 0; c < 3; c++)
            {
                double value = day[i * 3 + c] * dayFactor + night[i * 3 + c] * nightFactor;
                pixels[i * 4 + c] = gamma[Math.Clamp((int)(value * 4095), 0, 4095)];
            }
            if (nightFactor > .8 && night[i * 3] + night[i * 3 + 1] + night[i * 3 + 2] > .05) litNightPixels++;
            pixels[i * 4 + 3] = 255;
        }
        bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), pixels, Width * 4, 0);
    }
    internal object Diagnose() => new { Texture = "earth_night.jpg", NativeDayNightMaterial = true, Width, Height,
        Updates = updates, IlluminatedNightPixels = litNightPixels, MaximumUpdatesPerSecond = 2 };
}
