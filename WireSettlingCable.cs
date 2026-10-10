using System;
using System.Collections.Generic;
using System.Numerics;

namespace SprocketShellSelector;

public readonly record struct WireGroundProbe(Vector3 Previous, Vector3 Candidate, int Vertex);
public readonly record struct WireGroundSurface(float Height, Vector3 Normal);

// Visual vertex gravity/contact. No tension, rigidbody, stretch solver or terrain snagging.
public sealed class WireSettlingCable
{
    private readonly Vector3[] points;
    private readonly Vector3[] velocities;
    public IReadOnlyList<Vector3> OriginalDetachPoints { get; }
    private double? detachedAt;
    private double? lastUpdatedAt;
    public bool Detached => detachedAt.HasValue;
    public IReadOnlyList<Vector3> Points => points;
    public double PaidOutSnapshot { get; }
    public double RetentionSeconds { get; }
    public const double MaximumStep = .25;
    public const int MaximumVertices = 512;

    public WireSettlingCable(IReadOnlyList<Vector3> frozenGeometry, double paidOut, double retentionSeconds)
    {
        if (frozenGeometry == null || frozenGeometry.Count < 2 || frozenGeometry.Count > MaximumVertices ||
            !double.IsFinite(paidOut) || paidOut < 0 || !double.IsFinite(retentionSeconds) || retentionSeconds < 0)
            throw new ArgumentOutOfRangeException();
        points = new Vector3[frozenGeometry.Count]; velocities = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            if (!Finite(frozenGeometry[i])) throw new ArgumentOutOfRangeException(nameof(frozenGeometry));
            points[i] = frozenGeometry[i];
        }
        PaidOutSnapshot = paidOut; RetentionSeconds = retentionSeconds;
        OriginalDetachPoints = Array.AsReadOnly((Vector3[])points.Clone());
    }

    public void WholeCableDetach(double now)
    {
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        detachedAt ??= now;
    }
    public bool Expired(double now) => detachedAt is double end && double.IsFinite(now) && now >= end + RetentionSeconds;

    public void Step(double deltaTime, double now, Func<WireGroundProbe, WireGroundSurface?>? ground,
        float gravity = 9.81f, float damping = .25f, float surfaceOffset = .012f)
    {
        if (!Detached || now < detachedAt || Expired(now)) return;
        if (!double.IsFinite(deltaTime) || deltaTime <= 0 || !double.IsFinite(now) || now < 0 ||
            !float.IsFinite(gravity) || gravity < 0 || gravity > 100 || !float.IsFinite(damping) || damping < 0 || damping > 100 ||
            !float.IsFinite(surfaceOffset) || surfaceOffset < 0 || surfaceOffset > .2f) return;
        if (lastUpdatedAt is double previousTime && now <= previousTime) return;
        lastUpdatedAt = now;
        double dt = Math.Min(deltaTime, MaximumStep);
        int steps = Math.Min(15, Math.Max(1, (int)Math.Ceiling(dt * 60)));
        double h = dt / steps;
        // Analytical damped gravity makes freefall independent of subdivision.
        double e = Math.Exp(-damping * h);
        double velocityTerm = damping > 1e-5 ? (1 - e) / damping : h;
        double gravityPositionTerm = damping > 1e-5 ? (h - velocityTerm) / damping : h * h * .5;
        for (int i = 0; i < points.Length; i++)
        {
            var previous = points[i]; var candidate = previous; var velocity = velocities[i];
            for (int j = 0; j < steps; j++)
            {
                candidate += velocity * (float)velocityTerm + new Vector3(0, (float)(-gravity * gravityPositionTerm), 0);
                velocity = velocity * (float)e + new Vector3(0, (float)(-gravity * velocityTerm), 0);
            }
            if (!Finite(candidate) || !Finite(velocity)) continue;
            WireGroundSurface? surface = null;
            // Query once per vertex, not once per substep. Unknown terrain remains unknown.
            if (ground != null) { try { surface = ground(new(previous, candidate, i)); } catch { surface = null; } }
            if (surface is {} hit && float.IsFinite(hit.Height) && Finite(hit.Normal) && hit.Normal.LengthSquared() > .001f &&
                candidate.Y <= hit.Height + surfaceOffset)
            {
                var normal = Vector3.Normalize(hit.Normal);
                if (normal.Y > .1f)
                {
                    candidate.Y = hit.Height + surfaceOffset;
                    // Vertical-column settling: no lateral slope slide or cable tension claim.
                    // Trusted cached terrain can arrive late after the vertex fell below it.
                    velocity = Vector3.Zero;
                }
            }
            points[i] = candidate; velocities[i] = velocity;
        }
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
