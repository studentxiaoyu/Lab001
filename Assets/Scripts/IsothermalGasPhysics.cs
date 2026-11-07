using System;

/// <summary>Closed ideal gas; fixed n. SI calculation, mL/kPa are display units only.
/// Quasistatic equilibria: no adiabatic transient, noise, leaks or invented measurements.</summary>
public static class IsothermalGasPhysics
{
    public const double GasConstant = 8.31446261815324;
    public const double AmountMoles = 0.02;
    public const double MinVolumeMl = 200, MaxVolumeMl = 1000;
    public const double MinKelvin = 250, MaxKelvin = 400;
    public const double BoreRadiusMetres = 0.035;
    public const double MaxPressurePa = 400000;

    public struct State
    {
        public readonly double VolumeM3, TemperatureK, PressurePa;
        public double VolumeMl => VolumeM3 * 1e6;
        public double PressureKPa => PressurePa / 1000;
        public double PV => PressurePa * VolumeM3;
        public double PistonHeightMetres => VolumeM3 / (Math.PI * BoreRadiusMetres * BoreRadiusMetres);
        internal State(double volumeMl, double kelvin)
        {
            VolumeM3 = volumeMl * 1e-6;
            TemperatureK = kelvin;
            PressurePa = AmountMoles * GasConstant * kelvin / VolumeM3;
            if (PressurePa > MaxPressurePa) throw new InvalidOperationException("Pressure exceeds the apparatus range.");
        }
    }

    public static State At(double volumeMl, double kelvin)
        => new State(Limit(volumeMl, MinVolumeMl, MaxVolumeMl), Limit(kelvin, MinKelvin, MaxKelvin));

    private static double Limit(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        return Math.Max(min, Math.Min(max, value));
    }

    public static bool SameIsotherm(double recordedKelvin, bool recordedIsothermal, double selectedKelvin)
        => recordedIsothermal && Math.Abs(recordedKelvin - selectedKelvin) < 0.0001;
}
