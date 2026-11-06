using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Storybook.Editor
{
    [InitializeOnLoad]
    public static class ProjectAssetAudit
    {
        [Serializable] public class Entry { public string path,category,reason;public long bytes;public string[] incoming; }
        [Serializable] public class Report
        {
            public string scene;public string[] buildScenes,resources,addressables,dynamicLoadCode,missingReferences,missingScripts;
            public int assetFiles,metaFiles;public long contentBytes,totalBytes;
            public Entry[] entries;public string[] duplicateImages;
        }
        static bool HasReferenceId<T>(T value) => !EqualityComparer<T>.Default.Equals(value,default(T));
        static ProjectAssetAudit(){EditorApplication.delayCall+=Requested;}
        static void Requested()
        {
            if(!File.Exists(".utmp/audit-project") || EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(".utmp/audit-project");try{Run();}catch(Exception ex){Debug.LogException(ex);}
        }
        [MenuItem("Storybook/Audit Project Assets")]
        public static void Run()
        {
            var files=Directory.GetFiles("Assets","*",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).OrderBy(p=>p).ToArray();
            var assets=files.Where(p=>!p.EndsWith(".meta")).ToArray();
            var scene=EditorSceneManager.GetActiveScene();
            var buildScenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            var roots=new HashSet<string>(buildScenes);if(!string.IsNullOrEmpty(scene.path))roots.Add(scene.path);
            var resources=assets.Where(p=>p.Contains("/Resources/") || p.Contains("/StreamingAssets/")).ToArray();foreach(var p in resources)roots.Add(p);
            var addressables=assets.Where(p=>p.Contains("Addressable")).ToArray();
            // Include serialized project-setting references, even for inactive pipelines.
            foreach(var file in Directory.GetFiles("ProjectSettings","*",SearchOption.AllDirectories))
                foreach(Match match in Regex.Matches(File.ReadAllText(file),@"guid:\s*([a-fA-F0-9]{32})"))
                {string p=AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);if(p.StartsWith("Assets/"))roots.Add(p);}
            var used=new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(),true));
            var typeFiles=new Dictionary<Type,string>();
            foreach(var file in assets.Where(p=>p.EndsWith(".cs")))
            {
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(file);var type=script!=null?script.GetClass():null;
                if(type!=null)typeFiles[type]=file;
            }
            foreach(var file in used.Where(p=>p.EndsWith(".cs")).ToArray())
            {
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(file);var type=script!=null?script.GetClass():null;
                for(var parent=type!=null?type.BaseType:null;parent!=null;parent=parent.BaseType)
                    if(typeFiles.TryGetValue(parent,out var baseFile))used.Add(baseFile);
            }
            var incoming=new Dictionary<string,List<string>>();var missingRefs=new List<string>();
            foreach(var file in assets.Where(p=>new[]{".unity",".prefab",".asset",".mat",".controller",".anim"}.Contains(Path.GetExtension(p))))
            {
                foreach(Match match in Regex.Matches(File.ReadAllText(file),@"guid:\s*([a-fA-F0-9]{32})"))
                {
                    string guid=match.Groups[1].Value;if(guid.StartsWith("0000000000000000"))continue;
                    string p=AssetDatabase.GUIDToAssetPath(guid);
                    if(string.IsNullOrEmpty(p)){missingRefs.Add(file+": "+guid);continue;}
                    if(!incoming.ContainsKey(p))incoming[p]=new List<string>();if(!incoming[p].Contains(file))incoming[p].Add(file);
                }
            }
            var missingScripts=new List<string>();
            Action<GameObject,string> scan=(root,label)=>{
                foreach(var component in root.GetComponentsInChildren<Component>(true))
                {
                    if(component==null)continue;
                    var so=new SerializedObject(component);var property=so.GetIterator();
                    while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue==null && HasReferenceId(property.objectReferenceEntityIdValue))
                        missingRefs.Add(label+"/"+component.name+": "+property.propertyPath);
                }
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)missingScripts.Add(label+"/"+t.name);
            };
            foreach(var root in scene.GetRootGameObjects())scan(root,scene.path);
            foreach(var path in assets.Where(p=>p.EndsWith(".prefab")))
            {
                var root=PrefabUtility.LoadPrefabContents(path);try{scan(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var code=assets.Where(p=>p.EndsWith(".cs")).ToArray();var entries=new List<Entry>();
            foreach(var path in assets)
            {
                string category,reason;incoming.TryGetValue(path,out var refs);
                if(used.Contains(path)){category="A";reason="Build/current scene or serialized ProjectSettings dependency, including inactive objects";}
                else if(path.EndsWith(".cs")){category="C";reason="Editor startup/menu or dynamic tooling; not safe to infer unused from scene dependencies";}
                else if(path=="Assets/Storybook/Art/SlashFront.png" || path=="Assets/Storybook/Art/SlashBack.png")
                {
                    bool codeRef=code.Where(p=>!p.EndsWith("/ProjectAssetAudit.cs")).Any(p=>File.ReadAllText(p).Contains(Path.GetFileName(path)));
                    if((refs==null || refs.Count==0) && !codeRef){category="B";reason="Old slash image; current swing uses SwordSweep; no serialized incoming or code filename reference";}
                    else{category="C";reason="Old image still has serialized/code reference";}
                }
                else{category="C";reason="Not in runtime dependency closure; may be retained backup/source or dynamically loaded by editor tooling";}
                entries.Add(new Entry{path=path,category=category,reason=reason,bytes=new FileInfo(path).Length,incoming=refs==null?new string[0]:refs.ToArray()});
            }
            var imageGroups=new Dictionary<string,List<string>>();
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var path in assets.Where(p=>p.EndsWith(".png")))
            {
                string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));if(!imageGroups.ContainsKey(hash))imageGroups[hash]=new List<string>();imageGroups[hash].Add(path);
            }
            var report=new Report{scene=scene.path,buildScenes=buildScenes,resources=resources,addressables=addressables,
                dynamicLoadCode=code.Where(p=>Regex.IsMatch(File.ReadAllText(p),@"Resources\.Load|Addressables|LoadAssetAtPath|AssetBundle|StreamingAssets")).ToArray(),
                missingReferences=missingRefs.Distinct().ToArray(),missingScripts=missingScripts.Distinct().ToArray(),
                assetFiles=assets.Length,metaFiles=files.Count(p=>p.EndsWith(".meta")),contentBytes=assets.Sum(p=>new FileInfo(p).Length),totalBytes=files.Sum(p=>new FileInfo(p).Length),entries=entries.ToArray(),
                duplicateImages=imageGroups.Values.Where(g=>g.Count>1).Select(g=>string.Join(" | ",g)).ToArray()};
            Directory.CreateDirectory(".utmp");File.WriteAllText(".utmp/asset-audit.json",JsonUtility.ToJson(report,true));
            Debug.Log("PROJECT_ASSET_AUDIT_OK: A="+entries.Count(e=>e.category=="A")+", B="+entries.Count(e=>e.category=="B")+", C="+entries.Count(e=>e.category=="C")+", missing GUID="+report.missingReferences.Length+", missing scripts="+report.missingScripts.Length+", total bytes="+report.totalBytes);
        }
    }
}
