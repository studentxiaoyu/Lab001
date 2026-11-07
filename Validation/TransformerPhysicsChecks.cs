using System;
public static class TransformerPhysicsChecks {
 static int checks;
 static void Check(bool ok,string msg){checks++;if(!ok)throw new Exception(msg);}
 static bool Near(double a,double b)=>Math.Abs(a-b)<1e-8;
 public static void Main(){
  foreach(double v in new[]{-1,0,3,6,12,24,100,double.NaN,double.PositiveInfinity})
  foreach(double a in new[]{0,100,200,400,1000,10000,double.NaN})
  foreach(double b in new[]{-10,100,200,400,1000,double.NegativeInfinity})
  foreach(bool on in new[]{false,true}){
   var s=TransformerPhysics.At(v,a,b,on);
   Check(s.N1>=100&&s.N1<=1000&&s.N2>=100&&s.N2<=1000,"turn boundaries");
   Check(!double.IsNaN(s.OutputVoltage)&&!double.IsInfinity(s.OutputVoltage),"finite output");
   Check(s.OutputVoltage>=0&&s.OutputVoltage<=240,"output boundary");
   Check(on||s.InputVoltage==0&&s.OutputVoltage==0,"OFF is zero");
   Check(Near(s.OutputVoltage*s.N1,s.InputVoltage*s.N2),"ideal law");
   Check(s.InputVoltage>0?s.VoltageRatio.HasValue&&Near(s.VoltageRatio.Value,s.TurnsRatio):!s.VoltageRatio.HasValue,"ratio defined only with nonzero actual input");
  }
  Check(Near(TransformerPhysics.At(6,200,400,true).OutputVoltage,12),"step-up");
  Check(Near(TransformerPhysics.At(6,200,100,true).OutputVoltage,3),"step-down");
  Check(Near(TransformerPhysics.At(12,200,200,true).OutputVoltage,12),"equal turns");
  Check(TransformerPhysics.ClampTurns(200.5)==201,"integer rounding");
  Console.WriteLine("Transformer physics: "+checks+" checks passed.");
 }
}