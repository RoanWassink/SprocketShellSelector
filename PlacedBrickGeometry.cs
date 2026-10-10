using System.Numerics;
namespace SprocketShellSelector;
internal static class PlacedBrickGeometry
{
    internal static readonly Vector3[] Corners={new(-.212f,.026f,-.13f),new(.212f,.026f,-.13f),new(.212f,.096f,-.13f),new(-.212f,.096f,-.13f),new(-.212f,.026f,.13f),new(.212f,.026f,.13f),new(.212f,.096f,.13f),new(-.212f,.096f,.13f)};
    internal static readonly int[] Triangles={0,3,2,0,2,1,4,5,6,4,6,7,0,4,7,0,7,3,1,2,6,1,6,5,0,1,5,0,5,4,3,7,6,3,6,2};
    internal static bool MeshMatches(IReadOnlyList<Vector3> vertices,IReadOnlyList<int> triangles)=>vertices.Count==36&&triangles.Count==36&&Enumerable.Range(0,36).All(i=>triangles[i]==i&&Vector3.DistanceSquared(vertices[i],Corners[Triangles[i]])<1e-12f);
    internal static bool UnitScale(Vector3 scale)=>Finite(scale)&&Math.Abs(Math.Abs(scale.X)-1)<.001&&Math.Abs(Math.Abs(scale.Y)-1)<.001&&Math.Abs(Math.Abs(scale.Z)-1)<.001;
    private static float Axis(Vector3 v,int i)=>i==0?v.X:i==1?v.Y:v.Z;
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    // Actual local surface entry and native own exit must describe the same box chord.
    internal static bool Chord(Vector3 entry,Vector3 exit,Vector3 direction,out double physicalDepthMm)
    {
        physicalDepthMm=0;if(!Finite(entry)||!Finite(exit)||!Finite(direction)||direction.LengthSquared()<.9f)return false;
        var low=Corners[0];var high=Corners[6];float next=float.PositiveInfinity;bool inwardSurface=false;
        for(int i=0;i<3;i++)
        {
            float e=Axis(entry,i),d=Axis(direction,i),lo=Axis(low,i),hi=Axis(high,i);
            if(e<lo-.001f||e>hi+.001f)return false;
            if(Math.Abs(e-lo)<.001f&&d>1e-5f||Math.Abs(e-hi)<.001f&&d< -1e-5f)inwardSurface=true;
            if(Math.Abs(d)>1e-5f)next=Math.Min(next,((d>0?hi:lo)-e)/d);
        }
        if(!inwardSurface||!float.IsFinite(next)||next<=.001f)return false;
        var preceding=entry-direction*.003f;
        if(preceding.X>=low.X&&preceding.X<=high.X&&preceding.Y>=low.Y&&preceding.Y<=high.Y&&preceding.Z>=low.Z&&preceding.Z<=high.Z)return false;
        if(Vector3.DistanceSquared(entry+direction*next,exit)>.000004f)return false;
        physicalDepthMm=(high.Y-low.Y)*1000;return true;
    }
    internal readonly record struct Boundary(int Object,int Structure,float Distance,bool Enter,bool Exit,bool Inside);
    internal static int OwnExit(IReadOnlyList<Boundary> hits,int entryIndex,float reachedBlockExit)
    {
        if(entryIndex<0||entryIndex>=hits.Count)return -1;var e=hits[entryIndex];
        if(!e.Enter||e.Exit||!float.IsFinite(e.Distance)||!float.IsFinite(reachedBlockExit))return -1;
        int result=-1;float closest=float.PositiveInfinity;
        for(int i=0;i<hits.Count;i++)
        {
            var h=hits[i];if(h.Object!=e.Object||h.Structure!=e.Structure||!float.IsFinite(h.Distance)||h.Distance<=e.Distance+.00001f||h.Distance>reachedBlockExit+.0001f)continue;
            if(h.Distance<closest){closest=h.Distance;result=i;}
        }
        if(result>=0&&hits.Count(h=>h.Object==e.Object&&h.Structure==e.Structure&&Math.Abs(h.Distance-closest)<.00001f)>1)return -1;
        return result>=0&&hits[result].Exit&&!hits[result].Enter?result:-1;
    }
}
