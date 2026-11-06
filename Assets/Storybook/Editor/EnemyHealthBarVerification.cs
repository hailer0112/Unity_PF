using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class EnemyHealthBarVerification
    {
        static MonsterHealth a,b;
        static EnemyHealthBar barA,barB;
        static float next;
        static int stage,checks;
        static EnemyHealthBarVerification()
        {
            EditorApplication.playModeStateChanged+=State;
            EditorApplication.delayCall+=()=>{
                if(!File.Exists(".utmp/test-enemy-bar") || EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(".utmp/test-enemy-bar");SessionState.SetBool("EnemyBarTest",true);EditorApplication.EnterPlaymode();
            };
        }
        static void Check(bool value,string message){if(!value)throw new Exception("ENEMY_BAR_TEST_FAIL: "+message);checks++;Debug.Log("ENEMY_BAR_CHECK_PASS: "+message);}
        static void State(PlayModeStateChange change)
        {
            if(change!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("EnemyBarTest",false))return;
            SessionState.SetBool("EnemyBarTest",false);
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Storybook/Enemies/CrabSoldier/CorruptedCrabSoldier.prefab");
                a=UnityEngine.Object.Instantiate(prefab,new Vector3(.3f,0,-1.0f),Quaternion.identity).GetComponent<MonsterHealth>();
                b=UnityEngine.Object.Instantiate(prefab,new Vector3(-1.5f,0,.8f),Quaternion.identity).GetComponent<MonsterHealth>();
                barA=a.GetComponentInChildren<EnemyHealthBar>();barB=b.GetComponentInChildren<EnemyHealthBar>();
                Check(!barA.IsVisible && !barB.IsVisible,"Both bars initially hidden");
                a.ReceiveDamage(25);
                Check(barA.IsVisible && Mathf.Abs(barA.DisplayedRatio-.75f)<.001f,"25 damage shows 75 percent immediately");
                Check(!barB.IsVisible && b.CurrentHP==100,"Second enemy remains unaffected");
                stage=0;next=Time.time+2f;EditorApplication.update+=Tick;
            }
            catch(Exception ex){Fail(ex);}
        }
        static void Fail(Exception ex){Debug.LogException(ex);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();}
        static void Tick()
        {
            if(Time.time<next)return;
            try
            {
                if(stage==0)
                {
                    Check(barA.IsVisible,"Still visible before three seconds");a.ReceiveDamage(25);
                    Check(barA.IsVisible && Mathf.Abs(barA.DisplayedRatio-.5f)<.001f,"Repeated damage updates fill to 50 percent");
                    stage=1;next=Time.time+1.2f;
                }
                else if(stage==1)
                {
                    Check(barA.IsVisible,"Second hit resets timeout beyond original deadline");
                    var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();
                    var rect=barA.GetComponent<RectTransform>();
                    Check(Quaternion.Angle(rect.rotation,camera.transform.rotation)<.01f,"HP bar faces camera without sprite shake rotation");
                    Check(Vector3.Dot(rect.position-a.transform.position,camera.transform.up)>.9f,"Bar stays above rendered sprite head");
                    b.ReceiveDamage(25);Check(barB.IsVisible && Mathf.Abs(barB.DisplayedRatio-.75f)<.001f,"Independent second bar shows own HP");
                    Capture(camera);
                    stage=2;next=Time.time+2f;
                }
                else
                {
                    Check(!barA.IsVisible,"First bar hides three seconds after last hit");
                    Check(barB.IsVisible,"Second bar has its own timeout");
                    a.ReceiveDamage(50);Check(a.IsDead && !barA.IsVisible,"Lethal damage immediately hides HP bar");
                    b.transform.position+=Vector3.right;
                    var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();
                    camera.transform.position+=Vector3.right;
                    stage=3;next=Time.time+.15f;
                    EditorApplication.update-=Tick;EditorApplication.update+=Finish;
                }
            }
            catch(Exception ex){Fail(ex);}
        }
        static void Finish()
        {
            if(Time.time<next)return;EditorApplication.update-=Finish;
            try
            {
                var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>().GetComponent<Camera>();
                var rect=barB.GetComponent<RectTransform>();
                Check(Mathf.Abs(Vector3.Dot(rect.position-b.transform.position,camera.transform.right))<.01f,"Bar remains centered after enemy and camera movement");
                b.ReceiveDamage(100);Check(!barB.IsVisible,"Visible bar hides immediately on death");
                Debug.Log("ENEMY_BAR_TEST_PASSED: "+checks);
            }
            catch(Exception ex){Debug.LogException(ex);}
            EditorApplication.ExitPlaymode();
        }
        static void Capture(Camera camera)
        {
            var rt=new RenderTexture(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/Unity 포트폴리오/enemy-hp-bars.png",image.EncodeToPNG());
            camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
// Execute HP bar Play mode verification.
