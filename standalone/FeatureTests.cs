using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace OneWaySpeaker.Standalone {
    static class FeatureTests {
        static void Check(bool condition,string name){if(!condition)throw new Exception("v1.5 self-test failed: "+name);Debug.Log("[OneWaySpeaker TEST] PASS "+name);}
        static bool Near(float a,float b){return Mathf.Abs(a-b)<.001f;}
        static Event Key(EventType type,KeyCode key){return new Event{type=type,keyCode=key};}
        public static IEnumerator Run(){
            int[] counts=new int[3];for(int roll=0;roll<100;roll++)counts[SongCatalog.Select(roll)]++;
            Check(counts[0]==75 && counts[1]==20 && counts[2]==5,"exclusive song weights exactly 75/20/5 percent");
            Check(SongCatalog.Select(0)==0 && SongCatalog.Select(74)==0 && SongCatalog.Select(75)==1 && SongCatalog.Select(94)==1 && SongCatalog.Select(95)==2 && SongCatalog.Select(99)==2,"all song draw boundaries");
            Check(Near(PlaybackSpeed.MetresPerSecond(20),40) && Near(PlaybackSpeed.MetresPerSecond(-30),60) && PlaybackSpeed.MetresPerSecond(float.NaN)==0,"speed uses horizontal HUD metres/second, including reverse and invalid data");
            Check(Near(PlaybackSpeed.Target(40,true),1) && Near(PlaybackSpeed.Target(40.01f,true),1.2f) && Near(PlaybackSpeed.Target(59.99f,true),1.2f) && Near(PlaybackSpeed.Target(60,true),1.4f) && Near(PlaybackSpeed.Target(80,true),1.6f) && Near(PlaybackSpeed.Target(100,true),1.8f),"speed thresholds 40/60/80/100 m/s");
            Check(Near(PlaybackSpeed.Target(220,true),3) && Near(PlaybackSpeed.Target(400,true),3) && Near(PlaybackSpeed.Target(100,false),1),"engine pitch ceiling and disabled speed control");
            var ramp=new PitchRamp();Check(Near(ramp.Advance(1.2f,0),1) && Near(ramp.Advance(1.2f,.3f),1.1f) && Near(ramp.Advance(1.2f,.3f),1.2f),"pitch changes smoothly and arrives after 0.6 seconds");
            Check(Near(ramp.Advance(1.8f,.3f),1.5f) && Near(ramp.Advance(1,.3f),1.25f) && Near(ramp.Advance(1,.3f),1),"pitch can retarget smoothly while accelerating or disabling");
            var plugin=UnityEngine.Object.FindObjectOfType<Plugin>();Check(plugin!=null,"independent speaker plugin available for hotkey integration");
            UnityEngine.Object.Destroy(plugin);yield return null;yield return null;
            Plugin.EnsureRuntimeHost();plugin=UnityEngine.Object.FindObjectOfType<Plugin>();
            Check(plugin!=null,"runtime host recovers after its Unity component is destroyed");
            var localize=typeof(Commons.Localization_Utility).GetMethod("get_localization",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            Check(HarmonyLib.Harmony.GetPatchInfo(localize).Owners.Contains(Plugin.Owner),"scene host destruction retains installed localization patches");
            Check(Commons.Localization_Utility.get_localization("device_name_1311402").Contains("老菊の音箱"),"actual localization stays modified after the runtime host is replaced");
            bool originalSpeed=PlaybackSpeed.Enabled;
            Check(!plugin.HandleSpeedKey(Key(EventType.KeyDown,KeyCode.F1)) && PlaybackSpeed.Enabled==originalSpeed,"other mod shortcuts do not toggle playback");
            Check(plugin.HandleSpeedKey(Key(EventType.KeyDown,KeyCode.BackQuote)) && PlaybackSpeed.Enabled!=originalSpeed,"backquote key toggles vehicle speed playback");
            Check(System.IO.File.ReadAllText(plugin.Config.ConfigFilePath).IndexOf("SpeedByVehicle = "+PlaybackSpeed.Enabled,StringComparison.OrdinalIgnoreCase)>=0,"speed toggle is saved in the speaker's own config");
            Check(!plugin.HandleSpeedKey(Key(EventType.KeyDown,KeyCode.BackQuote)) && PlaybackSpeed.Enabled!=originalSpeed,"held key repeat does not toggle repeatedly");
            plugin.HandleSpeedKey(Key(EventType.KeyUp,KeyCode.BackQuote));plugin.HandleSpeedKey(Key(EventType.KeyDown,KeyCode.BackQuote));plugin.HandleSpeedKey(Key(EventType.KeyUp,KeyCode.BackQuote));
            Check(PlaybackSpeed.Enabled==originalSpeed,"second key press restores saved setting");
            var go=new GameObject("SpeakerV15AudioTest");var player=go.AddComponent<Player>();player.enabled=false;
            var source=(AudioSource)typeof(Player).GetField("source",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player);source.mute=true;
            var global=typeof(Hooks).GetField("player",BindingFlags.Static|BindingFlags.NonPublic);var previous=global.GetValue(null);global.SetValue(null,player);
            Hooks.Active=true;Hooks.Resting=false;
            foreach(int roll in new[]{0,75,95}){
                player.ResetRun();player.StartMusicForTest(roll);int index=SongCatalog.Select(roll);float timeout=Time.realtimeSinceStartup+30;
                while(!source.isPlaying && Time.realtimeSinceStartup<timeout)yield return null;
                Check(player.SelectedIndex==index && source.clip!=null && source.clip.length>1 && source.isPlaying && source.loop,"decode and play selected track "+SongCatalog.FileName(index));
                yield return null;yield return null;var currentClip=source.clip;var sample=source.timeSamples;
                player.StartMusicForTest(index==2?0:99);
                Check(player.SelectedIndex==index && source.clip==currentClip && source.timeSamples>=sample,"repeat attack keeps selected track "+index);
                PlaybackSpeed.Enabled=true;player.ApplyVehiclePitch(100,.3f);Check(Near(source.pitch,1.4f),"actual audio source smoothly reaches midpoint");
                player.ApplyVehiclePitch(100,.3f);Check(Near(source.pitch,1.8f) && source.clip==currentClip,"actual audio reaches speed target without changing track");
                PlaybackSpeed.Enabled=false;player.ApplyVehiclePitch(100,.3f);Check(Near(source.pitch,1.4f),"toggle off gradually reduces audio speed");
                player.ApplyVehiclePitch(100,.3f);Check(Near(source.pitch,1) && source.isPlaying && source.clip==currentClip,"toggle off restores 1x without restarting");
                player.ApplyDistanceFade(10,.8f);Check(Near(source.volume,.4f),"every song retains final 20m fade");
                player.ApplyDistanceFade(0,.8f);Check(source.volume==0,"every song reaches silence at endpoint");
                player.EnterRest();Check(!source.isPlaying && Near(source.pitch,1),"rest stops selected song and clears pitch");
            }
            player.ResetRun();var gameRandom=UnityEngine.Random.state;player.StartMusic();
            Check(player.SelectedIndex>=0 && player.SelectedIndex<3 && source.isPlaying && UnityEngine.Random.state.Equals(gameRandom),"public random playback uses its own RNG without changing game randomness");
            player.ResetRun();Hooks.Resting=true;player.StartMusic();Check(player.SelectedIndex==-1 && !source.isPlaying,"rest cannot draw or start a new random song");
            PlaybackSpeed.Enabled=originalSpeed;player.ResetRun();global.SetValue(null,previous);Hooks.Active=false;Hooks.Resting=false;UnityEngine.Object.Destroy(go);
            yield return null;
        }
    }
}
