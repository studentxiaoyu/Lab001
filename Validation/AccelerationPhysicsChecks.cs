using System;
using System.Collections.Generic;
public static class PhysicsChecks {
    static int count;
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
    static bool Near(double a, double b, double tolerance=1e-8) => Math.Abs(a-b)<tolerance;
    public static void Main() {
        var p=new AccelerationDynamics.Settings(.5,.05,0,0);
        var s=AccelerationDynamics.Solve(p);
        Check(Near(s.Acceleration,.05*9.81/.55),"finite hanging inertia");
        Check(Near(s.Tension,.05*(9.81-s.Acceleration)),"bucket equation");
        Check(Near(s.CartNetForce,.5*s.Acceleration),"cart equation");
        Check(s.Tension < .05*9.81,"weight is not tension");
        Check(AccelerationDynamics.Solve(new AccelerationDynamics.Settings(1,.05,0,0)).Acceleration<s.Acceleration,"increased cart mass");
        Check(AccelerationDynamics.Solve(new AccelerationDynamics.Settings(.5,.1,0,0)).Acceleration>s.Acceleration,"increased hanging mass");
        Check(AccelerationDynamics.Solve(new AccelerationDynamics.Settings(.5,.05,.05,0)).Acceleration<s.Acceleration,"friction response");
        Check(AccelerationDynamics.Solve(new AccelerationDynamics.Settings(.5,.05,0,5)).Acceleration>s.Acceleration,"incline response");
        Check(AccelerationDynamics.Solve(new AccelerationDynamics.Settings(.5,.01,.3,0)).Acceleration==0,"static friction holds");
        Check(AccelerationDynamics.Solve(new AccelerationDynamics.Settings(.5,0,0,0)).Acceleration==0,"no drive");
        double theta=Math.Atan(.1)*180/Math.PI;
        var balanced=AccelerationDynamics.Solve(new AccelerationDynamics.Settings(.5,.05,.1,theta));
        Check(Near(balanced.CompensationError,0),"friction balance");
        Check(Near(balanced.Acceleration,s.Acceleration),"compensated total inertia");
        var c=new AccelerationDynamics.Settings(0,-1,-1,90);
        Check(c.CartMass==.2&&c.HangingMass==0&&c.Friction==0&&c.AngleDegrees==12,"parameter boundaries");
        bool rejected=false;try { new AccelerationDynamics.Settings(double.NaN,0,0,0); } catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"nonfinite input");
        double duration=AccelerationDynamics.Duration(s,.8);
        Check(Near(.5*s.Acceleration*duration*duration,.8),"endpoint time");
        var tape=new List<AccelerationDynamics.Sample>();
        for(int i=0;i*AccelerationDynamics.SampleInterval<=duration;i++)tape.Add(new AccelerationDynamics.Sample(i*AccelerationDynamics.SampleInterval,s.Acceleration));
        Check(tape.Count>3,"enough tape points");
        for(int i=1;i<tape.Count;i++)Check(Near(tape[i].Time-tape[i-1].Time,.02),"50 Hz cadence");
        Check(Near(AccelerationDynamics.MeasuredAcceleration(tape),s.Acceleration),"second difference acceleration");
        Check(tape[tape.Count-1].Position<=.8,"no postimpact samples");
        Check(AccelerationDynamics.SameForceSeries(p,new AccelerationDynamics.Settings(.5,.1,0,0)),"force series changes hanging mass");
        Check(!AccelerationDynamics.SameForceSeries(p,new AccelerationDynamics.Settings(1,.1,0,0)),"reject mixed cart mass");
        Check(AccelerationDynamics.SameMassSeries(p,new AccelerationDynamics.Settings(1,.05,0,0)),"mass series changes cart mass");
        Check(!AccelerationDynamics.SameMassSeries(p,new AccelerationDynamics.Settings(1,.06,0,0)),"reject mixed hanging mass");
        Check(!AccelerationDynamics.SameMassSeries(p,new AccelerationDynamics.Settings(1,.05,.1,0)),"reject mixed friction");
        Check(!AccelerationDynamics.SameMassSeries(p,new AccelerationDynamics.Settings(1,.05,0,1)),"reject mixed angle");
        Console.WriteLine("PASS: "+count+" assertions (includes every 50 Hz sample interval).");
    }
}
