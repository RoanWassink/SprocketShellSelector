using System.Numerics;
namespace SprocketShellSelector;
internal static class PlateDeflection
{
    internal static Vector3 Bend(Vector3 direction,Vector3 normal,double maximumDegrees,double minimumObliquity)
    {
        if(direction.LengthSquared()<1e-8f || normal.LengthSquared()<1e-8f)return direction;
        direction=Vector3.Normalize(direction);normal=Vector3.Normalize(normal);
        var dot=Vector3.Dot(direction,normal);
        if(dot<0){normal=-normal;dot=-dot;}
        dot=Math.Clamp(dot,0,1);
        var angle=Math.Acos(dot)*180/Math.PI;
        if(angle<=minimumObliquity || angle>=89 || maximumDegrees<=0)return direction;
        var tangent=direction-normal*dot;
        if(tangent.LengthSquared()<1e-8f)return direction;
        tangent=Vector3.Normalize(tangent);
        var bend=Math.Min(maximumDegrees*(angle-minimumObliquity)/(89-minimumObliquity),89-angle);
        var target=(angle+bend)*Math.PI/180;
        return Vector3.Normalize(tangent*(float)Math.Sin(target)+normal*(float)Math.Cos(target));
    }
}
