using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
class CheckStandalone {
    static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types){foreach(var type in types){yield return type;foreach(var child in All(type.NestedTypes))yield return child;}}
    static MethodDefinition Required(AssemblyDefinition assembly,string type,string name){
        var method=All(assembly.MainModule.Types).Single(t=>t.FullName==type).Methods.Single(m=>m.Name==name);
        if(!method.HasBody || method.HasGenericParameters || method.DeclaringType.HasGenericParameters)throw new Exception("Unsupported required method: "+method.FullName);
        return method;
    }
    static int SpriteCalls(AssemblyDefinition assembly){return All(assembly.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Count(i=>{
        var call=i.Operand as GenericInstanceMethod;
        return call!=null && call.DeclaringType.FullName=="Addrs.Addressable_Utility" && call.Name=="try_load_asset" && call.GenericArguments.Count==1 && call.GenericArguments[0].FullName=="UnityEngine.Sprite";
    });}
    static void Api(AssemblyDefinition assembly){
        var gameNames=new HashSet<string>(new[]{"World","Commons","AutoCodes","Foundations","Camp"});
        foreach(var type in All(assembly.MainModule.Types))foreach(var method in type.Methods.Where(m=>m.HasBody))foreach(var instruction in method.Body.Instructions){
            var member=instruction.Operand as MemberReference;if(member==null || member.DeclaringType==null)continue;
            var scope=member.DeclaringType.Scope as AssemblyNameReference;if(scope==null || !gameNames.Contains(scope.Name))continue;
            var called=member as MethodReference;var field=member as FieldReference;
            if(called!=null && called.Resolve()==null || field!=null && field.Resolve()==null)throw new Exception("Required speaker API changed: "+member.FullName);
        }
    }
    static int Main(string[] args){
        try{
            if(args.Length!=2)throw new Exception("Usage: CheckStandalone.exe <ManagedDirectory> <OneWaySpeaker.Plugin.dll>");
            string managed=Path.GetFullPath(args[0]),plugin=Path.GetFullPath(args[1]);
            using(var resolver=new DefaultAssemblyResolver()){
                resolver.AddSearchDirectory(managed);resolver.AddSearchDirectory(Path.GetDirectoryName(plugin));resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(AssemblyDefinition).Assembly.Location));
                var read=new ReaderParameters{AssemblyResolver=resolver,ReadingMode=ReadingMode.Immediate,InMemory=true};
                using(var world=AssemblyDefinition.ReadAssembly(Path.Combine(managed,"World.dll"),read))
                using(var common=AssemblyDefinition.ReadAssembly(Path.Combine(managed,"Commons.dll"),read))
                using(var camp=AssemblyDefinition.ReadAssembly(Path.Combine(managed,"Camp.dll"),read))
                using(var mod=AssemblyDefinition.ReadAssembly(plugin,read)){
                    if(mod.Name.Name!="OneWaySpeaker.Plugin")throw new Exception("Incorrect standalone speaker assembly identity");
                    foreach(var a in new[]{world,common,camp}){
                        if(a.MainModule.AssemblyReferences.Any(r=>r.Name=="OneWaySpeaker" || r.Name=="OneWaySpeaker.Plugin"))throw new Exception("Legacy or duplicate speaker hooks found in "+a.Name.Name+". Migrate that layer first.");
                        int sprites=SpriteCalls(a);if(sprites==0)throw new Exception("Sprite caller API changed in "+a.Name.Name);
                        Console.WriteLine("PASS native sprite callers "+a.Name.Name+": "+sprites);
                    }
                    Api(mod);
                    foreach(var name in new[]{"on_init","on_fini","back_campScene","back_initScene","QuitGame","SetSettlement","ExitSafeArea"})Required(world,"World.WorldSceneRoot",name);
                    Required(world,"World.SafeArea.SafeAreaView","Init");Required(world,"World.WorldRawImage","LateUpdate");Required(world,"World.Devices.Equip.EquipmentMgrView","OnEnable");Required(world,"World.Devices.DeviceUiViews.DeviceUiView","attach");
                    foreach(var type in new[]{"World.Devices.DeviceViews.DeviceView","World.Devices.DeviceViews.DeviceView_Spine"})Required(world,type,"attach");
                    var panel=All(world.MainModule.Types).Where(t=>t.FullName.StartsWith("World.Devices.DeviceUiViews.DevicePanelAttachment_Trigger",StringComparison.Ordinal)).SelectMany(t=>t.Methods).Count(m=>m.Name=="OnPointerDown" && m.HasBody);
                    if(panel==0)throw new Exception("Native attack panel methods changed");
                    var localize=Required(common,"Commons.Localization_Utility","get_localization");if(localize.ReturnType.FullName!="System.String")throw new Exception("Localization return type changed");
                    var audio=Required(common,"Commons.AudioSystem","Update");if(!audio.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="set_volume"))throw new Exception("Native audio volume update changed");
                    var attack=Required(world,"World.Input.WorldInputController","OnLeftClickPerformedHandler");
                    if(attack.Body.Instructions.Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).FullName=="System.Void System.Action::Invoke()")!=1)throw new Exception("Native guarded attack callback changed");
                    Console.WriteLine("PASS speaker v1.4.0 standalone APIs, lifecycle, guarded attack, UI, audio and localization. No game files modified.");
                }
            }
            return 0;
        }catch(Exception e){Console.Error.WriteLine("Speaker compatibility rejected: "+e);return 1;}
    }
}
