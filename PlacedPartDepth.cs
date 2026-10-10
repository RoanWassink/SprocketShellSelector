using System.Numerics;
namespace SprocketShellSelector;
// Calibration for the supplied closed cuboid prototype collision assets; never a native penetration path.
internal static class PlacedPartDepth
{
    internal static double Measure(IReadOnlyList<Vector3> vertices,IReadOnlyList<int> triangles,Vector3 scale)
    {
        if(vertices.Count<3||vertices.Count>2000||triangles.Count<3||triangles.Count>6000||triangles.Count%3!=0||!Finite(scale)||scale.X==0||scale.Y==0||scale.Z==0)return double.NaN;
        double shortest=double.PositiveInfinity;
        for(int i=0;i<triangles.Count;i+=3)
        {
            for(int j=0;j<3;j++)
            {
                int a=triangles[i+j],b=triangles[i+(j+1)%3];if(a<0||b<0||a>=vertices.Count||b>=vertices.Count||!Finite(vertices[a])||!Finite(vertices[b]))return double.NaN;
                var edge=Vector3.Multiply(vertices[a]-vertices[b],scale);double length=edge.Length();
                if(length<=.000001||!double.IsFinite(length))return double.NaN;
                shortest=Math.Min(shortest,length);
            }
        }
        return shortest*1000;
    }
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
