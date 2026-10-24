using System.Windows;
using System.Windows.Media.Media3D;

namespace SensorPanel;

// Project the same Hermite ephemeris used for bodies. Subdivide its exact cubic
// control hull in screen space, then clip every emitted line to the viewport.
internal static class SolarOrbitPath
{
    internal readonly record struct Cubic(Point3D A,Point3D B,Point3D C,Point3D D)
    {
        internal (Cubic Left,Cubic Right) Split(double t)
        {
            static Point3D Mix(Point3D a,Point3D b,double amount)=>a+(b-a)*amount;
            var ab=Mix(A,B,t);var bc=Mix(B,C,t);var cd=Mix(C,D,t);
            var abc=Mix(ab,bc,t);var bcd=Mix(bc,cd,t);var point=Mix(abc,bcd,t);
            return (new(A,ab,abc,point),new(point,bcd,cd,D));
        }
        internal Cubic Range(double start,double stop)
        {
            var span=this;
            if(stop<1)span=span.Split(stop).Left;
            if(start>0)span=span.Split(start/stop).Right;
            return span;
        }
    }
    internal readonly record struct Segment(Point A,Point B);
    internal static Segment[] Project(IEnumerable<Cubic> spans,Func<Point3D,Point> project,Rect view)
    {
        var segments=new List<Segment>();
        foreach(var span in spans)Visit(project(span.A),project(span.B),project(span.C),project(span.D),0);
        return segments.ToArray();
        void Visit(Point a,Point b,Point c,Point d,int depth)
        {
            double left=Math.Min(Math.Min(a.X,b.X),Math.Min(c.X,d.X)),right=Math.Max(Math.Max(a.X,b.X),Math.Max(c.X,d.X));
            double top=Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y)),bottom=Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y));
            // A Bezier curve cannot leave the convex hull of its controls.
            if(right<view.Left||left>view.Right||bottom<view.Top||top>view.Bottom)return;
            if(depth>=18||Math.Max(Distance(b,a,d),Distance(c,a,d))<=.3)
            {
                if(ClipLine(a,d,view,out var entry,out var exit))segments.Add(new(entry,exit));
                return;
            }
            var ab=a+(b-a)*.5;var bc=b+(c-b)*.5;var cd=c+(d-c)*.5;
            var abc=ab+(bc-ab)*.5;var bcd=bc+(cd-bc)*.5;var middle=abc+(bcd-abc)*.5;
            Visit(a,ab,abc,middle,depth+1);Visit(middle,bcd,cd,d,depth+1);
        }
    }
    internal static double Distance(Point p,Point a,Point b)
    {
        var direction=b-a;double t=direction.LengthSquared>0?Math.Clamp(Vector.Multiply(p-a,direction)/direction.LengthSquared,0,1):0;
        return (p-(a+direction*t)).Length;
    }
    internal static bool ClipLine(Point a,Point b,Rect view,out Point start,out Point end)
    {
        double lo=0,hi=1,dx=b.X-a.X,dy=b.Y-a.Y;
        bool Edge(double p,double q)
        {
            if(p==0)return q>=0;double t=q/p;
            if(p<0){if(t>hi)return false;lo=Math.Max(lo,t);}else{if(t<lo)return false;hi=Math.Min(hi,t);}return true;
        }
        start=end=default;
        if(!Edge(-dx,a.X-view.Left)||!Edge(dx,view.Right-a.X)||!Edge(-dy,a.Y-view.Top)||!Edge(dy,view.Bottom-a.Y))return false;
        start=new(Math.Clamp(a.X+lo*dx,view.Left,view.Right),Math.Clamp(a.Y+lo*dy,view.Top,view.Bottom));
        end=new(Math.Clamp(a.X+hi*dx,view.Left,view.Right),Math.Clamp(a.Y+hi*dy,view.Top,view.Bottom));return true;
    }
    internal static void Check()
    {
        var span=new Cubic(new(0,0,0),new(0,100,0),new(100,100,0),new(100,0,0));
        var split=span.Split(.37);var lines=Project(new[]{split.Left,split.Right},p=>new(p.X,p.Y),new Rect(-1,-1,102,102));
        if(lines.Length<=8||!lines.Any(line=>(line.A-new Point(split.Left.D.X,split.Left.D.Y)).Length<1e-9
            ||(line.B-new Point(split.Left.D.X,split.Left.D.Y)).Length<1e-9))throw new InvalidOperationException("Orbit curve lost its exact body-time anchor.");
        if(!ClipLine(new(-1e12,30),new(1e12,30),new Rect(0,0,100,60),out var start,out var end)
            ||(start-new Point(0,30)).Length>.001||(end-new Point(100,30)).Length>.001)throw new InvalidOperationException("Large orbit coordinates escaped clipping.");
        if(Project(new[]{new Cubic(new(-100,-50,0),new(-90,-45,0),new(-70,-40,0),new(-50,-30,0))},p=>new(p.X,p.Y),new Rect(0,0,100,60)).Length!=0)
            throw new InvalidOperationException("An invisible orbit curve was submitted.");
    }
}
