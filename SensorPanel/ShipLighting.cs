using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SensorPanel;

// Light locations come from the hull's existing emissive mesh components.
// The added glow follows those fittings; hull paint and UVs remain untouched.
internal static class ShipLighting
{
    internal static Model3DGroup ForPortrait(IReadOnlyList<ShipModelPart> parts,Transform3D upright,Rect3D bounds)
    {
        var result=new Model3DGroup();
        var lamps=new List<(Point3D Centre,double Radius,Color Colour)>();
        foreach(var part in parts.Where(p=>p.SolidEmission.HasValue))
        {
            var mesh=part.Mesh;
            var weld=new Dictionary<(double,double,double),int>();
            var parents=new int[mesh.Positions.Count];
            for(int i=0;i<parents.Length;i++)
            {
                var p=mesh.Positions[i]; var key=(Math.Round(p.X,6),Math.Round(p.Y,6),Math.Round(p.Z,6));
                if(weld.TryGetValue(key,out int previous)) parents[i]=previous;
                else {parents[i]=i;weld[key]=i;}
            }
            int Root(int i) {while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
            for(int i=0;i<mesh.TriangleIndices.Count;i+=3)
            {
                int a=Root(mesh.TriangleIndices[i]),b=Root(mesh.TriangleIndices[i+1]),c=Root(mesh.TriangleIndices[i+2]);
                parents[b]=a;parents[c]=a;
            }
            foreach(var group in Enumerable.Range(0,parents.Length).GroupBy(Root))
            {
                var box=Rect3D.Empty;
                foreach(int i in group)box.Union(upright.Transform(mesh.Positions[i]));
                var centre=new Point3D(box.X+box.SizeX/2,box.Y+box.SizeY/2,box.Z+box.SizeZ/2);
                double radius=Math.Max(box.SizeX,Math.Max(box.SizeY,box.SizeZ))/2;
                if(radius>0 && radius<bounds.SizeY*.16)lamps.Add((centre,radius,part.SolidEmission!.Value));
            }
        }
        foreach(var lamp in lamps.OrderByDescending(l=>l.Radius).Take(64))
        {
            bool drive=lamp.Colour.B>lamp.Colour.R && lamp.Radius>.012 && lamp.Centre.Y<bounds.Y+bounds.SizeY*.14;
            if(drive)
            {
                var exit=new Point3D(lamp.Centre.X,bounds.Y-.003,lamp.Centre.Z);
                double length=Math.Min(bounds.SizeY*.09,lamp.Radius*4);
                result.Children.Add(Plume(exit,lamp.Radius*.82,length));
                var light=new PointLight(Color.FromScRgb(1,.02f,.16f,.42f),exit)
                    { Range=lamp.Radius*4,ConstantAttenuation=1,QuadraticAttenuation=80 };
                light.Freeze();result.Children.Add(light);
            }
            else
            {
                double r=Math.Clamp(lamp.Radius*.9,.003,.009);
                var mesh=new MeshGeometry3D();
                var p=lamp.Centre;
                mesh.Positions=new Point3DCollection {p+new Vector3D(r,0,0),p+new Vector3D(-r,0,0),
                    p+new Vector3D(0,r,0),p+new Vector3D(0,-r,0),p+new Vector3D(0,0,r),p+new Vector3D(0,0,-r)};
                mesh.TriangleIndices=new Int32Collection {0,2,4,4,2,1,1,2,5,5,2,0,4,3,0,1,3,4,5,3,1,0,3,5};
                mesh.Freeze();
                var material=new EmissiveMaterial(new SolidColorBrush(lamp.Colour));material.Freeze();
                var model=new GeometryModel3D(mesh,material);model.Freeze();result.Children.Add(model);
            }
        }
        result.Freeze();return result;
    }

    private static GeometryModel3D Plume(Point3D exit,double radius,double length)
    {
        var mesh=new MeshGeometry3D(); const int segments=20;
        foreach(var ring in new[] {(0d,radius),(.25d,radius*.78),(1d,0d)})
        for(int i=0;i<=segments;i++)
        {
            double a=i*2*Math.PI/segments;
            mesh.Positions.Add(exit+new Vector3D(Math.Cos(a)*ring.Item2,-length*ring.Item1,Math.Sin(a)*ring.Item2));
            mesh.TextureCoordinates.Add(new((double)i/segments,ring.Item1));
        }
        for(int row=0;row<2;row++)for(int i=0;i<segments;i++)
        {
            int a=row*(segments+1)+i,b=a+segments+1;
            foreach(int index in new[]{a,b,a+1,a+1,b,b+1})mesh.TriangleIndices.Add(index);
        }
        mesh.Freeze();
        var glow=new LinearGradientBrush {StartPoint=new(0,0),EndPoint=new(0,1),GradientStops=new GradientStopCollection {
            new(Color.FromArgb(220,185,245,255),0),new(Color.FromArgb(175,38,164,255),.24),
            new(Color.FromArgb(50,20,75,240),.65),new(Color.FromArgb(0,10,35,180),1)}};
        glow.Freeze();var material=new EmissiveMaterial(glow);material.Freeze();
        var model=new GeometryModel3D(mesh,material) {BackMaterial=material};model.Freeze();return model;
    }
}
