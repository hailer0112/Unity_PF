using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabSpriteInstaller
    {
        const string Folder="Assets/Storybook/Enemies/CrabSoldier";
        static CrabSpriteInstaller(){EditorApplication.delayCall+=Requested;}
        static void Requested()
        {
            if(!File.Exists(".utmp/install-crab-sprite-request") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/install-crab-sprite-request");
            try{Install();}catch(Exception ex){Debug.LogException(ex);}
        }
        static void Install()
        {
            string path=Folder+"/CorruptedCrabSoldier.prefab";
            string backup=Folder+"/LegacyPrimitiveCrab.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(backup)==null && !AssetDatabase.CopyAsset(path,backup))throw new Exception("Cannot back up crab prefab");
            var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/CrabSoldierIdle.png");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.maxTextureSize=128;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=9;settings.spritePivot=new Vector2(.5f,.04f);importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/CrabSoldierIdle.png");
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight); importer.spritePixelsPerUnit=sourceHeight*.90f/.85f;importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"/CrabSoldierIdle.png");
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var old=root.transform.Find("Visual");if(old!=null)old.gameObject.SetActive(false);
                var visual=root.transform.Find("SpriteVisual");
                if(visual==null){visual=new GameObject("SpriteVisual").transform;visual.SetParent(root.transform,false);}
                var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();
                visual.localRotation=camera!=null?camera.transform.rotation:Quaternion.Euler(36,0,0);
                var renderer=visual.GetComponent<SpriteRenderer>();if(renderer==null)renderer=visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite=sprite;renderer.sortingOrder=2;renderer.color=Color.white;
                var hit=new SerializedObject(root.GetComponent<MonsterHitVisual>());hit.FindProperty("visualRoot").objectReferenceValue=visual;
                var array=hit.FindProperty("flashRenderers");array.arraySize=1;array.GetArrayElementAtIndex(0).objectReferenceValue=renderer;hit.ApplyModifiedPropertiesWithoutUndo();
                // Keep the hitbox around the torso, excluding oversized claws and sprite effects.
                var collider=root.GetComponent<CapsuleCollider>();collider.radius=.18f;collider.height=.65f;collider.center=new Vector3(0,.35f,0);
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
                Debug.Log("CRAB_SPRITE_INSTALLED: backup preserved; static camera-facing sprite; HP/attack unchanged");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var scene=EditorSceneManager.GetActiveScene();
            if(!scene.isDirty)Debug.Log("CRAB_SCENE_PREFAB_LINK_READY");
        }
    }
}
