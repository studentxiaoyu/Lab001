using System;
public static class GasPhysicsChecks
{
    static int count;
    static void Check(bool ok,string name) { if(!ok)throw new Exception(name);count++; }
    static bool Near(double a,double b,double tolerance=1e-8)=>Math.Abs(a-b)<tolerance;
    public static void Main()
    {
        var a=IsothermalGasPhysics.At(500,300);
        var b=IsothermalGasPhysics.At(250,300);
        Check(Near(a.PressurePa,IsothermalGasPhysics.AmountMoles*IsothermalGasPhysics.GasConstant*300/.0005),"SI equation");
        Check(Near(b.PressurePa,2*a.PressurePa),"half V doubles p");
        Check(Near(IsothermalGasPhysics.At(1000,300).PressurePa,a.PressurePa/2),"double V halves p");
        Check(Near(IsothermalGasPhysics.At(500,350).PressurePa/a.PressurePa,350.0/300),"Kelvin pressure ratio");
        Check(Near(a.PV,b.PV),"constant pV isotherm");
        Check(!Near(a.PV,IsothermalGasPhysics.At(500,350).PV),"nonisothermal pV differs");
        Check(Near(a.VolumeM3,.0005),"mL to m3");
        Check(Near(a.PressureKPa,a.PressurePa/1000),"Pa to kPa");
        Check(Near(IsothermalGasPhysics.At(500,273.15).TemperatureK-273.15,0),"Celsius display only");
        var limits=IsothermalGasPhysics.At(0,-273.15);
        Check(Near(limits.VolumeMl,200)&&Near(limits.TemperatureK,250),"lower input boundaries");
        limits=IsothermalGasPhysics.At(1e8,1e8);
        Check(Near(limits.VolumeMl,1000)&&Near(limits.TemperatureK,400),"upper input boundaries");
        foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {
            bool bad=false;try {IsothermalGasPhysics.At(invalid,300);}catch(ArgumentOutOfRangeException){bad=true;}
            Check(bad,"invalid volume");bad=false;
            try {IsothermalGasPhysics.At(500,invalid);}catch(ArgumentOutOfRangeException){bad=true;}
            Check(bad,"invalid temperature");
        }
        double area=Math.PI*IsothermalGasPhysics.BoreRadiusMetres*IsothermalGasPhysics.BoreRadiusMetres;
        for(int ml=200;ml<=1000;ml+=50)for(int k=250;k<=400;k+=25) {
            var state=IsothermalGasPhysics.At(ml,k);
            Check(Near(state.PV,.02*IsothermalGasPhysics.GasConstant*k),"pV grid");
            Check(Near(area*state.PistonHeightMetres,state.VolumeM3),"piston-volume consistency");
            Check(state.PressurePa>0&&state.PressurePa<400000,"gauge limits");
            Check(state.PistonHeightMetres>0&&state.PistonHeightMetres<.30,"physical stroke limits");
        }
        Check(IsothermalGasPhysics.SameIsotherm(300,true,300),"same isotherm");
        Check(!IsothermalGasPhysics.SameIsotherm(350,true,300),"different T excluded");
        Check(!IsothermalGasPhysics.SameIsotherm(300,false,300),"FREE records excluded");
        Console.WriteLine("PASS: "+count+" gas model assertions.");
    }
}
