using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows.Media.Media3D;

namespace SensorPanel;

/// <summary>Offline geometric JPL Horizons vectors. No network, game or sensor access.</summary>
internal sealed class SolarEphemeris
{
    internal const double AuKm = 149597870.7;
    private sealed record Data(int Schema, string Source, string Start, string Stop, string TimeScale,
        double AuKm, double SunRadiusKm, Series[] Bodies);
    internal sealed record Series(string Id, string Parent, double RadiusKm, double[][] Samples);
    private readonly Dictionary<string, Series> bodies;
    private readonly Dictionary<string,Series> pathBodies;
    private readonly Dictionary<string,double> guideDays=new();
    internal double StartJd { get; }
    internal double StopJd { get; }
    internal double SunRadiusKm { get; }
    internal IEnumerable<Series> Bodies => bodies.Values;
    internal string Range { get; }

    private SolarEphemeris(Data data)
    {
        if (data.Schema != 1 || data.Source != "NASA/JPL Horizons" || data.TimeScale != "TDB" || data.AuKm != AuKm
            || data.Bodies.Length != 16 || data.SunRadiusKm != 695700)
            throw new InvalidDataException("Unsupported NASA navigation data.");
        bodies = data.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
        StartJd = data.Bodies.Max(b => b.Samples[0][0]);
        StopJd = data.Bodies.Min(b => b.Samples[^1][0]);
        if (StopJd - StartJd < 360) throw new InvalidDataException("Incomplete NASA navigation range.");
        SunRadiusKm = data.SunRadiusKm;
        Range = data.Start + " / " + data.Stop;
        foreach (var b in bodies.Values)
        {
            if (!(b.RadiusKm > 0) || b.Samples.Length < 2 || b.Parent != "Sun" && !bodies.ContainsKey(b.Parent)
                || b.Parent == b.Id) throw new InvalidDataException("Invalid NASA body: " + b.Id);
            double previous = double.NegativeInfinity;
            foreach (var sample in b.Samples)
            {
                if (sample.Length != 7 || sample.Any(v => !double.IsFinite(v)) || sample[0] <= previous)
                    throw new InvalidDataException("Invalid NASA vector: " + b.Id);
                previous = sample[0];
            }
        }
        pathBodies=new(bodies,StringComparer.Ordinal);
        foreach(var guide in SolarOrbitGuide.Load())
        {
            var runtime=bodies[guide.Id];
            if(guide.Samples[0][0]>StartJd||guide.Samples[^1][0]<StopJd)throw new InvalidDataException("Orbit guide misses the runtime epoch range.");
            // Keep every runtime sample inside its original range, so paths and
            // bodies use precisely the same cubic there. Add guide epochs outside.
            var samples=guide.Samples.Where(s=>s[0]<runtime.Samples[0][0]||s[0]>runtime.Samples[^1][0]).Concat(runtime.Samples).OrderBy(s=>s[0]).ToArray();
            pathBodies[guide.Id]=runtime with{Samples=samples};guideDays.Add(guide.Id,guide.WindowDays);
        }
    }

    internal static SolarEphemeris? Load(out string? error)
    {
        try
        {
            using var file = Rookline.AssetProtection.AppAssets.Open("SolarNavigation/nasa-horizons.json.gz");
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            // Bound decompression before parsing a data file supplied with an installation.
            using var json = new MemoryStream(); var buffer = new byte[65536]; int read;
            while ((read = gzip.Read(buffer)) != 0)
            {
                if (json.Length + read > 32 * 1024 * 1024) throw new InvalidDataException("NASA navigation data exceeds its budget.");
                json.Write(buffer, 0, read);
            }
            var data = JsonSerializer.Deserialize<Data>(json.ToArray()) ?? throw new InvalidDataException("Empty NASA navigation data.");
            var result = new SolarEphemeris(data); error = null; return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or ArgumentException
            or IndexOutOfRangeException or NullReferenceException or KeyNotFoundException or UnauthorizedAccessException)
        { error = ex.Message; return null; }
    }

