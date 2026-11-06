using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class ChargedWarningVerification
    {
        static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static CrabSoldierAI ai;
        static PlayerHealth player;
        static ChargedAttackWarning warning;
        static float next;
        static int checks;
        static object Get(object obj,string name)=>obj.GetType().GetField(name,Flags).GetValue(obj);
        static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Flags).SetValue(obj,value);
        static void Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
        static void Check(bool ok,string text){if(!ok)throw new Exception("CHARGED_WARNING_TEST_FAIL: "+text);checks++;Debug.Log("CHARGED_WARNING_CHECK_PASS: "+text);}
        static ChargedWarningVerification()
        {
            EditorApplication.playModeStateChanged+=State;
            EditorApplication.delayCall+=()=>{if(!File.Exists(".utmp/test-charged-warning") || EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(".utmp/test-charged-warning");SessionState.SetBool("ChargedWarningTest",true);EditorApplication.EnterPlaymode();};
        }
        static void State(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ChargedWarningTest",false))return;SessionState.SetBool("ChargedWarningTest",false);
            foreach(var other in UnityEngine.Object.FindObjectsByType<CrabSoldierAI>(FindObjectsSortMode.None))other.enabled=false;
            player=UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(0,0,1.2f);cc.enabled=true;
            ai=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab"),Vector3.zero,Quaternion.identity).GetComponent<CrabSoldierAI>();ai.enabled=false;Set(ai,"target",player);
            warning=ai.GetComponent<ChargedAttackWarning>();next=Time.time+.15f;EditorApplication.update+=Test;
        }
        static void Begin(){Physics.SyncTransforms();Call(ai,"BeginAttack",Vector3.forward);}
        static void At(float elapsed){Set(ai,"attackStarted",Time.time-elapsed);Call(ai,"TickAttack");}
        static void Test()
        {
            if(Time.time<next)return;EditorApplication.update-=Test;
            try
            {
                float windup=(float)Get(ai,"windupTime"),active=(float)Get(ai,"activeTime");
                Check(Mathf.Abs(windup-.6f)<.001f && Mathf.Abs((float)Get(ai,"hitLength")-1.8f)<.001f && Mathf.Abs((float)Get(ai,"hitWidth")-1.1f)<.001f,"Existing attack timings and rectangle dimensions unchanged");
                Begin();Check(warning.Progress==0 && warning.IsVisible,"Attack begins at zero with original outline visible");
                var fill=(MeshRenderer)Get(warning,"fillRenderer");
                Check(fill.sharedMaterial!=null && !ShaderUtil.ShaderHasError(fill.sharedMaterial.shader),"Transparent fill shader compiles without errors");
                foreach(float fraction in new[]{.25f,.5f,.75f})
                {
                    At(windup*fraction);Check(Mathf.Abs(warning.Progress-fraction)<.001f,"Windup progress matches "+fraction);
                    var mesh=(Mesh)Get(warning,"mesh");Check(Mathf.Abs(mesh.bounds.size.z-1.8f*fraction)<.001f,"Filled length matches actual range times progress");
                    if(fraction==.5f)Capture();
                }
                Quaternion rotation=fill.transform.rotation;Vector3 position=fill.transform.position;
                var pc=player.GetComponent<CharacterController>();pc.enabled=false;player.transform.position=Vector3.right*1.2f;pc.enabled=true;Physics.SyncTransforms();At(windup*.9f);
                Check(fill.transform.position==position && Quaternion.Angle(fill.transform.rotation,rotation)<.001f,"Fill origin and direction stay locked when player moves");
                pc.enabled=false;player.transform.position=Vector3.forward*1.2f;pc.enabled=true;Physics.SyncTransforms();
                Begin();float hp=player.CurrentHP;At(windup-.0001f);
                Check(warning.Progress<1 && player.CurrentHP==hp,"Fill cannot finish or damage before windup ends");
                At(windup);Check(warning.Progress==1 && player.CurrentHP==hp-15,"Full charge and existing damage start in same tick");
                At(windup+.01f);Check(player.CurrentHP==hp-15,"Charge visual does not add duplicate damage");
                At(windup+active+.01f);Check(!warning.IsVisible && !fill.enabled,"Warning removed after existing hit window");
                Begin();At(windup*.5f);Call(ai,"CancelAttack");Check(!warning.IsVisible && warning.Progress==0,"Explicit attack cancellation clears fill and outline");
                Begin();Check(warning.Progress==0,"New attack always restarts from zero");
                Set(ai,"windupTime",1.2f);At(.3f);Check(Mathf.Abs(warning.Progress-.25f)<.001f,"Inspector windup change automatically rescales charge");Set(ai,"windupTime",windup);
                Set(ai,"state",CrabSoldierAI.State.Idle);Call(ai,"Update");Check(!warning.IsVisible,"Abnormal exit from attack clears remaining warning");
                Begin();var hong=player.GetComponent<HongGildong>();var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();
                Call(hong,"BeginDash",(Vector2)camera.WorldToScreenPoint(player.transform.position+Vector3.right));hp=player.CurrentHP;At(windup);
                Check(hong.IsInvulnerable && player.CurrentHP==hp,"Existing dash invulnerability remains effective at full charge");Set(hong,"dashTimeLeft",0f);Set(hong,"invulnerableUntil",0d);
                Begin();ai.enabled=true;ai.GetComponent<MonsterHealth>().ReceiveDamage(100);Check(!warning.IsVisible,"Monster death immediately cancels charge");
                var other=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab"),Vector3.zero,Quaternion.identity).GetComponent<CrabSoldierAI>();Set(other,"target",player);Call(other,"BeginAttack",Vector3.forward);
                player.TryTakeDamage(1000);Call(other,"Update");Check(!other.GetComponent<ChargedAttackWarning>().IsVisible,"Player death cancels remaining charge");
                Debug.Log("CHARGED_WARNING_TEST_PASSED: "+checks);
            }
            catch(Exception ex){Debug.LogException(ex);}
            EditorApplication.ExitPlaymode();
        }
        static void Capture()
        {
            var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();var rt=new RenderTexture(1280,720,24);var old=camera.targetTexture;var previous=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/Unity 포트폴리오/charged-warning.png",image.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
// Execute charge timing verification.
