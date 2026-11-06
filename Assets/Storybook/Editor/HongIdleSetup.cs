using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class HongIdleSetup
    {
        static HongIdleSetup(){EditorApplication.delayCall+=Run;}
        static void Run()
        {
            if(!File.Exists(".utmp/setup-hong-idle") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/setup-hong-idle");
            try
            {
                var scene=EditorSceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save scene first; preserving unsaved work.");
                var player=UnityEngine.Object.FindFirstObjectByType<HongGildong>();var source=new SerializedObject(player);
                var body=(SpriteRenderer)source.FindProperty("body").objectReferenceValue;
                var idle=body.GetComponent<HongGildongIdleVisual>();if(idle==null)idle=body.gameObject.AddComponent<HongGildongIdleVisual>();
                var so=new SerializedObject(idle);so.FindProperty("player").objectReferenceValue=player;so.FindProperty("body").objectReferenceValue=body;
                so.FindProperty("idleFront").objectReferenceValue=source.FindProperty("idleFront").objectReferenceValue;
                so.FindProperty("idleBack").objectReferenceValue=source.FindProperty("idleBack").objectReferenceValue;so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Debug.Log("HONG_IDLE_SETUP_OK: original front/back sprites retained; visual child mesh only, 1.5 second loop");
            }
            catch(Exception ex){Debug.LogException(ex);}
        }
    }
}