    internal double RadiusKm(string id) => id == "Sun" ? SunRadiusKm : bodies[id].RadiusKm;
    internal string? Parent(string id) => bodies.TryGetValue(id, out var body) && body.Parent != "Sun" ? body.Parent : null;
    internal Point3D Position(string id, double jd)
    {
        if (id == "Sun") return new();
        var relative = Relative(id, jd);
        return bodies[id].Parent == "Sun" ? relative : Position(bodies[id].Parent, jd) + (Vector3D)relative;
    }
    internal Point3D Relative(string id, double jd)
    {
        var samples = bodies[id].Samples;
        jd = Math.Clamp(jd, StartJd, StopJd);
        int lo = 0, hi = samples.Length - 1;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (samples[mid][0] <= jd) lo = mid; else hi = mid; }
        var a = samples[lo]; var b = samples[hi];
        double dt = b[0] - a[0], t = (jd - a[0]) / dt, t2 = t * t, t3 = t2 * t;
        double Coordinate(int axis) => (2 * t3 - 3 * t2 + 1) * a[axis] + (t3 - 2 * t2 + t) * dt * a[axis + 3]
            + (-2 * t3 + 3 * t2) * b[axis] + (t3 - t2) * dt * b[axis + 3];
        return new(Coordinate(1), Coordinate(2), Coordinate(3));
    }
    internal IEnumerable<SolarOrbitPath.Cubic> OrbitSpans(string id,double jd)
    {
        var data=pathBodies[id];var samples=data.Samples;
        double days=guideDays.TryGetValue(id,out double guided)?guided:id switch
        {"Mercury"=>87.969,"Venus"=>224.701,"Earth"=>365.256,"Luna"=>27.322,"Io"=>1.769,"Europa"=>3.551,"Ganymede"=>7.155,"Callisto"=>16.689,_=>365};
        double length=Math.Min(days,samples[^1][0]-samples[0][0]);
        double start=Math.Clamp(jd-length/2,samples[0][0],samples[^1][0]-length),stop=start+length;
        int lo=0,hi=samples.Length-1;
        while(hi-lo>1){int mid=(lo+hi)/2;if(samples[mid][0]<=start)lo=mid;else hi=mid;}
        for(int i=lo+1;i<samples.Length;i++)
        {
            var a=samples[i-1];var b=samples[i];if(b[0]<start)continue;if(a[0]>stop)break;
            double dt=b[0]-a[0],left=Math.Max(start,a[0]),right=Math.Min(stop,b[0]);if(right<=left)continue;
            var pa=new Point3D(a[1],a[2],a[3]);var pb=new Point3D(b[1],b[2],b[3]);
            var span=new SolarOrbitPath.Cubic(pa,pa+new Vector3D(a[4],a[5],a[6])*(dt/3),pb-new Vector3D(b[4],b[5],b[6])*(dt/3),pb);
            span=span.Range((left-a[0])/dt,(right-a[0])/dt);
            // Explicitly retain the current epoch as a vertex, including moons.
            if(jd>left&&jd<right){var split=span.Split((jd-left)/(right-left));yield return split.Left;yield return split.Right;}
            else yield return span;
        }
    }
    internal static double JulianDate(DateTimeOffset utc) => utc.ToUnixTimeMilliseconds() / 86400000d + 2440587.5 + 69.184 / 86400;
    internal static DateTimeOffset Utc(double jd) => DateTimeOffset.FromUnixTimeMilliseconds((long)((jd - 2440587.5 - 69.184 / 86400) * 86400000));

    internal object Check()
    {
        SolarOrbitPath.Check();
        using var checkpoints = JsonDocument.Parse(Rookline.AssetProtection.AppAssets.Read("SolarNavigation/nasa-checkpoints.json"));
        double maximumErrorKm = 0;
        foreach (var checkpoint in checkpoints.RootElement.EnumerateArray())
        {
            var expected = checkpoint.GetProperty("Position").EnumerateArray().Select(p => p.GetDouble()).ToArray();
            var actual = Relative(checkpoint.GetProperty("Id").GetString()!, checkpoint.GetProperty("JulianDate").GetDouble());
            double error = (actual - new Point3D(expected[0], expected[1], expected[2])).Length * AuKm;
            if (error > checkpoint.GetProperty("ToleranceKm").GetDouble()) throw new InvalidOperationException("Independent NASA interpolation checkpoint failed.");
            maximumErrorKm = Math.Max(maximumErrorKm, error);
        }
        foreach (var b in bodies.Values)
        {
            var first = b.Samples[0]; var at = Relative(b.Id, first[0]);
            if ((at - new Point3D(first[1], first[2], first[3])).Length > 1e-12)
                throw new InvalidOperationException("NASA vector endpoint mismatch.");
            var last = b.Samples[^1]; var end = Relative(b.Id, last[0]);
            if ((end - new Point3D(last[1], last[2], last[3])).Length > 1e-12)
                throw new InvalidOperationException("NASA final vector endpoint mismatch.");
            var middle = Relative(b.Id, (first[0] + b.Samples[1][0]) / 2);
            if (!double.IsFinite(middle.X) || middle.X == first[1] && middle.Y == first[2])
                throw new InvalidOperationException("NASA interpolation did not advance.");
            if (b.Parent != "Sun")
            {
                var difference = Position(b.Id, first[0]) - Position(b.Parent, first[0]);
                if ((difference - (Vector3D)at).Length > 1e-12) throw new InvalidOperationException("Moon detached from its NASA parent.");
            }
        }
        double earthMoonKm = ((Vector3D)Relative("Luna", StartJd)).Length * AuKm;
        if (earthMoonKm is < 330000 or > 420000 || RadiusKm("Earth") / AuKm > .00005)
            throw new InvalidOperationException("NASA navigation units changed.");
        return new { Source = "NASA/JPL Horizons", Range, Bodies = bodies.Count,
            ParentRelativeMoonVectors = true, CubicHermiteInterpolation = true, EndpointAndUnitChecks = true,
            IndependentHorizonsCheckpoints = checkpoints.RootElement.GetArrayLength(), MaximumCheckpointErrorKm = maximumErrorKm };
    }
}
