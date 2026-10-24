using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace SensorPanel;

// Longer, independently sampled geometric trajectories for outer-planet context.
// The one-year runtime positions and their independent checkpoints stay intact.
internal static class SolarOrbitGuide
{
    private sealed record Data(int Schema,string Source,string TimeScale,string Frame,string Units,Entry[] Bodies);
    internal sealed record Entry(string Id,double WindowDays,double[][] Samples);
    internal static Entry[] Load()
    {
        using var source=Rookline.AssetProtection.AppAssets.Open("SolarNavigation/nasa-orbits.json.gz");
        using var gzip=new GZipStream(source,CompressionMode.Decompress);using var data=new MemoryStream();var buffer=new byte[65536];int read;
        while((read=gzip.Read(buffer))>0){if(data.Length+read>2*1024*1024)throw new InvalidDataException("Orbit guide exceeds its budget.");data.Write(buffer,0,read);}
        var document=JsonSerializer.Deserialize<Data>(data.ToArray())??throw new InvalidDataException("Empty orbit guides.");
        if(document.Schema!=1||document.Source!="NASA/JPL Horizons"||document.TimeScale!="TDB"
            ||document.Frame!="J2000 ecliptic / ICRF, geometric vectors"||document.Units!="AU and AU/day"
            ||!document.Bodies.Select(b=>b.Id).Order().SequenceEqual(new[]{"Jupiter","Mars","Neptune","Saturn","Uranus"}))throw new InvalidDataException("Unsupported orbit guide frame or bodies.");
        foreach(var body in document.Bodies)
        {
            if(body.Samples.Length is <700 or >730||body.WindowDays<=365)throw new InvalidDataException("Incomplete orbit guide.");
            double previous=double.NegativeInfinity;
            foreach(var sample in body.Samples){if(sample.Length!=7||sample.Any(n=>!double.IsFinite(n))||sample[0]<=previous)throw new InvalidDataException("Invalid orbit guide vector.");previous=sample[0];}
        }
        return document.Bodies;
    }
}
