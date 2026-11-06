using System.IO;
using UnityEngine;
using UnityEditor;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabGameCapture
    {
        static CrabGameCapture(){EditorApplication.delayCall+=Capture;}
        static void Capture()
        {
            if(!File.Exists(".utmp/capture-crab-request") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/capture-crab-request");
            var scene=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            var crab=UnityEngine.Object.FindFirstObjectByType<MonsterHealth>();
            if (crab!=null && !scene.isDirty) { crab.transform.position=new Vector3(1.8f,0,-1.0f); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); }
            var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();
            var rt=new RenderTexture(1280,720,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes("C:/Users/Admin/Documents/ChatGPT/Unity 포트폴리오/crab-game-view.png",image.EncodeToPNG());
            camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
            Debug.Log("CRAB_GAME_CAPTURE_OK");
        }
    }
}
// Recapture after source-resolution PPU correction.
// Quality diagnostic: capture restored source resolution without changing camera.
