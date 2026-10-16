using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SensorPanel;

/// <summary>AU geometry, J2000 planetary elements. Decorative motion is not a current ephemeris.</summary>
internal static class SolarSystemModel
{
    internal const double AuKm = 149597870.7;
    internal sealed record Body(string Id, double Axis, double RadiusKm, Color Colour, double Eccentricity = 0,
        double Inclination = 0, double Longitude = 0, double Perihelion = 0, double Node = 0,
        string? Parent = null, bool Fictional = false)
    {
        internal double RadiusAu => RadiusKm / AuKm;
        internal bool Moon => Parent is not null;
        internal bool Planet => !Moon && !Fictional && Id is not ("Sun" or "Ceres" or "Eros");
        internal string Label => Id switch { "Sun" => "Sol", "SolGate" => "Sol Ring (fictional)",
            "Medina" => "Medina / Ring Space", "Tycho" => "Tycho (mobile station)", _ => Id };
    }
    // Planet elements: JPL approximate positions, Table 1, epoch J2000; Earth represents EM barycenter.
    // Moon orbits are circular mean-distance illustrations, not JPL ephemerides. Fiction has no AU position.
    internal static readonly Body[] Bodies =
    [
        new("Sun", 0, 695700, C(255, 194, 86)),
        new("Mercury", .38709927, 2439.4, C(166, 155, 139), .20563593, 7.00497902, 252.2503235, 77.45779628, 48.33076593),
        new("Venus", .72333566, 6051.8, C(225, 187, 119), .00677672, 3.39467605, 181.9790995, 131.60246718, 76.67984255),
        new("Earth", 1.00000261, 6371.0084, C(58, 143, 220), .01671123, -.00001531, 100.46457166, 102.93768193),
        new("Luna", 384400 / AuKm, 1737.4, C(186, 193, 199), Inclination: 5.145, Longitude: 50, Parent: "Earth"),
        new("Mars", 1.52371034, 3389.5, C(210, 101, 63), .09339410, 1.84969142, -4.55343205, -23.94362959, 49.55953891),
        new("Ceres", 2.7675, 469.7, C(153, 162, 155), .0758, 10.59, 130),
        new("Eros", 1.458, 8.4, C(171, 146, 112), .223, 10.829, 230),
        new("Tycho", 0, 0, C(88, 229, 234), Fictional: true),
        new("Jupiter", 5.202887, 69911, C(202, 165, 130), .04838624, 1.30439695, 34.39644051, 14.72847983, 100.47390909),
        new("Io", 421800 / AuKm, 1821.6, C(223, 182, 76), Longitude: 20, Parent: "Jupiter"),
        new("Ganymede", 1070400 / AuKm, 2634.1, C(153, 163, 170), Longitude: 195, Parent: "Jupiter"),
        new("Callisto", 1882700 / AuKm, 2410.3, C(119, 139, 148), Longitude: 278, Parent: "Jupiter"),
        new("Saturn", 9.53667594, 58232, C(210, 191, 138), .05386179, 2.48599187, 49.95424423, 92.59887831, 113.66242448),
        new("Phoebe", 12947918 / AuKm, 106.5, C(143, 149, 153), Inclination: 175, Longitude: 60, Parent: "Saturn"),
        new("Uranus", 19.18916464, 25362, C(113, 199, 210), .04725744, .77263783, 313.23810451, 170.95427630, 74.01692503),
        new("Neptune", 30.06992276, 24622, C(55, 100, 211), .00859048, 1.77004347, -55.12002969, 44.96476227, 131.78422574),
        new("SolGate", 0, 0, C(92, 225, 248), Fictional: true),
        new("Medina", 0, 0, C(255, 189, 98), Fictional: true)
    ];

    internal static Body Find(string id) => Bodies.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException("Unknown solar-system body.", nameof(id));

    internal static Point3D Position(Body body, double days = 0)
    {
        if (body.Fictional || body.Id == "Sun") return new();
        var period = body.Moon ? body.Id switch { "Luna" => 27.322, "Io" => 1.769,
            "Ganymede" => 7.155, "Callisto" => 16.689, _ => 550.48 } : 365.256 * Math.Pow(body.Axis, 1.5);
        var mean = Rad(body.Longitude - body.Perihelion) + days * Math.Tau / period;
        var eccentric = Math.IEEERemainder(mean, Math.Tau);
        for (var i = 0; i < 8; i++) eccentric -= (eccentric - body.Eccentricity * Math.Sin(eccentric) - Math.IEEERemainder(mean, Math.Tau))
            / (1 - body.Eccentricity * Math.Cos(eccentric));
        return OrbitPoint(body, eccentric, days);
    }

    internal static Point3D OrbitPoint(Body body, double eccentric, double days = 0)
    {
        var x = body.Axis * (Math.Cos(eccentric) - body.Eccentricity);
        var z = body.Axis * Math.Sqrt(1 - body.Eccentricity * body.Eccentricity) * Math.Sin(eccentric);
        var w = Rad(body.Perihelion - body.Node); var n = Rad(body.Node); var i = Rad(body.Inclination);
        var p = new Point3D((Math.Cos(w) * Math.Cos(n) - Math.Sin(w) * Math.Sin(n) * Math.Cos(i)) * x
            + (-Math.Sin(w) * Math.Cos(n) - Math.Cos(w) * Math.Sin(n) * Math.Cos(i)) * z,
            Math.Sin(w) * Math.Sin(i) * x + Math.Cos(w) * Math.Sin(i) * z,
            (Math.Cos(w) * Math.Sin(n) + Math.Sin(w) * Math.Cos(n) * Math.Cos(i)) * x
            + (-Math.Sin(w) * Math.Sin(n) + Math.Cos(w) * Math.Cos(n) * Math.Cos(i)) * z);
        if (body.Parent is not null) p += (Vector3D)Position(Find(body.Parent), days);
        return p;
    }

    internal static void Check()
    {
        if (Bodies.Select(b => b.Id).Distinct().Count() != Bodies.Length) throw new InvalidOperationException("Duplicate solar body.");
        foreach (var b in Bodies.Where(b => !b.Fictional && b.Axis > 0))
        {
            var parent = b.Parent is null ? new Point3D() : Position(Find(b.Parent));
            foreach (var phase in new[] { 0d, Math.PI })
            {
                var expected = b.Axis * (phase == 0 ? 1 - b.Eccentricity : 1 + b.Eccentricity);
                if (Math.Abs((OrbitPoint(b, phase) - parent).Length - expected) > 1e-10)
                    throw new InvalidOperationException("Solar orbit lost its AU scale: " + b.Id);
            }
            if (!double.IsFinite(Position(b, 120).X) || b.RadiusAu <= 0)
                throw new InvalidOperationException("Invalid solar geometry: " + b.Id);
        }
        if (Math.Abs(Find("Neptune").Axis / Find("Earth").Axis - 30.069844) > .0001
            || Math.Abs(Find("Earth").RadiusAu * AuKm - 6371.0084) > 1e-8)
            throw new InvalidOperationException("Solar distance and radius units diverged.");
    }
    private static double Rad(double degrees) => degrees * Math.PI / 180;
    private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
