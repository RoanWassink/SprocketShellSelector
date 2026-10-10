using System;
using System.Collections.Generic;
using System.Numerics;

namespace SprocketShellSelector;

// A bounded visual path and command-link budget. Not a rigid body or cable collision solver.
public sealed class WireCable
{
    private readonly List<Vector3> points = new();
    private Vector3 lastObserved;
    private double? finishedAt;
    private double lastFiniteTime;
    public double PaidOut { get; private set; }
    public string? BreakReason { get; private set; }
    public bool Connected => BreakReason == null && finishedAt == null;
    public IReadOnlyList<Vector3> Points => points;
    public double MaximumLength { get; }
    public double SampleDistance { get; }
    public int MaximumPoints { get; }
    public double RetentionSeconds { get; }

    public WireCable(Vector3 launchAnchor, Vector3 projectileStart, double maximumLength = 4000,
        double sampleDistance = 2, int maximumPoints = 256, double retentionSeconds = 20, double createdAt = 0)
    {
        if (!Finite(launchAnchor) || !Finite(projectileStart) || !Valid(maximumLength) || maximumLength <= 0 ||
            !Valid(sampleDistance) || sampleDistance <= 0 || maximumPoints < 4 || maximumPoints > 4096 ||
            !Valid(retentionSeconds) || retentionSeconds < 0 || !Valid(createdAt) || createdAt < 0) throw new ArgumentOutOfRangeException();
        MaximumLength = maximumLength; SampleDistance = sampleDistance; MaximumPoints = maximumPoints;
        RetentionSeconds = retentionSeconds; lastFiniteTime = createdAt;
        double initialLength = Vector3.Distance(launchAnchor, projectileStart);
        if (!Valid(initialLength)) throw new ArgumentOutOfRangeException(nameof(projectileStart), "Derived wire distance is nonfinite.");
        if (initialLength > MaximumLength) projectileStart = Vector3.Lerp(launchAnchor, projectileStart, (float)(MaximumLength / initialLength));
        points.Add(launchAnchor); points.Add(projectileStart);
        lastObserved = projectileStart; PaidOut = Math.Min(initialLength, MaximumLength);
        if (initialLength > MaximumLength) Break("wire-exhausted", createdAt);
    }

    public void Observe(Vector3 projectilePosition, double now, bool launcherHealthy, bool ownsGuidance)
    {
        if (!Valid(now) || now < 0 || !Finite(projectilePosition)) { Break("invalid-position", Valid(now) && now >= 0 ? now : lastFiniteTime); return; }
        lastFiniteTime = now;
        if (finishedAt != null || BreakReason != null) return;
        double step = Vector3.Distance(lastObserved, projectilePosition);
        if (!Valid(step)) { Break("invalid-distance", now); return; }
        double remaining = Math.Max(0, MaximumLength - PaidOut);
        bool exhausted = step > remaining;
        if (exhausted && step > 0) projectilePosition = Vector3.Lerp(lastObserved, projectilePosition, (float)(remaining / step));
        PaidOut += Math.Min(step, remaining);
        lastObserved = projectilePosition;
        // Store bends while retaining a live endpoint; compression never changes payout.
        if (Vector3.Distance(points[^2], projectilePosition) >= SampleDistance)
            points.Add(projectilePosition);
        else points[^1] = projectilePosition;
        if (points.Count > MaximumPoints)
        {
            var compact = new List<Vector3> { points[0] };
            for (int i = 2; i < points.Count - 1; i += 2) compact.Add(points[i]);
            compact.Add(points[^1]); points.Clear(); points.AddRange(compact);
        }
        if (exhausted) Break("wire-exhausted", now);
        else if (!launcherHealthy) Break("launcher-unavailable", now);
        else if (!ownsGuidance) Break("superseded", now);
    }

    public void Break(string reason, double now) { BreakReason ??= reason; Finish(now); }
    public void Finish(double now) { if (!Valid(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now)); finishedAt ??= now; }
    public bool Expired(double now) => finishedAt is double end && now >= end + RetentionSeconds;

    public Vector3[] RenderPoints(float sagMetres = 0)
    {
        if (!float.IsFinite(sagMetres) || sagMetres < 0 || sagMetres > 10) throw new ArgumentOutOfRangeException(nameof(sagMetres));
        // Visual sag is an approximation. Anchor and rocket endpoint remain exact.
        var result = points.ToArray();
        for (int i = 1; i < result.Length - 1; i++)
        {
            double t = (double)i / (result.Length - 1);
            result[i].Y -= sagMetres * (float)(4 * t * (1 - t));
        }
        return result;
    }

    private static bool Valid(double v) => double.IsFinite(v);
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
