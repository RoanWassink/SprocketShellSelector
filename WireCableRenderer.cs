using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SprocketShellSelector;

// Call from the main Unity thread; owns its GameObject only, never projectile/native materials.
public sealed class WireCableRenderer : IDisposable
{
    private readonly GameObject root;
    private readonly LineRenderer line;
    public WireCableRenderer(Material suppliedMaterial, int layer, float displayWidth = .008f)
    {
        if (suppliedMaterial == null) throw new ArgumentNullException(nameof(suppliedMaterial));
        if (!float.IsFinite(displayWidth) || displayWidth <= 0 || displayWidth > .1f)
            throw new ArgumentOutOfRangeException(nameof(displayWidth));
        root = new GameObject("Shell wire cable"); root.layer = layer;
        try
        {
            line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.loop = false;
            line.startWidth = displayWidth; line.endWidth = displayWidth;
            line.sharedMaterial = suppliedMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
        }
        catch { Object.Destroy(root); throw; }
    }
    public void Update(WireCable cable, float visualSag = .15f)
    {
        var vertices = cable.RenderPoints(visualSag);
        UpdatePoints(vertices);
    }
    public void UpdatePoints(System.Collections.Generic.IReadOnlyList<System.Numerics.Vector3> vertices)
    {
        line.positionCount = vertices.Count;
        for (int i = 0; i < vertices.Count; i++)
            line.SetPosition(i, new Vector3(vertices[i].X, vertices[i].Y, vertices[i].Z));
    }
    public void Dispose() { if (root != null) Object.Destroy(root); }
}
