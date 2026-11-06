using System;
using System.IO;
using UnityEngine;
using UnityEditor;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class CrabTextureQualityFix
    {
        const string Folder="Assets/Storybook/Enemies/CrabSoldier";
        static CrabTextureQualityFix(){EditorApplication.delayCall+=Run;}
        static void Run()
        {
            if(!File.Exists(".utmp/crab-quality-request") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/crab-quality-request");try{Fix();}catch(Exception ex){Debug.LogException(ex);}
        }
        static void Fix()
        {
            string path=Folder+"/CrabSoldierStyleSheet.png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);int oldWidth=texture.width,oldHeight=texture.height;
            var sprites=new Sprite[8];var rects=new Rect[8];var pivots=new Vector2[8];var ppu=new float[8];var sizes=new Vector3[8];
            for(int i=0;i<8;i++)
            {
                sprites[i]=AssetDatabase.LoadAssetAtPath<Sprite>(Folder+"/"+(i<4?"Idle":"Walk")+"Frame"+(i%4)+".asset");
                rects[i]=sprites[i].rect;pivots[i]=new Vector2(sprites[i].pivot.x/rects[i].width,sprites[i].pivot.y/rects[i].height);ppu[i]=sprites[i].pixelsPerUnit;sizes[i]=sprites[i].bounds.size;
            }
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth,out int sourceHeight);
            importer.maxTextureSize=2048;importer.npotScale=TextureImporterNPOTScale.None;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
            importer.SaveAndReimport();texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            float ratioX=(float)texture.width/oldWidth,ratioY=(float)texture.height/oldHeight;
            if(Mathf.Abs(ratioX-ratioY)>.01f)throw new Exception("Nonuniform source aspect; frame correction needs manual review.");
            for(int i=0;i<8;i++)
            {
                Rect rect=rects[i];rect=new Rect(rect.x*ratioX,rect.y*ratioY,rect.width*ratioX,rect.height*ratioY);
                var updated=Sprite.Create(texture,rect,pivots[i],ppu[i]*ratioX,0,SpriteMeshType.FullRect);
                updated.name=sprites[i].name;EditorUtility.CopySerialized(updated,sprites[i]);UnityEngine.Object.DestroyImmediate(updated);EditorUtility.SetDirty(sprites[i]);
                if(Vector3.Distance(sprites[i].bounds.size,sizes[i])>.002f)throw new Exception("Sprite world size changed unexpectedly: "+i);
            }
            AssetDatabase.SaveAssets();
            var hero=UnityEngine.Object.FindFirstObjectByType<HongGildong>();
            var heroBody=(SpriteRenderer)new SerializedObject(hero).FindProperty("body").objectReferenceValue;
            var crab=UnityEngine.Object.FindFirstObjectByType<CrabSpriteAnimation>();
            Debug.Log("CRAB_QUALITY_FIXED: source="+sourceWidth+"x"+sourceHeight+", imported="+oldWidth+"x"+oldHeight+" -> "+texture.width+"x"+texture.height+", PPU="+ppu[0]+" -> "+sprites[0].pixelsPerUnit+"; all 8 frame world bounds preserved; heroScale="+heroBody.transform.lossyScale+", crabScale="+crab.transform.lossyScale);
        }
    }
}
