using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using World.Devices.DeviceViews;
using World.Devices.DeviceUiViews;

[assembly:AssemblyVersion("1.5.1.0")]
[assembly:AssemblyFileVersion("1.5.1.0")]

namespace OneWaySpeaker.Standalone {
    [BepInPlugin(Owner,"Old Ju Speaker","1.5.1")]
    [BepInDependency("suika.oneway.dps",BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin {
        public const string Owner="suika.oneway.speaker.standalone";
        Harmony harmony;
        static Harmony runtimeHarmony;
        static ConfigEntry<bool> runtimeSpeedSetting;
        static ManualLogSource runtimeLog;
        static Plugin currentHost;
        static bool runtimeInstalled,quitting;
        bool installed,selfTestRequested;
        ConfigEntry<bool> speedEnabled;
        bool backquoteHeld;
        float speedNoticeUntil;
        GUIStyle speedNoticeStyle;
        Font speedNoticeFont;
        void Awake(){
            var arguments=Environment.GetCommandLineArgs();
            selfTestRequested=Array.IndexOf(arguments,"--speaker-self-test")>=0;
            if(selfTestRequested){Application.runInBackground=true;Diagnostics.DeferBoot=true;}
            Logger.LogInfo("Speaker self-test flag="+selfTestRequested+"; managed argument count="+arguments.Length);
            if(runtimeInstalled){
                harmony=runtimeHarmony;speedEnabled=runtimeSpeedSetting;installed=true;currentHost=this;
                Logger.LogInfo("Speaker runtime host restored; existing patches and playback settings retained.");return;
            }
            string originalDirectory=Environment.CurrentDirectory;
            try{
                EnterAsciiDirectory();
                speedEnabled=Config.Bind<bool>("Playback","SpeedByVehicle",true,"Smoothly change speaker song speed with vehicle m/s. Toggle with the backquote/tilde key.");
                PlaybackSpeed.Enabled=speedEnabled.Value;
                RejectLegacyMode();
                var plan=Prepare();
                harmony=new Harmony(Owner);
                foreach(var patch in plan.Values)harmony.Patch(patch.Target,patch.Prefix==null?null:new HarmonyMethod(patch.Prefix),patch.Postfix==null?null:new HarmonyMethod(patch.Postfix),patch.Transpiler==null?null:new HarmonyMethod(patch.Transpiler));
                installed=true;
                runtimeHarmony=harmony;runtimeSpeedSetting=speedEnabled;runtimeLog=Logger;runtimeInstalled=true;currentHost=this;
                SceneManager.sceneLoaded+=OnSceneLoaded;
                Application.quitting+=Shutdown;
                Logger.LogInfo("Speaker v1.5.1 standalone enabled; "+plan.Count+" Harmony targets. Game DLLs and other mod owners are unchanged.");
            }catch(Exception e){
                if(harmony!=null)try{harmony.UnpatchSelf();}catch(Exception rollback){Logger.LogError("Speaker rollback failed: "+rollback);}
                Logger.LogError("Speaker disabled: compatibility check or patch failed. "+e);
            }finally{Environment.CurrentDirectory=originalDirectory;}
        }
        static void OnSceneLoaded(Scene scene,LoadSceneMode mode){EnsureRuntimeHost();}
        internal static void EnsureRuntimeHost(){
            if(!runtimeInstalled || quitting || currentHost!=null)return;
            try{
                var host=new GameObject("OneWaySpeakerRuntime");DontDestroyOnLoad(host);host.AddComponent<Plugin>();
                if(runtimeLog!=null)runtimeLog.LogInfo("Speaker persistent runtime host created after scene initialization.");
            }catch(Exception e){if(runtimeLog!=null)runtimeLog.LogError("Speaker runtime host recovery failed: "+e);}
        }
        void Start(){
            if(!installed || !selfTestRequested)return;
            Diagnostics.DeferBoot=false;
            Diagnostics.ExtraTests=FeatureTests.Run;
            Logger.LogInfo("Speaker self-test scheduled from plugin Start; background="+Application.runInBackground);
            Hooks.DiagnosticBoot();
        }
        public bool HandleSpeedKey(Event input){
            if(!installed || input==null || input.keyCode!=KeyCode.BackQuote)return false;
            if(input.type==EventType.KeyUp){backquoteHeld=false;return false;}
            if(input.type!=EventType.KeyDown || backquoteHeld)return false;
            backquoteHeld=true;speedEnabled.Value=!speedEnabled.Value;PlaybackSpeed.Enabled=speedEnabled.Value;
            speedNoticeUntil=Time.realtimeSinceStartup+2;
            Logger.LogInfo("Speaker vehicle speed playback="+PlaybackSpeed.Enabled);
            input.Use();return true;
        }
        void OnApplicationFocus(bool focused){if(!focused)backquoteHeld=false;}
        void OnGUI(){
            if(!installed)return;
            HandleSpeedKey(Event.current);
            if(Time.realtimeSinceStartup>=speedNoticeUntil)return;
            if(speedNoticeStyle==null){
                speedNoticeFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);
                speedNoticeStyle=new GUIStyle(GUI.skin.box);speedNoticeStyle.font=speedNoticeFont;speedNoticeStyle.fontSize=20;speedNoticeStyle.alignment=TextAnchor.MiddleCenter;
            }
            GUI.Box(new Rect((Screen.width-340)/2f,16,340,38),"音箱随车速变速："+(PlaybackSpeed.Enabled?"开启":"关闭"),speedNoticeStyle);
        }
        static void EnterAsciiDirectory(){
            // Harmony also emits byte-loaded wrapper assemblies. They need the
            // same Mono CodeBase workaround as the earlier preloader interval.
            string safe=Environment.GetFolderPath(Environment.SpecialFolder.System);
            if(String.IsNullOrEmpty(safe) || safe.Any(c=>c>127) || !System.IO.Directory.Exists(safe))throw new InvalidOperationException("No ASCII system directory is available for Harmony code generation");
            Environment.CurrentDirectory=safe;
        }
        void OnDestroy(){
            if(speedNoticeFont!=null)Destroy(speedNoticeFont);
            if(ReferenceEquals(currentHost,this))currentHost=null;
            installed=false;
            // Unity can remove the pre-scene BepInEx object while the first Main
            // scene initializes. Component destruction is not a mod unload.
            if(runtimeInstalled && !quitting && runtimeLog!=null)runtimeLog.LogInfo("Transient speaker host removed; retaining runtime patches for scene recovery.");
        }
        static void Shutdown(){
            if(quitting)return;quitting=true;
            SceneManager.sceneLoaded-=OnSceneLoaded;Application.quitting-=Shutdown;
            Hooks.End();string originalDirectory=Environment.CurrentDirectory;
            try{EnterAsciiDirectory();if(runtimeHarmony!=null)runtimeHarmony.UnpatchSelf();}
            catch(Exception e){if(runtimeLog!=null)runtimeLog.LogError("Speaker cleanup failed: "+e);}
            finally{Environment.CurrentDirectory=originalDirectory;runtimeInstalled=false;}
        }
        static void RejectLegacyMode(){
            var own=typeof(Plugin).Assembly;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies()){
                if(assembly==own)continue;
                if(assembly.GetName().Name=="OneWaySpeaker" || assembly.GetType("OneWaySpeaker.Hooks",false)!=null)throw new InvalidOperationException("Another speaker runtime is loaded: "+assembly.FullName+". Remove the legacy static/memory speaker layer first.");
                if(new[]{"World","Commons","Camp","OneWayDps"}.Contains(assembly.GetName().Name) && assembly.GetReferencedAssemblies().Any(a=>a.Name=="OneWaySpeaker"))throw new InvalidOperationException("Legacy speaker hooks found in "+assembly.GetName().Name+"; refusing duplicate sound and visuals.");
            }
        }
        sealed class PatchPlan {public MethodBase Target;public MethodInfo Prefix,Postfix,Transpiler;}
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
        static MethodInfo Callback(string name){return typeof(Plugin).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic);}
        static Assembly Native(string name){return AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name==name);}
        static MethodInfo Required(Assembly assembly,string type,string name){
            var result=assembly.GetType(type,true).GetMethods(Flags).Single(m=>m.Name==name);
            if(result.ContainsGenericParameters || result.GetMethodBody()==null)throw new InvalidOperationException("Unsupported required method: "+result);
            return result;
        }
        static void Add(Dictionary<MethodBase,PatchPlan> plan,MethodBase method,string prefix,string postfix,string transpiler){
            PatchPlan p;if(!plan.TryGetValue(method,out p)){p=new PatchPlan{Target=method};plan.Add(method,p);}
            if(prefix!=null)p.Prefix=Callback(prefix);if(postfix!=null)p.Postfix=Callback(postfix);if(transpiler!=null)p.Transpiler=Callback(transpiler);
        }
        static Dictionary<MethodBase,PatchPlan> Prepare(){
            var plan=new Dictionary<MethodBase,PatchPlan>();
            var world=typeof(World.WorldSceneRoot).Assembly;var common=typeof(Commons.AudioSystem).Assembly;
            var camp=Assembly.Load("Camp");
            Add(plan,Required(world,"World.WorldSceneRoot","on_init"),"BeginPrefix",null,null);
            foreach(var name in new[]{"on_fini","back_campScene","back_initScene","QuitGame","SetSettlement"})Add(plan,Required(world,"World.WorldSceneRoot",name),"EndPrefix",null,null);
            Add(plan,Required(world,"World.SafeArea.SafeAreaView","Init"),"RestPrefix",null,null);
            Add(plan,Required(world,"World.WorldSceneRoot","ExitSafeArea"),"LeaveRestPrefix",null,null);
            Add(plan,Required(world,"World.WorldRawImage","LateUpdate"),null,"PreviewPostfix",null);
            Add(plan,Required(world,"World.Devices.Equip.EquipmentMgrView","OnEnable"),null,"PreviewPostfix",null);
            Add(plan,Required(world,"World.Devices.DeviceUiViews.DeviceUiView","attach"),null,"UiPostfix",null);
            Add(plan,Required(common,"Commons.AudioSystem","Update"),null,"AudioPostfix",null);
            var localize=Required(common,"Commons.Localization_Utility","get_localization");
            if(localize.ReturnType!=typeof(string))throw new InvalidOperationException("Localization return type changed");
            Add(plan,localize,null,"LocalizePostfix",null);
            var attack=Required(world,"World.Input.WorldInputController","OnLeftClickPerformedHandler");
            if(PatchProcessor.GetOriginalInstructions(attack).Count(i=>IsActionInvoke(i.operand))!=1)throw new InvalidOperationException("Native attack UI guard/callback structure changed");
            Add(plan,attack,null,null,"AttackTranspiler");
            int panels=0,views=0;
            foreach(var type in world.GetTypes()){
                if(type.FullName.StartsWith("World.Devices.DeviceUiViews.DevicePanelAttachment_Trigger",StringComparison.Ordinal))foreach(var method in type.GetMethods(Flags).Where(m=>m.Name=="OnPointerDown" && m.GetMethodBody()!=null)){Add(plan,method,"PanelPrefix",null,null);panels++;}
                if(type.FullName=="World.Devices.DeviceViews.DeviceView" || type.FullName=="World.Devices.DeviceViews.DeviceView_Spine")foreach(var method in type.GetMethods(Flags).Where(m=>m.Name=="attach" && m.GetMethodBody()!=null)){Add(plan,method,null,"ViewPostfix",null);views++;}
            }
            if(panels==0 || views<2)throw new InvalidOperationException("Native speaker UI/view attachment methods changed");
            foreach(var assembly in new[]{world,common,camp})if(AddSprites(plan,assembly)==0)throw new InvalidOperationException("Native sprite callers were not found in "+assembly.GetName().Name);
            // The DPS runtime is optional. Its loaded caller methods are patched after its
            // preloader completes, so neither preloader can discard the other's hooks.
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="OneWayDps"))AddSprites(plan,assembly);
            foreach(var target in plan.Keys){var info=Harmony.GetPatchInfo(target);if(info!=null && info.Owners.Contains(Owner))throw new InvalidOperationException("Speaker patches are already installed");}
            return plan;
        }
        static int AddSprites(Dictionary<MethodBase,PatchPlan> plan,Assembly assembly){
            int count=0;
            foreach(var type in assembly.GetTypes())foreach(var method in type.GetMethods(Flags).Cast<MethodBase>().Concat(type.GetConstructors(Flags))){
                if(method.ContainsGenericParameters || method.GetMethodBody()==null)continue;
                var calls=PatchProcessor.GetOriginalInstructions(method).Count(i=>IsSpriteLoad(i.operand));
                if(calls==0)continue;
                Add(plan,method,null,null,"SpriteTranspiler");count+=calls;
            }
            return count;
        }
        static bool IsSpriteLoad(object operand){
            var method=operand as MethodInfo;
            if(method==null || method.DeclaringType==null || method.DeclaringType.FullName!="Addrs.Addressable_Utility" || method.Name!="try_load_asset" || !method.IsGenericMethod)return false;
            var args=method.GetGenericArguments();var parameters=method.GetParameters();
            return args.Length==1 && args[0]==typeof(Sprite) && method.ReturnType==typeof(bool) && parameters.Length==2 && parameters[0].ParameterType==typeof(string) && parameters[1].ParameterType==typeof(Sprite).MakeByRefType();
        }
        static bool IsActionInvoke(object operand){var m=operand as MethodInfo;return m!=null && m.DeclaringType==typeof(Action) && m.Name=="Invoke";}
        static IEnumerable<CodeInstruction> SpriteTranspiler(IEnumerable<CodeInstruction> instructions){
            int count=0;var replacement=AccessTools.Method(typeof(Hooks),"LoadSprite");
            foreach(var instruction in instructions){
                // Mutating only the call keeps its original labels and exception metadata.
                if(IsSpriteLoad(instruction.operand)){instruction.opcode=OpCodes.Call;instruction.operand=replacement;count++;}
                yield return instruction;
            }
            if(count==0)throw new InvalidOperationException("Sprite caller changed before the speaker transpiler ran");
        }
        static IEnumerable<CodeInstruction> AttackTranspiler(IEnumerable<CodeInstruction> instructions){
            int count=0;
            foreach(var instruction in instructions){
                if(IsActionInvoke(instruction.operand)){
                    var added=new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(Hooks),"Attack"));
                    added.labels.AddRange(instruction.labels);instruction.labels.Clear();
                    // Begin boundaries follow the inserted call; closing boundaries stay
                    // on the original Invoke so its native exception scope is unchanged.
                    var starts=instruction.blocks.Where(b=>b.blockType!=ExceptionBlockType.EndExceptionBlock).ToArray();
                    added.blocks.AddRange(starts);foreach(var start in starts)instruction.blocks.Remove(start);
                    yield return added;count++;
                }
                yield return instruction;
            }
            if(count!=1)throw new InvalidOperationException("Native guarded attack callback changed");
        }
        static void Safe(Action action){try{action();}catch(Exception e){Debug.LogWarning("[OneWaySpeaker] Harmony hook: "+e);}}
        static void BeginPrefix(){EnsureRuntimeHost();Hooks.Begin();}
        static void EndPrefix(){Hooks.End();}
        static void RestPrefix(){Hooks.EnterRest();}
        static void LeaveRestPrefix(){Hooks.LeaveRest();}
        static void PreviewPostfix(Component __instance){Safe(()=>Hooks.FitPreview(__instance));}
        static void UiPostfix(DeviceUiView __instance){Hooks.AttachUi(__instance);}
        static void ViewPostfix(DeviceView __instance){Hooks.Attach(__instance);}
        static void PanelPrefix(Component __instance){Safe(()=>Hooks.PanelAttack(__instance));}
        static void AudioPostfix(Commons.AudioSystem __instance){Safe(()=>{EnsureRuntimeHost();Hooks.AudioUpdated(__instance);});}
        static void LocalizePostfix(ref string __result){__result=Hooks.Localize(__result);}
    }
}
