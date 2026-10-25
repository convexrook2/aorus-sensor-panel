using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SensorPanel;

internal sealed class ArtworkAnimation : IDisposable
{
    private sealed record Frame(string File, string Hash, int Width, int Height,int Bytes);
    private sealed record Surface(WriteableBitmap Bitmap, byte[] Pixels);
    private readonly Image[] images;
    private readonly Surface?[] surfaces = new Surface?[3];
    private readonly Frame?[] presented = new Frame?[3];
    private byte[] encoded=[];
    private readonly DispatcherTimer timer;
    private readonly Stopwatch elapsed = new();
    private Frame[] leftFrames = [], rightFrames = [], brandFrames = [];
    private bool disposed, enabled = true;
    private Task? loading;
    private int generation;
    public int TickCount { get; private set; }
    public int FrameIndex { get; private set; }
    public int UniqueFrameCount { get; private set; }
    internal long EncodedBytes => encoded.Length;
    public bool Enabled
    {
        get => enabled;
        set { enabled = value; if (value && leftFrames.Length > 0) timer.Start(); else timer.Stop(); }
    }

    public ArtworkAnimation(Image left, Image right, Image brand)
    {
        images = [left, right, brand];
        timer = new DispatcherTimer(DispatcherPriority.Background, left.Dispatcher) { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += Tick;
    }
    public Task LoadAsync() => loading ??= LoadFramesAsync();
    private async Task LoadFramesAsync()
    {
        int version = generation;
        // Reuse one compressed input buffer and three full-resolution decoded
        // surfaces. Per-frame PNG reads must not allocate large-object arrays.
        var result = await Task.Run(() =>
        {
            using var index = JsonDocument.Parse(Rookline.AssetProtection.AppAssets.Read("AORUS/@Resources/GIFs/frames.json"));
            if(index.RootElement.GetProperty("schema").GetInt32()!=1)throw new InvalidDataException("Unsupported AORUS animation index.");
            var frames = index.RootElement.GetProperty("frames").EnumerateArray().Select(entry =>
                {
                    string path=entry.GetProperty("file").GetString()!;
                    int width = entry.GetProperty("width").GetInt32();
                    int height = entry.GetProperty("height").GetInt32();
                    if(width is <1 or >1024 || height is <1 or >1024)throw new InvalidDataException("AORUS animation frame exceeds its pixel budget.");
                    int bytes=entry.GetProperty("bytes").GetInt32();
                    if(bytes is <24 or >1_048_576)throw new InvalidDataException("AORUS animation input exceeds its pixel budget.");
                    return new Frame(path, entry.GetProperty("sha256").GetString()!, width, height,bytes);
                }).ToArray();
            Frame[] Sequence(string name,int count)
            {
                var values=index.RootElement.GetProperty("sequences").GetProperty(name).EnumerateArray().Select(i=>frames[i.GetInt32()]).ToArray();
                if(values.Length!=count)throw new InvalidDataException("Incomplete AORUS animation sequence.");
                return values;
            }
            return (Sequence("L",175),Sequence("R",175),Sequence("GIF1",4),unique:frames.Length,buffer:new byte[frames.Max(f=>f.Bytes)]);
        });
        if (disposed || version != generation) return;
        (leftFrames, rightFrames, brandFrames, UniqueFrameCount,encoded) = result;
        elapsed.Start(); Update(); if (enabled) timer.Start();
    }
    public void ReleaseFrames()
    {
        generation++; timer.Stop(); elapsed.Reset();
        leftFrames = []; rightFrames = []; brandFrames = []; loading = null;encoded=[];
        Array.Clear(presented);
    }
    private void Tick(object? sender, EventArgs e) => Update();
    private void Update()
    {
        if (leftFrames.Length == 0) return;
        int counter = (int)(elapsed.ElapsedMilliseconds / 30);
        FrameIndex = counter % 175;
        Present(0, leftFrames[FrameIndex]); Present(1, rightFrames[FrameIndex]); Present(2, brandFrames[(counter / 13) % 4]);
        TickCount++;
    }
    private void Present(int slot, Frame frame)
    {
        if (ReferenceEquals(presented[slot], frame)) return;
        var surface = surfaces[slot];
        if (surface is null || surface.Bitmap.PixelWidth != frame.Width || surface.Bitmap.PixelHeight != frame.Height)
        {
            surface = new(new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Pbgra32, null), new byte[checked(frame.Width * frame.Height * 4)]);
            surfaces[slot] = surface; images[slot].Source = surface.Bitmap;
        }
        ReadPng(frame);Decode(encoded, surface.Pixels, frame.Width, frame.Height,frame.Bytes);
        surface.Bitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), surface.Pixels, frame.Width * 4, 0);
        presented[slot] = frame;
    }
    private void ReadPng(Frame frame)
    {
        int length=Rookline.AssetProtection.AppAssets.ReadInto("AORUS/@Resources/GIFs/"+frame.File,encoded);
        var png=encoded.AsSpan(0,length);
        if(length!=frame.Bytes||length<24||BinaryPrimitives.ReadInt32BigEndian(png.Slice(16,4))!=frame.Width||
            BinaryPrimitives.ReadInt32BigEndian(png.Slice(20,4))!=frame.Height||
            !Convert.ToHexString(SHA256.HashData(png)).Equals(frame.Hash,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("AORUS animation index differs from source pixels.");
    }
    internal static void Decode(byte[] png, byte[] pixels, int width, int height,int length=-1)
    {
        int available=length<0?png.Length:length;
        if(width is <1 or >1024||height is <1 or >1024||pixels.Length!=width*height*4||available>png.Length||available<33||
            !png.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("Invalid AORUS PNG dimensions/signature.");
        int compressed=0,offset=8;bool header=false,ended=false;
        while(offset<=available-12)
        {
            int size=BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset,4));
            if(size<0||size>available-offset-12)throw new InvalidDataException("AORUS PNG chunk exceeds its input.");
            var kind=png.AsSpan(offset+4,4);var data=png.AsSpan(offset+8,size);
            if(kind.SequenceEqual("IHDR"u8))
            {
                if(header||offset!=8||size!=13||BinaryPrimitives.ReadInt32BigEndian(data)!=width||BinaryPrimitives.ReadInt32BigEndian(data[4..])!=height||
                    data[8]!=8||data[9]!=6||data[10]!=0||data[11]!=0||data[12]!=0)throw new InvalidDataException("AORUS artwork requires its original noninterlaced RGBA8 PNG format.");
                header=true;
            }
            else if(kind.SequenceEqual("IDAT"u8))
            {
                if(!header)throw new InvalidDataException("AORUS PNG data precedes its header.");
                // Input is authenticated and hash-checked before decoding. It is
                // caller-owned scratch space; compact IDAT in place, no copy array.
                data.CopyTo(png.AsSpan(compressed));compressed+=size;
            }
            else if(kind.SequenceEqual("IEND"u8))
            {if(size!=0)throw new InvalidDataException("Invalid AORUS PNG terminator.");ended=true;offset+=12;break;}
            else if((kind[0]&32)==0&&!kind.SequenceEqual("PLTE"u8))throw new InvalidDataException("Unsupported AORUS PNG critical chunk.");
            offset+=size+12;
        }
        if(!header||!ended||compressed==0||offset!=available)throw new InvalidDataException("Incomplete AORUS PNG.");
        using var stream=new MemoryStream(png,0,compressed,false);
        using var inflater=new ZLibStream(stream,CompressionMode.Decompress);
        int stride=width*4;
        Span<byte> rows=stackalloc byte[stride*2],previous=rows[..stride],current=rows[stride..];previous.Clear();
        for(int y=0;y<height;y++)
        {
            int filter=inflater.ReadByte();if(filter is <0 or >4)throw new InvalidDataException("Invalid AORUS PNG row filter.");
            inflater.ReadExactly(current);
            for(int x=0;x<stride;x++)
            {
                int left=x>=4?current[x-4]:0,above=previous[x],corner=x>=4?previous[x-4]:0;
                int prediction=filter switch{0=>0,1=>left,2=>above,3=>(left+above)/2,_=>Paeth(left,above,corner)};
                current[x]=unchecked((byte)(current[x]+prediction));
            }
            int row=y*stride;
            for(int x=0;x<stride;x+=4)
            {
                int alpha=current[x+3];
                pixels[row+x]=(byte)((current[x+2]*alpha+127)/255);
                pixels[row+x+1]=(byte)((current[x+1]*alpha+127)/255);
                pixels[row+x+2]=(byte)((current[x]*alpha+127)/255);pixels[row+x+3]=(byte)alpha;
            }
            var swap=previous;previous=current;current=swap;
        }
        if(inflater.ReadByte()!=-1)throw new InvalidDataException("AORUS PNG contains excess pixel rows.");
        static int Paeth(int left,int above,int corner)
        {
            int prediction=left+above-corner,a=Math.Abs(prediction-left),b=Math.Abs(prediction-above),c=Math.Abs(prediction-corner);
            return a<=b&&a<=c?left:b<=c?above:corner;
        }
    }
    internal static int CheckPixels()
    {
        int checkedFrames=0;var hashes=new HashSet<string>();
        var reusable=new byte[1_048_576];
        foreach(var (folder,prefix,count) in new[]{("L","Comp ",175),("R","Comp ",175),("GIF1","X",4)})
        for(int i=0;i<count;i++)
        {
            byte[] png=Rookline.AssetProtection.AppAssets.Read($"AORUS/@Resources/GIFs/{folder}/{prefix}{i}.png");
            if(!hashes.Add(Convert.ToHexString(SHA256.HashData(png))))continue;
            using var stream=new MemoryStream(png,false);
            var frame=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
            var expected=new FormatConvertedBitmap(frame,PixelFormats.Pbgra32,null,0);
            int stride=frame.PixelWidth*4;
            var original=new byte[stride*frame.PixelHeight];expected.CopyPixels(original,stride,0);
            int length=Rookline.AssetProtection.AppAssets.ReadInto($"AORUS/@Resources/GIFs/{folder}/{prefix}{i}.png",reusable);
            if(length!=png.Length||!reusable.AsSpan(0,length).SequenceEqual(png))throw new InvalidOperationException("Reusable PNG input differs from source.");
            var actual=new byte[original.Length];Decode(reusable,actual,frame.PixelWidth,frame.PixelHeight,length);
            if(!original.AsSpan().SequenceEqual(actual))throw new InvalidOperationException($"Decoded AORUS pixels changed: {folder}/{prefix}{i}.png");
            checkedFrames++;
        }
        File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"aorus-pixel-check.json"),System.Text.Json.JsonSerializer.Serialize(new{Passed=true,UniqueFrames=checkedFrames,ExactPbgra32=true}));
        return 0;
    }
    public void Dispose()
    {
        disposed = true; ReleaseFrames(); timer.Tick -= Tick;
        Array.Clear(surfaces);
    }
}
