using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SensorPanel;

internal sealed class ArtworkAnimation : IDisposable
{
    private readonly Image left, right, brand;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch elapsed = new();
    private BitmapSource[] leftFrames = [], rightFrames = [], brandFrames = [];
    private bool disposed, enabled = true;
    private Task? loading;
    private int generation;
    public int TickCount { get; private set; }
    public int FrameIndex { get; private set; }
    public int UniqueFrameCount { get; private set; }
    public bool Enabled { get => enabled; set { enabled = value; if (value && leftFrames.Length > 0) timer.Start(); else timer.Stop(); } }

    public ArtworkAnimation(Image left, Image right, Image brand)
    {
        this.left = left; this.right = right; this.brand = brand;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Background, (_, _) => Update(), left.Dispatcher);
        timer.Stop();
    }

    public Task LoadAsync() => loading ??= LoadFramesAsync();

    private async Task LoadFramesAsync()
    {
        int version=generation;
        // Freeze decoded images off the UI thread and share duplicate PNGs.
        var result = await Task.Run(() =>
        {
            var cache = new Dictionary<string, BitmapSource>();
            BitmapSource[] Load(string folder, string prefix, int count)
                => Enumerable.Range(0, count).Select(index =>
                {
                    var uri = new Uri($"pack://application:,,,/Aorus/Animations/{folder}/{prefix}{index}.png");
                    using var stream = Application.GetResourceStream(uri)?.Stream ?? throw new FileNotFoundException(uri.ToString());
                    using var copy = new MemoryStream(); stream.CopyTo(copy);
                    string hash = Convert.ToHexString(SHA256.HashData(copy.ToArray()));
                    if (cache.TryGetValue(hash, out var existing)) return existing;
                    copy.Position = 0;
                    var bitmap = BitmapFrame.Create(copy, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    bitmap.Freeze(); cache.Add(hash, bitmap); return bitmap;
                }).ToArray();
            var l = Load("L", "Comp ", 175); var r = Load("R", "Comp ", 175); var x = Load("X", "X", 4);
            return (l, r, x, unique: cache.Count);
        });
        if (disposed || version!=generation) return;
        (leftFrames, rightFrames, brandFrames, UniqueFrameCount) = result;
        elapsed.Start(); if (enabled) timer.Start();
    }
    public void ReleaseFrames()
    {
        generation++; timer.Stop(); elapsed.Reset();
        leftFrames=[]; rightFrames=[]; brandFrames=[]; loading=null;
    }

    private void Update()
    {
        int counter = (int)(elapsed.ElapsedMilliseconds / 30);
        FrameIndex = counter % 175;
        left.Source = leftFrames[FrameIndex]; right.Source = rightFrames[FrameIndex];
        brand.Source = brandFrames[(counter / 13) % 4];
        TickCount++;
    }
    public void Dispose() { disposed = true; timer.Stop(); elapsed.Stop(); }
}
