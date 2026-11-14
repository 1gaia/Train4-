using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using LineFour.Oido;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LineFour.Streets
{
    public static class CityConsistUpgrade
    {
        const string Folder="Assets/Blender/CityConsist/";
        const string ScenePath="Assets/Scene/Oido_Run.unity";
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
            var body=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"Line4_Sliding.fbx");
            var cab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"ApprovedConsoles.fbx");
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Line4_Palette.png");
            var skins=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(r=>r.name);
            if(skins.Count!=30 || !atlas || sim.doorCars.Length!=30)throw new InvalidDataException("Train import");
            var finishes=new Dictionary<string,Material>();
            foreach(var r in sim.doorCars){
                var src=skins[r.name];
                if(src.sharedMesh.blendShapeCount!=2)throw new InvalidDataException(src.name);
                var old=r.sharedMaterials.ToDictionary(m=>m.name.Replace("Train ",""));
                var slots=src.sharedMaterials.Select(m=>{
                    if(finishes.TryGetValue(m.name,out var finish))return finish;
                    var original=old.TryGetValue(m.name,out var value)?value:AssetDatabase.LoadAssetAtPath<Material>("Assets/OidoLine4/RouteAssets/Train "+m.name+".mat");
                    string path=Folder+"Train "+m.name+".mat";
                    finish=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!finish){finish=new Material(original);AssetDatabase.CreateAsset(finish,path);}
                    finish.mainTexture=atlas;EditorUtility.SetDirty(finish);finishes[m.name]=finish;return finish;
                }).ToArray();
                r.sharedMesh=src.sharedMesh;r.sharedMaterials=slots;
            }
            var root=sim.doorCars[0].GetComponentInParent<LODGroup>().transform.parent;
            var oldCab=root.Find("ApprovedConsoles");if(oldCab)Object.DestroyImmediate(oldCab.gameObject);
            var cabInstance=(GameObject)PrefabUtility.InstantiatePrefab(cab,scene);
            cabInstance.transform.SetParent(root,false);
            var renderers=cabInstance.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length!=2)throw new InvalidDataException("Expected two cab meshes");
            foreach(var renderer in renderers){
                var number=renderer.name.Contains("4954")?"4954":"4054";
                var skin=sim.doorCars[number=="4054"?0:27];
                if(!skin.bounds.Intersects(renderer.bounds) || renderer.bounds.size.z>4)
                    throw new InvalidDataException($"Alignment {number}: cab {renderer.bounds} body {skin.bounds}");
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++){
                    string key=slots[i].name.Split('.')[0];
                    if(!(key.StartsWith("display") || key.StartsWith("dial")))continue;
                    string art=(number=="4954" && key.StartsWith("display")?"rear_":"")+key;
                    string path=Folder+art+".mat";
                    var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
                    m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+art+".png");
                    if(!m.mainTexture)throw new InvalidDataException(art);
                    m.color=Color.white;m.SetFloat("_Glossiness",.25f);
                    if(key.StartsWith("display")){m.EnableKeyword("_EMISSION");m.SetTexture("_EmissionMap",m.mainTexture);m.SetColor("_EmissionColor",Color.gray);}
                    EditorUtility.SetDirty(m);slots[i]=m;
                }
                renderer.sharedMaterials=slots;
                var group=skin.GetComponentInParent<LODGroup>();var lods=group.GetLODs();
                for(int i=0;i<lods.Length;i++)lods[i].renderers=lods[i].renderers.Where(r=>r).ToArray();
                lods[0].renderers=lods[0].renderers.Concat(new Renderer[]{renderer}).ToArray();group.SetLODs(lods);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            StationShopBuilder.Build();
            var city=scene.GetRootGameObjects().Single(x=>x.name=="역세권 상가");
            if(city.GetComponentsInChildren<MeshFilter>().Length<1800 || !Object.FindFirstObjectByType<PlatformScreenDoors>())
                throw new InvalidDataException("Scenery or platform doors missing");
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
            if(sim.doorCars.Any(r=>!r || !r.sharedMesh || r.sharedMesh.blendShapeCount!=2))throw new InvalidDataException("Saved train");
            var camera=sim.cabCamera;
            camera.transform.position=new Vector3(.54f,2.89f,sim.cabEyeZ);
            camera.transform.rotation=Quaternion.LookRotation(new Vector3(0,-.30f,1));
            Capture(camera,"Library/StationCityCab.png");
            camera.transform.position=new Vector3(24,49,38);
            camera.transform.LookAt(new Vector3(-32,7,-60));
            Capture(camera,"Library/StationCityOido.png");
            Debug.Log("CITY_CONSIST_VERIFIED");
        }
        [InitializeOnLoadMethod]
        static void RestorePlayCheck(){
            if(Application.isBatchMode)EditorApplication.update+=CheckFrame;
        }
        public static void PlayCheck(){
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetInt("CityPlayStage",1);
            EditorApplication.isPlaying=true;
        }
        static int frame;
        static void CheckFrame(){
            int stage=SessionState.GetInt("CityPlayStage",0);
            if(stage==0 || !EditorApplication.isPlaying || Time.frameCount<Math.Max(12,frame))return;
            try{
                var sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
                if(stage==1){
                    sim.BeginRun();while(sim.View!=1)sim.CycleView();
                    sim.run.Service();frame=Time.frameCount+8;
                }else if(stage==2){
                    if(sim.run.DoorTravel<.95f)return;
                    if(!sim.doorCars.Any(r=>r.GetBlendShapeWeight(0)>90 || r.GetBlendShapeWeight(1)>90))
                        throw new InvalidDataException("Door motion failed");
                    Capture(sim.cabCamera,"Library/CityCabPlay.png");
                    sim.run.Target=13;sim.run.Served=12;sim.run.Distance=LineFour.Oido.RouteSchedule.Stops[13]-130;
                    sim.run.Phase=LineFour.Oido.DoorPhase.Closed;sim.run.Speed=13;while(sim.View!=0)sim.CycleView();frame=Time.frameCount+15;
                }else{
                    if(sim.run.Distance<=LineFour.Oido.RouteSchedule.Stops[13]-130)throw new InvalidDataException("Drive did not advance");
                    Capture(sim.cabCamera,"Library/CityDrivePlay.png");
                    Debug.Log("CITY_PLAY_VERIFIED");SessionState.SetInt("CityPlayStage",0);EditorApplication.Exit(0);return;
                }
                SessionState.SetInt("CityPlayStage",stage+1);
            }catch(Exception e){Debug.LogException(e);SessionState.SetInt("CityPlayStage",0);EditorApplication.Exit(1);}
        }
        static void Capture(Camera camera,string path){
            var rt=RenderTexture.GetTemporary(1200,720,24);
            var tex=new Texture2D(1200,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,1200,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
        }
    }
}
