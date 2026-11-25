using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SensorPanel;

// Original low-cost model of the alien control station and a representative gate.
internal static class RingStationModel
{
    internal static Model3DGroup Build()
    {
        var model = new Model3DGroup();
        model.Children.Add(new GeometryModel3D(SolarBodyAssets.Sphere(), Metal(Color.FromRgb(12, 19, 32), 4)));

        Color[] finishes = [Color.FromRgb(31, 44, 61), Color.FromRgb(40, 59, 76),
            Color.FromRgb(55, 74, 89), Color.FromRgb(66, 87, 99), Color.FromRgb(40, 67, 80)];
        var shells = finishes.Select(_ => new MeshGeometry3D()).ToArray();
        var channels = new MeshGeometry3D();
        Vector3D[] axes = [new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)];
        const int cells = 13;
        for (int side = 0; side < axes.Length; side++)
        {
            Vector3D normal = axes[side];
            Vector3D across = Vector3D.CrossProduct(Math.Abs(normal.Y) > .9 ? new(0, 0, 1) : new(0, 1, 0), normal);
            Vector3D up = Vector3D.CrossProduct(normal, across);
            for (int row = 0; row < cells; row++)
            for (int column = 0; column < cells; column++)
            {
                int pattern = (side * 29 + row * 17 + column * 43 + row * column * 3) % 31;
                double x = -1 + (column + .5) * 2 / cells, y = -1 + (row + .5) * 2 / cells;
                double edge = 1d / cells;
                double radius = 1.014 + .003 * (pattern % 6);
                Panel(shells[pattern % shells.Length], x - .94 * edge, y - .94 * edge, x + .94 * edge, y + .94 * edge, radius);
                if ((row == 2 || row == 9 || column == 5) && pattern % 5 < 3)
                    Panel(channels, x - .64 * edge, y - .06 * edge, x + .63 * edge, y + .065 * edge, radius + .003);
            }

            void Panel(MeshGeometry3D mesh, double left, double bottom, double right, double top, double radius)
            {
                int first = mesh.Positions.Count;
                foreach (var (x, y) in new[] { (left, bottom), (right, bottom), (right, top), (left, top) })
                {
                    Vector3D point = normal + across * x + up * y;
                    point.Normalize();
                    mesh.Positions.Add((Point3D)(point * radius));
                    mesh.Normals.Add(point);
                }
                foreach (int corner in new[] { 0, 1, 2, 0, 2, 3 }) mesh.TriangleIndices.Add(first + corner);
            }
        }
        for (int i = 0; i < shells.Length; i++) model.Children.Add(new GeometryModel3D(shells[i], Metal(finishes[i], 19)));
        model.Children.Add(new GeometryModel3D(channels, new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(80, 217, 232)))));
        model.Freeze();
        return model;
    }

    internal static Model3DGroup BuildGateMarker()
    {
        var model = new Model3DGroup();
        var mesh = new MeshGeometry3D();
        const int segments = 64, sides = 8;
        for (int i = 0; i < segments; i++)
        for (int j = 0; j < sides; j++)
        {
            double angle = i * Math.Tau / segments, tube = j * Math.Tau / sides;
            var normal = new Vector3D(Math.Cos(angle) * Math.Cos(tube), Math.Sin(angle) * Math.Cos(tube), Math.Sin(tube));
            mesh.Positions.Add(new(Math.Cos(angle) + .055 * normal.X, Math.Sin(angle) + .055 * normal.Y, .055 * normal.Z));
            mesh.Normals.Add(normal);
            int a = i * sides + j, b = (i + 1) % segments * sides + j;
            int c = (i + 1) % segments * sides + (j + 1) % sides, d = i * sides + (j + 1) % sides;
            foreach (int index in new[] { a, b, c, a, c, d }) mesh.TriangleIndices.Add(index);
        }
        model.Children.Add(new GeometryModel3D(mesh, Metal(Color.FromRgb(45, 159, 179), 20)));
        model.Freeze();
        return model;
    }

    private static Material Metal(Color color, double exponent)
    {
        var material = new MaterialGroup();
        material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
        material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(110, 155, 223, 250)), exponent));
        material.Freeze();
        return material;
    }
}
