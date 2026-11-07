using System;
using System.Collections.Generic;

/// <summary>SI units. Positive direction is down the board, towards the pulley.
/// Massless inextensible rope, ideal pulley, constant gravity, mu_static = mu_kinetic.
/// The falling bucket and cart share the same displacement. No fictitious force data.</summary>
public static class AccelerationDynamics
{
    public const double Gravity = 9.81;
    public const double SampleInterval = 0.02;

    [Serializable]
    public struct Settings
    {
        public double CartMass, HangingMass, Friction, AngleDegrees;
        public Settings(double cart, double hanging, double friction, double angle)
        {
            CartMass = Limit(cart, 0.2, 2.0);
            HangingMass = Limit(hanging, 0, 0.25);
            Friction = Limit(friction, 0, 0.3);
            AngleDegrees = Limit(angle, 0, 12);
        }
        private static double Limit(double x, double lo, double hi)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new ArgumentOutOfRangeException(nameof(x));
            return Math.Max(lo, Math.Min(hi, x));
        }
    }

    public struct Solution
    {
        public double Acceleration, Tension, CartNetForce, FrictionForce, CompensationError;
    }

    public static Solution Solve(Settings input)
    {
        var p = new Settings(input.CartMass, input.HangingMass, input.Friction, input.AngleDegrees);
        double angle = p.AngleDegrees * Math.PI / 180;
        double downhill = p.CartMass * Gravity * Math.Sin(angle);
        double frictionLimit = p.Friction * p.CartMass * Gravity * Math.Cos(angle);
        double drive = p.HangingMass * Gravity + downhill;
        double a = Math.Max(0, (drive - frictionLimit) / (p.CartMass + p.HangingMass));
        return new Solution {
            Acceleration = a,
            Tension = p.HangingMass * (Gravity - a),
            CartNetForce = p.CartMass * a,
            FrictionForce = a > 0 ? frictionLimit : drive,
            CompensationError = downhill - frictionLimit
        };
    }

    [Serializable]
    public struct Sample
    {
        public double Time, Position, Velocity;
        public Sample(double t, double a) { Time = t; Position = 0.5 * a * t * t; Velocity = a * t; }
    }

    public static double Duration(Solution s, double travel)
    {
        if (travel <= 0 || double.IsNaN(travel) || double.IsInfinity(travel))
            throw new ArgumentOutOfRangeException(nameof(travel));
        return s.Acceleration > 1e-9 ? Math.Min(20, Math.Sqrt(2 * travel / s.Acceleration)) : 2;
    }

    // Only equally spaced, pre-impact paper-tape points enter this second difference.
    public static double MeasuredAcceleration(IList<Sample> points)
    {
        if (points.Count < 3) throw new ArgumentException("At least three tape points required.");
        double sum = 0;
        for (int i = 2; i < points.Count; i++)
            sum += (points[i].Position - 2 * points[i - 1].Position + points[i - 2].Position)
                 / (SampleInterval * SampleInterval);
        return sum / (points.Count - 2);
    }

    public static bool SameForceSeries(Settings a, Settings b)
        => Close(a.CartMass, b.CartMass) && Close(a.Friction, b.Friction) && Close(a.AngleDegrees, b.AngleDegrees);
    public static bool SameMassSeries(Settings a, Settings b)
        => Close(a.HangingMass, b.HangingMass) && Close(a.Friction, b.Friction) && Close(a.AngleDegrees, b.AngleDegrees);
    private static bool Close(double a, double b) => Math.Abs(a - b) < 1e-5;
}
