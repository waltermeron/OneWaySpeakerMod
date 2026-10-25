using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Foundations;
using World;
using World.Devices;
using World.Devices.DeviceViews;
using World.Devices.DeviceUiViews;
using AutoCodes;
using Addrs;

namespace OneWaySpeaker {
    public static class Hooks {
        public static void DiagnosticBoot(){Diagnostics.Boot();}
        public static void FitPreview(Component view){var fit=view.GetComponent<PreviewFraming>();if(fit==null)fit=view.gameObject.AddComponent<PreviewFraming>();fit.Refresh(WorldContext.instance.caravan_camera);}
        public static bool Active;
        public static bool Resting;
        public static bool IsPlaying {get{return player!=null && player.IsPlaying;}}
        public static void AudioUpdated(Commons.AudioSystem audio){if(player!=null)player.ApplyBackgroundGain(audio);}
        static Player player;
        static Sprite sprite;
        static Sprite iconSprite;
        static Sprite controlSprite,controlOffSprite;
        static string Root {get{return Path.GetFullPath(Path.Combine(Application.dataPath,"..","SpeakerMod"));}}
        public static bool IsBell(Device d) {return d!=null && IsBell(d.desc);}
        static bool IsBell(device_all d) {return d!=null && (d.id==1311402 || d.id/10==1311402 || (!String.IsNullOrEmpty(d.prefeb) && d.prefeb.IndexOf("1311402",StringComparison.OrdinalIgnoreCase)>=0));}
        public static Sprite Sprite {
            get {
                if(sprite==null) {
                    var tex=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    if(!ImageConversion.LoadImage(tex,File.ReadAllBytes(Path.Combine(Root,"assets","speaker.png")),false))throw new IOException("Invalid speaker.png");
                    tex.filterMode=FilterMode.Point;tex.wrapMode=TextureWrapMode.Clamp;
                    sprite=UnityEngine.Sprite.Create(tex,new Rect(0,0,tex.width,tex.height),new Vector2(.5f,0),64);
                    sprite.name="OneWaySpeaker";UnityEngine.Object.DontDestroyOnLoad(tex);UnityEngine.Object.DontDestroyOnLoad(sprite);
                }
                return sprite;
            }
        }
        public static Sprite IconSprite {
            get {
                if(iconSprite!=null)return iconSprite;
                var source=Sprite.texture;const int canvas=96;
                var texture=new Texture2D(canvas,canvas,TextureFormat.RGBA32,false);
                texture.SetPixels32(new Color32[canvas*canvas]);
                texture.SetPixels32((canvas-source.width)/2,(canvas-source.height)/2,source.width,source.height,source.GetPixels32());
                texture.Apply();texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
                iconSprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,canvas,canvas),new Vector2(.5f,.5f),64);
                iconSprite.name="OneWaySpeakerIcon";UnityEngine.Object.DontDestroyOnLoad(texture);UnityEngine.Object.DontDestroyOnLoad(iconSprite);return iconSprite;
            }
        }
        public static Sprite ControlSprite {get{LoadControlSprites();return controlSprite;}}
        public static Sprite ControlOffSprite {get{LoadControlSprites();return controlOffSprite;}}
        static void LoadControlSprites(){
            if(controlSprite!=null)return;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!ImageConversion.LoadImage(texture,File.ReadAllBytes(Path.Combine(Root,"assets","speaker-control.png")),false))throw new IOException("Invalid speaker-control.png");
            texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
            controlSprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),64);controlSprite.name="OneWaySpeakerControlOn";
            var dim=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);var pixels=texture.GetPixels32();
            for(int i=0;i<pixels.Length;i++){var c=pixels[i];pixels[i]=new Color32((byte)(c.r*.48f),(byte)(c.g*.48f),(byte)(c.b*.48f),c.a);}
            dim.SetPixels32(pixels);dim.Apply();dim.filterMode=FilterMode.Point;dim.wrapMode=TextureWrapMode.Clamp;
            controlOffSprite=UnityEngine.Sprite.Create(dim,new Rect(0,0,dim.width,dim.height),new Vector2(.5f,.5f),64);controlOffSprite.name="OneWaySpeakerControlOff";
            UnityEngine.Object.DontDestroyOnLoad(texture);UnityEngine.Object.DontDestroyOnLoad(dim);UnityEngine.Object.DontDestroyOnLoad(controlSprite);UnityEngine.Object.DontDestroyOnLoad(controlOffSprite);
        }
        public static void AttachUi(DeviceUiView view){try{if(IsBell(view.owner))ReplaceBattlePanel(view.gameObject);}catch(Exception e){Debug.LogWarning("[OneWaySpeaker] Control panel: "+e.Message);}}
        public static int ReplaceBattlePanel(GameObject root){
            int count=0;
            foreach(var image in root.GetComponentsInChildren<Image>(true)){
                if(image.sprite==null)continue;
                string name=image.sprite.name;
                if(name!="ui_battle_ctrl_bell_on" && name!="ui_battle_ctrl_bell_on-2")continue;
                image.sprite=name=="ui_battle_ctrl_bell_on"?ControlSprite:ControlOffSprite;image.preserveAspect=true;count++;
            }
            if(count>0)Debug.Log("[OneWaySpeaker] Replaced "+count+" bell control glyphs; native background and buttons retained");
            return count;
        }
        static Player Boot() {
            if(player==null) {var go=new GameObject("OneWaySpeaker");UnityEngine.Object.DontDestroyOnLoad(go);player=go.AddComponent<Player>();}
            return player;
        }
        public static void Begin(){try{Active=true;Resting=false;Boot().ResetRun();Debug.Log("[OneWaySpeaker] Run ready v1.3.0");}catch(Exception e){Debug.LogException(e);}}
        public static void End(){Active=false;Resting=false;if(player!=null)player.ResetRun();}
        public static void EnterRest(){Resting=true;if(player!=null)player.EnterRest();Debug.Log("[OneWaySpeaker] Rest area reached; music stopped, native BGM fading in");}
        public static void LeaveRest(){Resting=false;}
        public static string Localize(string value){
            if(String.IsNullOrEmpty(value))return value;
            value=value.Replace("摄魂铃","老菊の音箱");
            const string original="除了物理超度外还能发出音波攻击";
            const string added="似乎会发出一段令人怀念的音乐";
            if(value.Contains(original) && !value.Contains(added))value+="\n"+added;
            return value;
        }
        public static void Attack(){try {
            if(!Active || Resting)return;
            var ctx=WorldContext.instance;
            if(ctx==null || ctx.in_safe_area || ctx.is_in_settlement || ctx.is_freeze_time)return;
            DeviceMgr mgr;if(!Mission.instance.try_get_mgr<DeviceMgr>("DeviceMgr",out mgr) || mgr==null)return;
            foreach(var slot in mgr.slots_device)if(IsBell(slot.slot_device) && slot.slot_device.faction==WorldEnum.Faction.player && slot.slot_device.is_validate){Boot().StartMusic();return;}
        }catch(Exception e){Debug.LogWarning("[OneWaySpeaker] Attack: "+e.Message);}}
        public static void PanelAttack(Component trigger){var ui=trigger.GetComponentInParent<DeviceUiView>();if(ui!=null && IsBell(ui.owner))Attack();}
        public static void Attach(DeviceView view){try{if(IsBell(view.owner) && view.GetComponent<SpeakerVisual>()==null)view.gameObject.AddComponent<SpeakerVisual>();}catch(Exception e){Debug.LogException(e);}}
        public static bool LoadSprite(string key,out Sprite result){
            Diagnostics.Boot();
            try {
                device_all d;
                foreach(var entry in device_alls.records){d=entry.Value;if(IsBell(d) && !String.IsNullOrEmpty(key) && String.Equals(key,d.icon_photo,StringComparison.OrdinalIgnoreCase)){result=IconSprite;return true;}}
                if(!String.IsNullOrEmpty(key) && key.IndexOf("1311402",StringComparison.OrdinalIgnoreCase)>=0){result=IconSprite;return true;}
            }catch(Exception e){Debug.LogWarning("[OneWaySpeaker] Icon: "+e.Message);}
            return Addressable_Utility.try_load_asset<Sprite>(key,out result);
        }
        public static string MusicPath {get{return Path.Combine(Root,"assets","365里路.mp3");}}
    }
    public sealed class PreviewFraming : MonoBehaviour {
        Camera camera;float originalSize,originalFov;Vector3 originalLocalPosition;
        void LateUpdate(){Refresh(WorldContext.instance.caravan_camera);}
        public void Refresh(Camera target){
            if(target==null || target.targetTexture==null)return;
            if(camera!=target){Restore();camera=target;originalSize=camera.orthographicSize;originalFov=camera.fieldOfView;originalLocalPosition=camera.transform.localPosition;Debug.Log("[OneWaySpeaker] Preview camera "+camera.name+" orthographic="+camera.orthographic);}
            camera.orthographicSize=originalSize;camera.fieldOfView=originalFov;camera.transform.localPosition=originalLocalPosition;
            Bounds bounds=new Bounds();bool found=false;
            foreach(var visual in FindObjectsOfType<SpeakerVisual>()){var child=visual.transform.Find("TallSpeakerSprite");if(child==null)continue;var renderer=child.GetComponent<SpriteRenderer>();if(renderer==null || (camera.cullingMask & (1<<child.gameObject.layer))==0)continue;if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);}
            if(!found)return;
            foreach(var car in FindObjectsOfType<World.Caravans.CaravanView>())if(car.anim_renderer!=null)bounds.Encapsulate(car.anim_renderer.bounds);
            Frame(camera,bounds,originalSize);
        }
        public static void Frame(Camera camera,Bounds bounds,float minimumSize){
            Vector3 center=bounds.center;Vector3 displacement=center-camera.transform.position;
            camera.transform.position+=camera.transform.up*Vector3.Dot(displacement,camera.transform.up)+camera.transform.right*Vector3.Dot(displacement,camera.transform.right);
            float neededHeight=Mathf.Max(bounds.extents.y,bounds.extents.x/Mathf.Max(.01f,camera.aspect))*1.08f;
            if(camera.orthographic)camera.orthographicSize=Mathf.Max(minimumSize,neededHeight);
            else{float depth=Mathf.Abs(Vector3.Dot(displacement,camera.transform.forward));camera.fieldOfView=Mathf.Max(camera.fieldOfView,2*Mathf.Atan(neededHeight/Mathf.Max(.01f,depth))*Mathf.Rad2Deg);}
        }
        void Restore(){if(camera!=null){camera.orthographicSize=originalSize;camera.fieldOfView=originalFov;camera.transform.localPosition=originalLocalPosition;}camera=null;}
        void OnDisable(){Restore();}
        void OnDestroy(){Restore();}
    }
    public sealed class Diagnostics : MonoBehaviour {
        static bool booted;
        public static void Boot(){if(booted)return;booted=true;if(Array.IndexOf(Environment.GetCommandLineArgs(),"--speaker-self-test")<0)return;var go=new GameObject("SpeakerSelfTest");DontDestroyOnLoad(go);go.AddComponent<Diagnostics>();}
        static void Check(bool value,string label){if(!value)throw new Exception("Self-test failed: "+label);Debug.Log("[OneWaySpeaker TEST] PASS "+label);}
        static void NativeFrame(Player player,AudioSource source,float nativeVolume){source.volume=nativeVolume;player.ApplyBackgroundSource(source,true);}
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(8);
            var routine=Run();bool failed=false;
            while(true){object current=null;bool more=false;try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception e){Debug.LogException(e);failed=true;break;}if(!more)break;yield return current;}
            Debug.Log("[OneWaySpeaker TEST] "+(failed?"FAILED":"ALL PASSED"));Application.Quit(failed?1:0);
        }
        IEnumerator Run(){
            int matched=0;string prefabKey=null,panelKey=null;
            foreach(var entry in device_alls.records){var d=entry.Value;if(d.id/10!=1311402)continue;prefabKey=d.prefeb;panelKey=d.ui_prefab;Sprite s;Check(Hooks.LoadSprite(d.icon_photo,out s) && s==Hooks.IconSprite,"icon "+d.id+" "+d.icon_photo);Check(Commons.Localization_Utility.get_localization(d.name).Contains("老菊の音箱"),"renamed "+d.id);var description=Commons.Localization_Utility.get_localization(d.alt_info);Check(description.Contains("似乎会发出一段令人怀念的音乐"),"description "+d.id);matched++;}
            Check(Hooks.Localize("其他武器")=="其他武器","unrelated names unchanged");
            string lore="冠海常见的古代法器\n除了物理超度外还能发出音波攻击";Check(Hooks.Localize(lore)==lore+"\n似乎会发出一段令人怀念的音乐","description appended on new line");
            Check(matched>0,"bell database variants "+matched);
            Check(Hooks.Sprite.texture.width==31 && Hooks.Sprite.texture.height==64 && Hooks.Sprite.texture.filterMode==FilterMode.Point,"pixel texture");
            var icon=Hooks.IconSprite.texture;int left=icon.width,bottom=icon.height,right=-1,topPixel=-1;var iconPixels=icon.GetPixels32();
            for(int y=0;y<icon.height;y++)for(int x=0;x<icon.width;x++)if(iconPixels[y*icon.width+x].a>0){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);topPixel=Math.Max(topPixel,y);}
            Check(icon.width==96 && icon.height==96 && right-left+1==31 && topPixel-bottom+1==64 && left>0 && bottom>0,"inventory icon preserves tall proportions with centered transparent padding");
            GameObject prefab;Check(Addressable_Utility.try_load_asset<GameObject>(prefabKey,out prefab) && prefab!=null,"original bell prefab");
            GameObject panel;Check(Addressable_Utility.try_load_asset<GameObject>(panelKey,out panel) && panel!=null,"bell control panel prefab");
            foreach(var image in panel.GetComponentsInChildren<Image>(true)){string path=image.name;for(var p=image.transform.parent;p!=null && p!=panel.transform;p=p.parent)path=p.name+"/"+path;Debug.Log("[OneWaySpeaker PANEL] "+path+" sprite="+(image.sprite!=null?image.sprite.name:"null")+" rect="+image.rectTransform.rect+" type="+image.type);}
            foreach(var slider in panel.GetComponentsInChildren<Slider>(true))Debug.Log("[OneWaySpeaker SLIDER] "+slider.name+" fill="+(slider.fillRect!=null?slider.fillRect.name:"null")+" handle="+(slider.handleRect!=null?slider.handleRect.name:"null"));
            var inactiveRoot=new GameObject("TestControlPanelRoot");inactiveRoot.SetActive(false);var panelClone=Instantiate(panel,inactiveRoot.transform,false);
            var bg=panelClone.transform.Find("Contents_on_Board/Bg").GetComponent<Image>();var button=panelClone.transform.Find("Contents_on_Board/AtkBtn").GetComponent<Image>();var originalBg=bg.sprite;var originalButton=button.sprite;
            Check(Hooks.ReplaceBattlePanel(panelClone)==2,"battle panel replaces both bell glyphs");
            var on=panelClone.transform.Find("Contents_on_Board/Bg/Icon_On").GetComponent<Image>();var off=panelClone.transform.Find("Contents_on_Board/Bg/Icon_Off").GetComponent<Image>();
            Check(on.sprite==Hooks.ControlSprite && off.sprite==Hooks.ControlOffSprite && on.preserveAspect && off.preserveAspect,"battle glyph preserves aspect and separate light/dark states");
            Check(bg.sprite==originalBg && button.sprite==originalButton && Mathf.Abs(on.rectTransform.rect.width-136)<.01f && Mathf.Abs(on.rectTransform.rect.height-204)<.01f,"battle background, attack button and native layout unchanged");
            var colors=new HashSet<int>();foreach(var c in Hooks.ControlSprite.texture.GetPixels32())if(c.a>0)colors.Add(c.r<<16|c.g<<8|c.b);
            Check(Hooks.ControlSprite.texture.height==48 && colors.Count==3 && Hooks.ControlSprite.texture.filterMode==FilterMode.Point,"battle icon uses coarse 48px grid and three amber colors");Destroy(inactiveRoot);
            var visual=new GameObject("TestSpeakerVisual");visual.layer=10;var original=visual.AddComponent<SpriteRenderer>();original.sortingOrder=7;original.sprite=Hooks.Sprite;float oldBottom=original.bounds.min.y;float oldHeight=original.bounds.size.y;visual.AddComponent<SpeakerVisual>();
            yield return null;yield return new WaitForEndOfFrame();
            var replacement=visual.transform.Find("TallSpeakerSprite").GetComponent<SpriteRenderer>();
            Check(original.forceRenderingOff && replacement!=null,"world renderer replacement");
            Check(replacement.gameObject.layer==10,"preview camera layer preserved");
            var orderMethod=typeof(World.BattleUtility).GetMethod("slot_2_order_in_layer",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            foreach(var slot in new[]{"device_slot_wheel","slot_top","slot_back","slot_back_top","slot_front","slot_front_top"})Check(replacement.sortingOrder<(int)orderMethod.Invoke(null,new object[]{slot}) && replacement.transform.Find("PixelSoundWave0").GetComponent<SpriteRenderer>().sortingOrder<(int)orderMethod.Invoke(null,new object[]{slot}),"speaker and waves behind weapon slot "+slot);
            var previewCamera=new GameObject("TestPreviewCamera").AddComponent<Camera>();previewCamera.enabled=false;previewCamera.orthographic=true;previewCamera.aspect=1;previewCamera.transform.position=new Vector3(0,0,-10);PreviewFraming.Frame(previewCamera,new Bounds(new Vector3(0,1,0),new Vector3(2,4,0)),1);
            var top=previewCamera.WorldToViewportPoint(new Vector3(0,3,0));Check(top.y<1 && top.y>.9f,"preview camera fits tall speaker without top clipping");
            previewCamera.orthographic=false;previewCamera.fieldOfView=20;PreviewFraming.Frame(previewCamera,new Bounds(new Vector3(0,1,0),new Vector3(2,4,0)),5);top=previewCamera.WorldToViewportPoint(new Vector3(0,3,0));Check(top.y<1 && top.y>.9f,"perspective preview keeps car large while fitting speakers");Destroy(previewCamera.gameObject);
            Check(Mathf.Abs(replacement.bounds.min.y-(oldBottom-oldHeight*1.18f*.12f))<.001f,"speaker lowered 12 percent");
            var clone=Instantiate(visual);yield return null;yield return new WaitForEndOfFrame();
            Check(clone.GetComponentsInChildren<SpriteRenderer>(true).Length==6 && Mathf.Abs(clone.transform.Find("TallSpeakerSprite").GetComponent<SpriteRenderer>().bounds.size.y-replacement.bounds.size.y)<.001f,"preview clone has one speaker at original size");Destroy(clone);
            var go=new GameObject("TestSpeakerPlayer");var player=go.AddComponent<Player>();player.enabled=false;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var source=(AudioSource)typeof(Player).GetField("source",flags).GetValue(player);source.volume=0;source.mute=true;
            var playerField=typeof(Hooks).GetField("player",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var previousPlayer=playerField.GetValue(null);playerField.SetValue(null,player);
            Hooks.Active=true;Hooks.Resting=false;player.StartMusic();
            float timeout=Time.realtimeSinceStartup+30;
            while(source.clip==null && Time.realtimeSinceStartup<timeout)yield return null;
            Check(source.clip!=null && source.clip.length>30 && source.isPlaying && source.loop,"MP3 decode and loop playback");
            yield return new WaitForSecondsRealtime(.35f);int before=source.timeSamples;player.StartMusic();
            Check(source.timeSamples>=before && before>0,"repeat attack keeps music position");
            Check(AudioFade.DisplayMetres(10)==20 && AudioFade.DisplayMetres(5)==10,"fade distance matches HUD metres (world units x2)");
            player.ApplyDistanceFade(40,.8f);Check(Mathf.Abs(source.volume-.8f)<.001f,"song stays at configured volume before final 20m");
            player.ApplyDistanceFade(25,0);Check(source.volume==0,"song respects muted BGM settings");player.ApplyDistanceFade(20,.8f);
            Check(Mathf.Abs(source.volume-.8f)<.001f,"20m begins fade at full volume");
            player.ApplyDistanceFade(10,.8f);Check(Mathf.Abs(source.volume-.4f)<.001f,"10m song volume is half");
            player.ApplyDistanceFade(5,.8f);Check(source.volume>0 && source.volume<.4f,"5m song continues smooth fade");
            player.ApplyDistanceFade(0,.8f);Check(source.volume==0,"song is silent at 0m");player.StartMusic();player.ApplyDistanceFade(30,.8f);
            Check(source.volume==0,"repeat attack or reversing cannot restart finished fade");
            yield return null;yield return new WaitForEndOfFrame();
            var wave=replacement.transform.Find("PixelSoundWave0").GetComponent<SpriteRenderer>();var wavePosition=wave.transform.localPosition;
            Check(wave.enabled,"music enables pixel sound waves");
            yield return new WaitForSecondsRealtime(.2f);
            Check(wave.transform.localPosition!=wavePosition,"sound waves animate during playback");
            var background=new GameObject("TestBackground").AddComponent<AudioSource>();background.mute=false;background.volume=.8f;
            var alreadyMuted=new GameObject("TestMutedBackground").AddComponent<AudioSource>();alreadyMuted.mute=true;alreadyMuted.volume=.6f;
            player.ApplyBackgroundSource(background,true);player.ApplyBackgroundSource(alreadyMuted,true);
            Check(background.volume==0 && !background.mute && alreadyMuted.mute,"native BGM suppressed while preserving mute flags");
            Hooks.EnterRest();Check(!source.isPlaying && background.volume==0,"rest arrival stops song and begins native BGM from silence");
            player.AdvanceBackgroundRecovery(.75f);NativeFrame(player,background,.8f);
            Check(Mathf.Abs(background.volume-.4f)<.001f,"native BGM restores smoothly over 1.5 seconds");
            NativeFrame(player,background,.4f);Check(Mathf.Abs(background.volume-.2f)<.001f,"recovery respects native rest volume adjustment");
            NativeFrame(player,background,.4f);Check(Mathf.Abs(background.volume-.2f)<.001f,"native audio Update does not compound recovery gain");
            NativeFrame(player,alreadyMuted,.3f);Check(alreadyMuted.mute,"recovery preserves pre-existing mute state");
            Hooks.EnterRest();player.AdvanceBackgroundRecovery(.75f);
            Check(Mathf.Abs(background.volume-.4f)<.001f && !background.mute && alreadyMuted.mute,"recovery reaches current native level without restart or overshoot");
            player.StartMusic();Check(!source.isPlaying,"rest screen cannot restart music");yield return null;
            Check(!wave.enabled,"sound waves stop at rest screen");
            Hooks.LeaveRest();player.StartMusic();Check(source.isPlaying && source.timeSamples<source.clip.frequency,"next departure first attack starts new song");
            Hooks.End();Check(!source.isPlaying,"run end stops music");
            player.EnterRest();NativeFrame(player,background,.4f);Check(Mathf.Abs(background.volume-.4f)<.001f,"rest without a speaker song leaves native BGM untouched");
            playerField.SetValue(null,previousPlayer);Destroy(visual);Destroy(background.gameObject);Destroy(alreadyMuted.gameObject);Destroy(go);
            yield return null;
        }
    }
    public static class AudioFade {
        public const float FadeDistanceMetres=20f,RecoverySeconds=1.5f;
        public static float DisplayMetres(float remainingWorldDistance){return Mathf.Max(0,remainingWorldDistance*2);}
        public static float DistanceGain(float metres){if(float.IsNaN(metres))return 1;return Mathf.SmoothStep(0,1,Mathf.Clamp01(metres/FadeDistanceMetres));}
        public static float RecoveryGain(float seconds){return Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds/RecoverySeconds));}
    }
    public sealed class Player : MonoBehaviour {
        AudioSource source;
        AudioClip clip;
        Coroutine loading;
        bool requested,paused;
        bool backgroundOwned,recovering;
        float songGain=1,backgroundGain=1,recoveryElapsed;
        readonly Dictionary<AudioSource,bool> muted=new Dictionary<AudioSource,bool>();
        readonly Dictionary<AudioSource,float> nativeVolumes=new Dictionary<AudioSource,float>();
        static readonly System.Reflection.FieldInfo bgmVolume=typeof(Commons.AudioSystem).GetField("bgm_volume",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
        void Awake(){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=true;source.spatialBlend=0;source.volume=.8f;}
        public bool IsPlaying {get{return source!=null && source.isPlaying && requested && !paused;}}
        public void StartMusic(){if(requested || Hooks.Resting)return;requested=true;songGain=1;if(clip!=null)Play();else loading=StartCoroutine(Load());}
        IEnumerator Load(){
            var path=Hooks.MusicPath;
            if(!File.Exists(path)){Debug.LogError("[OneWaySpeaker] Music missing: "+path);requested=false;yield break;}
            using(var req=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.MPEG)){
                yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success){Debug.LogError("[OneWaySpeaker] Music load failed: "+req.error);requested=false;}
                else{clip=DownloadHandlerAudioClip.GetContent(req);if(requested && Hooks.Active)Play();}
            }
            loading=null;
        }
        float RemainingMetres(){var ctx=WorldContext.instance;return ctx!=null && ctx.r_scene!=null?AudioFade.DisplayMetres(ctx.scene_remain_progress):float.PositiveInfinity;}
        float MusicVolume(){var audio=Commons.AudioSystem.instance;return audio!=null && bgmVolume!=null?Mathf.Clamp01((float)bgmVolume.GetValue(audio)):.8f;}
        void Play(){recovering=false;backgroundOwned=true;backgroundGain=0;source.clip=clip;ApplyDistanceFade(RemainingMetres(),MusicVolume());ApplyBackgroundGain(Commons.AudioSystem.instance,false);source.Play();Debug.Log("[OneWaySpeaker] Playing 365里路; fade over final 20m");}
        void StopSong(){requested=false;paused=false;if(loading!=null){StopCoroutine(loading);loading=null;}if(source!=null){source.Stop();source.volume=0;}}
        public void ResetRun(){StopSong();recovering=false;recoveryElapsed=0;songGain=1;backgroundGain=1;Restore();}
        public void EnterRest(){
            StopSong();
            if(recovering || !backgroundOwned)return;
            recovering=true;recoveryElapsed=0;backgroundGain=0;
        }
        void Restore(){foreach(var kv in muted)if(kv.Key!=null){kv.Key.mute=kv.Value;float native;if(nativeVolumes.TryGetValue(kv.Key,out native))kv.Key.volume=native;}muted.Clear();nativeVolumes.Clear();backgroundOwned=false;}
        public void ApplyDistanceFade(float metres,float configuredVolume){songGain=Mathf.Min(songGain,AudioFade.DistanceGain(metres));source.volume=Mathf.Clamp01(configuredVolume)*songGain;}
        public void AdvanceBackgroundRecovery(float deltaTime){
            if(!recovering)return;
            recoveryElapsed+=Mathf.Max(0,deltaTime);backgroundGain=AudioFade.RecoveryGain(recoveryElapsed);
            if(recoveryElapsed>=AudioFade.RecoverySeconds){backgroundGain=1;recovering=false;Restore();}
        }
        public void ApplyBackgroundGain(Commons.AudioSystem audio){ApplyBackgroundGain(audio,true);}
        void ApplyBackgroundGain(Commons.AudioSystem audio,bool nativeFrame){if(audio==null || !backgroundOwned)return;ApplyBackgroundSource(audio.sourceScene,nativeFrame);ApplyBackgroundSource(audio.sourceBattle,nativeFrame);}
        public void ApplyBackgroundSource(AudioSource channel,bool nativeFrame){
            if(channel==null || !backgroundOwned)return;
            if(!muted.ContainsKey(channel)){muted.Add(channel,channel.mute);nativeVolumes[channel]=channel.volume;}
            if(nativeFrame)nativeVolumes[channel]=channel.volume;
            channel.volume=nativeVolumes[channel]*backgroundGain;channel.mute=muted[channel];
        }
        void Update(){
            AdvanceBackgroundRecovery(Time.unscaledDeltaTime);
            if(!Hooks.Active || !requested)return;
            var ctx=WorldContext.instance;if(ctx==null || ctx.is_in_settlement){ResetRun();return;}if(ctx.in_safe_area){EnterRest();return;}
            if(clip==null)return;
            if(ctx.is_freeze_time){if(!paused){source.Pause();paused=true;}}
            else if(paused){source.UnPause();paused=false;}
            ApplyDistanceFade(RemainingMetres(),MusicVolume());
        }
        void OnDestroy(){Restore();if(clip!=null)Destroy(clip);}
    }
    public sealed class SpeakerVisual : MonoBehaviour {
        public const float LoweringRatio=.12f;
        public const int BehindWeaponsOrder=90;
        Renderer[] originals;
        Renderer referenceRenderer;
        SpriteRenderer speaker;
        SpriteRenderer[] waves;
        Vector3 baseScale;
        float phase;
        static Sprite waveSprite;
        bool ready;
        static Sprite WaveSprite {
            get {
                if(waveSprite!=null)return waveSprite;
                var texture=new Texture2D(12,24,TextureFormat.RGBA32,false);var pixels=new Color32[12*24];
                for(int y=2;y<22;y++){int x=3+Mathf.RoundToInt(5*Mathf.Sin((y-2)*Mathf.PI/19));pixels[y*12+x]=new Color32(160,211,182,255);pixels[y*12+x+1]=new Color32(160,211,182,255);}
                texture.SetPixels32(pixels);texture.Apply();texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
                waveSprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,12,24),new Vector2(.5f,.5f),64);DontDestroyOnLoad(texture);DontDestroyOnLoad(waveSprite);return waveSprite;
            }
        }
        void CreateWaves(){
            waves=new SpriteRenderer[4];
            for(int i=0;i<waves.Length;i++){
                var existing=speaker.transform.Find("PixelSoundWave"+i);
                var obj=existing!=null?existing.gameObject:new GameObject("PixelSoundWave"+i);obj.transform.SetParent(speaker.transform,false);obj.layer=speaker.gameObject.layer;
                var wave=obj.GetComponent<SpriteRenderer>();if(wave==null)wave=obj.AddComponent<SpriteRenderer>();wave.sprite=WaveSprite;wave.sortingLayerID=speaker.sortingLayerID;wave.sortingOrder=speaker.sortingOrder+1;wave.enabled=false;waves[i]=wave;
            }
        }
        void LateUpdate(){
            if(!ready){
                var existing=transform.Find("TallSpeakerSprite");
                var originalList=new List<Renderer>();
                foreach(var r in GetComponentsInChildren<Renderer>(true))if(existing==null || (r.transform!=existing && !r.transform.IsChildOf(existing)))originalList.Add(r);
                originals=originalList.ToArray();
                Bounds bounds=new Bounds(transform.position,Vector3.zero);bool found=false;Renderer reference=null;
                foreach(var r in originals)if(r is MeshRenderer || r is SpriteRenderer){if(!found){bounds=r.bounds;reference=r;found=true;}else bounds.Encapsulate(r.bounds);}
                if(!found || bounds.size.y<.001f)return;
                referenceRenderer=reference;
                var go=existing!=null?existing.gameObject:new GameObject("TallSpeakerSprite");go.transform.SetParent(transform,false);go.layer=reference.gameObject.layer;speaker=go.GetComponent<SpriteRenderer>();if(speaker==null)speaker=go.AddComponent<SpriteRenderer>();speaker.sprite=Hooks.Sprite;speaker.forceRenderingOff=false;
                speaker.renderingLayerMask=reference.renderingLayerMask;
                speaker.sortingLayerID=reference.sortingLayerID;speaker.sortingOrder=BehindWeaponsOrder;
                float targetHeight=bounds.size.y*1.18f;
                go.transform.position=new Vector3(bounds.center.x,bounds.min.y-targetHeight*LoweringRatio,bounds.center.z);
                float scale=targetHeight/speaker.sprite.bounds.size.y;
                var lossy=transform.lossyScale;
                go.transform.localScale=new Vector3(scale/Mathf.Max(.001f,Mathf.Abs(lossy.x)),scale/Mathf.Max(.001f,Mathf.Abs(lossy.y)),1);
                baseScale=go.transform.localScale;CreateWaves();
                ready=true;Debug.Log("[OneWaySpeaker] Replaced bell world visual, height="+targetHeight);
            }
            foreach(var r in originals)if(r!=null)r.forceRenderingOff=true;
            if(referenceRenderer!=null)speaker.gameObject.layer=referenceRenderer.gameObject.layer;
            bool playing=Hooks.IsPlaying;
            if(playing)phase+=Time.unscaledDeltaTime;
            float pulse=playing?Mathf.Round(Mathf.Sin(phase*12)*2)/2:0;
            speaker.transform.localScale=new Vector3(baseScale.x*(1+pulse*.012f),baseScale.y*(1-pulse*.008f),baseScale.z);
            for(int i=0;i<waves.Length;i++){
                var wave=waves[i];wave.enabled=playing;
                if(!playing)continue;
                float step=Mathf.Floor(Mathf.Repeat(phase*1.8f+(i%2)*.5f,1)*4)/4;
                float side=i<2?-1:1;
                wave.transform.localPosition=new Vector3(side*(.30f+step*.14f),.42f,0);
                wave.transform.localScale=new Vector3(side,1+step*.5f,1);
                wave.color=new Color(.75f,1,.85f,(1-step)*.65f);
                wave.gameObject.layer=speaker.gameObject.layer;
            }
        }
        void OnDestroy(){if(originals!=null)foreach(var r in originals)if(r!=null)r.forceRenderingOff=false;}
    }
}





