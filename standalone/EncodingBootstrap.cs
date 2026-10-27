using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;

[assembly:System.Reflection.AssemblyVersion("1.5.1.0")]
[assembly:System.Reflection.AssemblyFileVersion("1.5.1.0")]

namespace OneWaySpeaker.Bootstrap {
    // Unity's Mono can assign an invalid CodeBase to byte-loaded assemblies when
    // the preloader's working directory contains non-ASCII characters. This shim
    // changes no assemblies; the original directory is restored before game code.
    public static class EncodingBootstrap {
        static bool initialized,ownsDirectory;
        static string originalDirectory;
        static readonly ManualLogSource log=Logger.CreateLogSource("OneWaySpeaker Bootstrap");
        public static IEnumerable<string> TargetDLLs {get{Initialize();return new string[0];}}
        static bool DpsOwnsEncoding(){
            // Check before touching cwd, independent of preloader order. The
            // current DPS preloader owns the same workaround. If it is present,
            // only it must capture/restore cwd; nested owners restore incorrectly.
            if(File.Exists(Path.Combine(Paths.PatcherPluginPath,"OneWayDps.Preloader.dll")))return true;
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())try{
                if(assembly.GetType("OneWayDps.Loading.Patcher",false)!=null)return true;
            }catch{ /* Never call Assembly.GetName/CodeBase during this bootstrap. */ }
            return false;
        }
        public static void Initialize(){
            if(initialized)return;initialized=true;
            if(DpsOwnsEncoding()){log.LogInfo("Existing DPS preloader owns the Unicode-path workaround; speaker bootstrap leaves cwd unchanged.");return;}
            string safe=Environment.GetFolderPath(Environment.SpecialFolder.System);
            if(String.IsNullOrEmpty(safe) || safe.Any(c=>c>127) || !Directory.Exists(safe))throw new InvalidOperationException("No ASCII system directory is available for the Mono preloader workaround");
            originalDirectory=Environment.CurrentDirectory;
            Environment.CurrentDirectory=safe;ownsDirectory=true;
            log.LogInfo("Speaker-only Mono Unicode-path workaround enabled during preload; no game assemblies are patched.");
        }
        // BepInEx recognizes this as a preloader plugin. Empty TargetDLLs means
        // Patch is never invoked and no game-assembly replacement is published.
        public static void Patch(AssemblyDefinition assembly){}
        public static void Finish(){
            if(!ownsDirectory)return;
            Environment.CurrentDirectory=originalDirectory;ownsDirectory=false;originalDirectory=null;
            log.LogInfo("Speaker bootstrap restored the original working directory before game startup.");
        }
    }
}
