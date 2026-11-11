using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CombatReadabilitySetup
    {
        const string Folder="Assets/Storybook/Enemies/CrabSoldier";
        static CombatReadabilitySetup(){EditorApplication.delayCall+=Request;}
        static void Request()
        {
            if(!File.Exists(".utmp/readability-request") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/readability-request");try{Apply();}catch(Exception ex){Debug.LogException(ex);}
        }
        static void Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();if(scene.isDirty)throw new Exception("Save current scene before readability setup; unsaved work preserved.");
            string path=Folder+"/CrabSoldierStyleSheet.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.isReadable=true;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);int cw=texture.width/4,ch=texture.height/2;
            var rects=new Rect[8];int maxHeight=1;
            for(int i=0;i<8;i++)
            {
                int ox=(i%4)*cw,oy=(1-i/4)*ch,minX=cw,minY=ch,maxX=0,maxY=0;
                for(int y=0;y<ch;y++)for(int x=0;x<cw;x++)if(texture.GetPixel(ox+x,oy+y).a>.15f){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
                if(minX>maxX || minY>maxY)throw new Exception("Empty animation cell "+i);
                rects[i]=new Rect(ox+minX,oy+minY,maxX-minX+1,maxY-minY+1);maxHeight=Math.Max(maxHeight,maxY-minY+1);
            }
            var frames=new Sprite[8];
            for(int i=0;i<8;i++)
            {
                var rect=rects[i];float pivotX=((i%4)*cw+cw*.5f-rect.x)/rect.width;
                string fp=Folder+"/"+(i<4?"Idle":"Walk")+"Frame"+(i%4)+".asset";
                var sprite=Sprite.Create(texture,rect,new Vector2(pivotX,0),maxHeight/.98f,0,SpriteMeshType.FullRect);
                sprite.name=(i<4?"Idle":"Walk")+(i%4);
                var existingFrame=AssetDatabase.LoadAssetAtPath<Sprite>(fp);
                if(existingFrame!=null){EditorUtility.CopySerialized(sprite,existingFrame);UnityEngine.Object.DestroyImmediate(sprite);sprite=existingFrame;}else AssetDatabase.CreateAsset(sprite,fp);frames[i]=sprite;
            }
            string prefabPath=Folder+"/CorruptedCrabSoldier.prefab";
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var visual=root.transform.Find("SpriteVisual");visual.localRotation=Quaternion.Euler(50,0,0);
                visual.GetComponent<SpriteRenderer>().sprite=frames[0];
                var animation=visual.GetComponent<CrabSpriteAnimation>();if(animation==null)animation=visual.gameObject.AddComponent<CrabSpriteAnimation>();
                var so=new SerializedObject(animation);
                foreach(string name in new[]{"idleFrames","walkFrames"})
                {
                    var a=so.FindProperty(name);a.arraySize=4;for(int i=0;i<4;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=frames[i+(name=="idleFrames"?0:4)];
                }
                so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var player=UnityEngine.Object.FindFirstObjectByType<HongGildong>();
            var camera=UnityEngine.Object.FindFirstObjectByType<ClearingCamera>();
            var cs=new SerializedObject(camera);cs.FindProperty("followTarget").objectReferenceValue=player.transform;
            cs.FindProperty("verticalSize").floatValue=2.6f;cs.FindProperty("minimumHalfWidth").floatValue=3.9f;cs.ApplyModifiedPropertiesWithoutUndo();
            camera.transform.rotation=Quaternion.Euler(50,0,0);
            camera.transform.position=new Vector3(player.transform.position.x,0,player.transform.position.z)+new Vector3(0,10,-8.141f);
            camera.GetComponent<Camera>().orthographicSize=2.6f;
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if(renderer.GetComponentInParent<HongGildong>()!=null || renderer.GetComponentInParent<StrawTarget>()!=null || renderer.GetComponentInParent<MonsterHealth>()!=null)
                    renderer.transform.rotation=camera.transform.rotation;
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("READABILITY_SETUP_OK: 4 Idle + 4 Walk, sheet="+texture.width+"x"+texture.height+", camera 4.5 to 2.6, tilt 36 to 50; physical player scale unchanged");
        }
    }
}
