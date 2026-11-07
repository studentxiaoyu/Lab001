using System;

/// <summary>Open-circuit, lossless ideal transformer. All voltages are AC RMS.</summary>
public static class TransformerPhysics
{
    public const int MinTurns=100, MaxTurns=1000;
    public const double MaxInputVoltage=24;
    public struct State
    {
        public int N1,N2;
        public bool Powered;
        public double SetVoltage,InputVoltage,OutputVoltage;
        public double TurnsRatio => (double)N2/N1;
        public double? VoltageRatio => InputVoltage>0 ? (double?)(OutputVoltage/InputVoltage) : null;
    }
    public static double ClampVoltage(double value)
    {
        if(double.IsNaN(value)||double.IsInfinity(value)) return 0;
        return Math.Max(0,Math.Min(MaxInputVoltage,value));
    }
    public static int ClampTurns(double value)
    {
        if(double.IsNaN(value)||double.IsInfinity(value)) return MinTurns;
        return (int)Math.Round(Math.Max(MinTurns,Math.Min(MaxTurns,value)),MidpointRounding.AwayFromZero);
    }
    public static State At(double inputVoltage,double n1,double n2,bool powered)
    {
        int primary=ClampTurns(n1),secondary=ClampTurns(n2);
        double setting=ClampVoltage(inputVoltage),actual=powered?setting:0;
        return new State{N1=primary,N2=secondary,Powered=powered,SetVoltage=setting,
            InputVoltage=actual,OutputVoltage=actual*secondary/primary};
    }
}
