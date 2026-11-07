from pathlib import Path
import subprocess,sys,xml.etree.ElementTree as E
sys.stdout.reconfigure(encoding='utf-8')
data=Path(r'C:\Program Files\Unity\Hub\Editor\2021.3.45f2\Editor\Data')
r=E.parse('Assembly-CSharp.csproj').getroot()
old='S:\\Unity Editor\\2021.3.45f2\\Editor\\Data'
refs=[x.text.replace(old,str(data)) for x in r.findall('.//HintPath')]
refs += [str(Path('Library/ScriptAssemblies')/(Path(x.attrib['Include']).stem+'.dll')) for x in r.findall('.//ProjectReference')]
assert all(Path(p).exists() for p in refs)
base=['-nologo','-langversion:9.0','-nostdlib+','-preferreduilang:en-US','-utf8output','-define:'+r.find('.//DefineConstants').text]+['-r:"'+str(Path(p).resolve())+'"' for p in refs]
def compile(name,sources,kind):
    rsp=Path('Validation')/(name+'.rsp');output=Path('Validation')/(name+('.exe' if kind=='exe' else '.dll'))
    rsp.write_text('\n'.join(base+['-target:'+kind,'-out:"'+str(output.resolve())+'"']+['"'+str(Path(p).resolve())+'"' for p in sources]),encoding='utf-8')
    result=subprocess.run([str(data/'NetCoreRuntime/dotnet.exe'),str(data/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp.resolve())],capture_output=True)
    print(result.stdout.decode('utf-8-sig',errors='replace'));print(name+'_COMPILE_EXIT='+str(result.returncode))
    if result.returncode:sys.exit(result.returncode)
sources=[x.attrib['Include'] for x in r.findall('.//Compile')]
sources += ['Assets/Framework/Scripts/Utils/InputModeUtil.cs','Assets/Scripts/AccelerationDynamics.cs','Assets/Scripts/AccelerationExperiment.cs','Assets/Scripts/IsothermalGasPhysics.cs','Assets/Scripts/IsothermalGasExperiment.cs','Assets/Scripts/TransformerPhysics.cs','Assets/Scripts/TransformerExperiment.cs']
sources=list(dict.fromkeys(str(Path(p)) for p in sources))
compile('TransformerStageCompile',sources,'library')
compile('TransformerPhysicsChecks',['Assets/Scripts/TransformerPhysics.cs','Validation/TransformerPhysicsChecks.cs'],'exe')
result=subprocess.run([str(data/'MonoBleedingEdge/bin/mono.exe'),str(Path('Validation/TransformerPhysicsChecks.exe').resolve())],capture_output=True)
print(result.stdout.decode('utf-8-sig',errors='replace'));print(result.stderr.decode('utf-8-sig',errors='replace'));print('TRANSFORMER_CHECK_EXIT='+str(result.returncode))
sys.exit(result.returncode)
