using UnityEngine;
using NVector=System.Numerics.Vector3;
namespace SprocketShellSelector;

// Real native surfaces only. Moving/non-terrain surfaces are revalidated.
internal sealed class WireGroundSampler
{
    private readonly NVector[] columns;
    private readonly WireGroundSurface?[] surfaces;
    private readonly Collider?[] colliders;
    private readonly float[] nextQuery;
    private readonly bool[] terrain;
    private static float frameTime=float.NaN;
    private static int remainingCasts;
    internal WireGroundSampler(IReadOnlyList<NVector> points)
    {
        columns=points.ToArray();surfaces=new WireGroundSurface?[points.Count];
        colliders=new Collider?[points.Count];nextQuery=new float[points.Count];terrain=new bool[points.Count];
    }
    internal static void BeginFrame(float now)
    {if(frameTime!=now){frameTime=now;remainingCasts=128;}}
    internal WireGroundSurface? Surface(int index,float now)
    {
        if(index<0||index>=columns.Length)return null;
        bool valid=colliders[index]!=null;
        if(valid&&terrain[index])return surfaces[index];
        if(now<nextQuery[index]&&(valid||surfaces[index]==null))return valid?surfaces[index]:null;
        // Never hold a stale moving roof as a phantom floor when the budget is exhausted.
        if(remainingCasts<=0)return null;
        remainingCasts--;
        var p=columns[index];
        var origin=new Vector3(p.X,p.Y+.25f,p.Z);
        surfaces[index]=null;colliders[index]=null;terrain[index]=false;nextQuery[index]=now+1;
        if(Physics.Raycast(origin,Vector3.down,out RaycastHit hit,2000f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
            &&float.IsFinite(hit.point.y))
        {
            var n=hit.normal;
            surfaces[index]=new WireGroundSurface(hit.point.y,new NVector(n.x,n.y,n.z));
            colliders[index]=hit.collider;
            terrain[index]=hit.collider?.TryCast<TerrainCollider>()!=null&&hit.collider.attachedRigidbody==null;
            nextQuery[index]=now+.25f;
        }
        return surfaces[index];
    }
}
