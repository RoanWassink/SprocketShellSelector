using System.Numerics;

namespace SprocketShellSelector;

internal sealed record AtgmSettings(double FlightSpeed = 200, double MaxTurnRate = 20,
    double MaximumFlightTime = 25, double GuidanceDelay = .25, string GuidanceMode = "sight",
    double? LaunchSpeed = null, string LaunchSpeedMode = "fixed", double LaunchSpeedMultiplier = 1,
    double Acceleration = 0, double MotorDelay = 0, double MotorBurnTime = 0, double CoastDeceleration = 0);

// Gameplay guidance only: bounded rotation, constant powered speed, no target lock.
internal static class AtgmGuidance
{
    internal static double InitialSpeed(AtgmSettings s, double cannonVelocity)
    {
        if (!double.IsFinite(cannonVelocity) || cannonVelocity < 0) throw new ArgumentOutOfRangeException(nameof(cannonVelocity));
        return s.LaunchSpeedMode == "cannon"
            ? Math.Clamp(cannonVelocity*s.LaunchSpeedMultiplier,10,s.FlightSpeed)
            : s.LaunchSpeed ?? s.FlightSpeed;
    }
    internal static double SpeedAtAge(AtgmSettings s, double initialSpeed, double age)
    {
        if (!double.IsFinite(initialSpeed) || !double.IsFinite(age) || initialSpeed < 0 || age < 0)
            throw new ArgumentOutOfRangeException(nameof(age));
        var poweredAge=Math.Max(0,age-s.MotorDelay);
        var burnAge=s.MotorBurnTime>0 ? Math.Min(poweredAge,s.MotorBurnTime) : poweredAge;
        var peak=Math.Min(s.FlightSpeed,initialSpeed+s.Acceleration*burnAge);
        var coastAge=s.MotorBurnTime>0 ? Math.Max(0,poweredAge-s.MotorBurnTime) : 0;
        return Math.Max(10,peak-s.CoastDeceleration*coastAge);
    }
    internal static Vector3 AiIntercept(Vector3 target, Vector3 estimatedVelocity, Vector3 launch, AtgmSettings s)
    {
        // Game AI prediction only: retain its estimated target/velocity, omit shell drop.
        var point=target;
        for(var i=0;i<3;i++)point=target+estimatedVelocity*(Vector3.Distance(point,launch)/(float)s.FlightSpeed);
        return point;
    }
    internal static bool FreshAim(double now,double sampleTime) => double.IsFinite(now) && double.IsFinite(sampleTime) && now>=sampleTime && now-sampleTime<.3;
    internal static Vector3 AimAlongRay(Vector3 origin, Vector3 direction, Vector3 missilePosition, double flightSpeed)
    {
        var axis=Vector3.Normalize(direction);
        var along=Vector3.Dot(missilePosition-origin,axis);
        // Aim one second ahead on the sight ray, rather than chase a fixed convergence point.
        return origin+axis*Math.Max((float)flightSpeed,along+(float)flightSpeed);
    }
    internal static void Validate(ShellProfile p)
    {
        var s = p.Atgm;
        if (s is null) return;
        void Range(string field, double value, double min, double max)
        {
            if (!double.IsFinite(value) || value < min || value > max)
                throw new FormatException(ShellBallistics.RangeError(p.Id, field, value, min, max));
        }
        Range("flightSpeed", s.FlightSpeed, 50, 1000);
        if (s.LaunchSpeed is {} launch) Range("launchSpeed", launch, 10, s.FlightSpeed);
        Range("launchSpeedMultiplier", s.LaunchSpeedMultiplier, .01, 4);
        Range("acceleration", s.Acceleration, 0, 2000);
        Range("motorBurnTime", s.MotorBurnTime, 0, 60);
        Range("coastDeceleration", s.CoastDeceleration, 0, 100);
        Range("motorDelay", s.MotorDelay, 0, 5);
        if (s.Acceleration > 0 && s.MotorDelay >= s.MaximumFlightTime)
            throw new FormatException($"Profile '{p.Id}': motorDelay must be less than maximumFlightTime when acceleration is enabled.");
        if (s.LaunchSpeedMode is not ("fixed" or "cannon"))
            throw new FormatException($"Profile '{p.Id}': launchSpeedMode is '{s.LaunchSpeedMode}'; expected fixed or cannon.");
        Range("maxTurnRate", s.MaxTurnRate, 0, 90);
        Range("maximumFlightTime", s.MaximumFlightTime, 1, 60);
        Range("guidanceDelay", s.GuidanceDelay, 0, 5);
        if (s.GuidanceDelay >= s.MaximumFlightTime)
            throw new FormatException(FormattableString.Invariant($"Profile '{p.Id}': guidanceDelay is {s.GuidanceDelay}; expected less than maximumFlightTime ({s.MaximumFlightTime})."));
        if (s.GuidanceMode is not ("sight" or "keyboard" or "none"))
            throw new FormatException($"Profile '{p.Id}': guidanceMode is '{s.GuidanceMode}'; expected sight, keyboard or none.");
    }

    internal static Vector3 KeyboardStep(Vector3 heading, double yaw, double pitch, AtgmSettings settings, double dt)
    {
        var current = Vector3.Normalize(Step(heading, null, settings, dt));
        if (!double.IsFinite(yaw) || !double.IsFinite(pitch)) throw new ArgumentOutOfRangeException(nameof(yaw));
        if (settings.GuidanceMode != "keyboard") return current * (float)settings.FlightSpeed;
        yaw = Math.Clamp(yaw, -1, 1); pitch = Math.Clamp(pitch, -1, 1);
        var right = Vector3.Cross(Vector3.UnitY, current);
        // At a vertical heading keep a finite pitch axis instead of dividing by zero.
        right = right.LengthSquared() < 1e-8f ? Vector3.UnitX : Vector3.Normalize(right);
        var axis = Vector3.UnitY * (float)yaw - right * (float)pitch;
        var strength = Math.Min(1, Math.Sqrt(yaw*yaw + pitch*pitch));
        if (axis.LengthSquared() > 1e-8f && strength > 0)
            current = Vector3.Normalize(Vector3.Transform(current, Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis),
                (float)(strength * settings.MaxTurnRate * Math.PI / 180 * dt))));
        return current * (float)settings.FlightSpeed;
    }

    internal static Vector3 Step(Vector3 heading, Vector3? desiredDirection, AtgmSettings settings, double deltaSeconds)
    {
        static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        if (!Finite(heading) || heading.LengthSquared() < 1e-8f || !double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(heading));
        var current = Vector3.Normalize(heading);
        if (desiredDirection is { } desired && Finite(desired) && desired.LengthSquared() > 1e-8f && settings.GuidanceMode == "sight")
        {
            var target = Vector3.Normalize(desired);
            var dot = Math.Clamp(Vector3.Dot(current, target), -1, 1);
            // Do not chase an aim point behind the rocket or make a U-turn after passing it.
            if (dot > 0)
            {
                var angle = Math.Acos(dot);
                var allowed = settings.MaxTurnRate * Math.PI / 180 * deltaSeconds;
                if (angle <= allowed) current = target;
                else if (allowed > 0 && angle > 1e-6)
                {
                    var axis = Vector3.Normalize(Vector3.Cross(current, target));
                    current = Vector3.Normalize(Vector3.Transform(current, Quaternion.CreateFromAxisAngle(axis, (float)allowed)));
                }
            }
        }
        return current * (float)settings.FlightSpeed;
    }
}
