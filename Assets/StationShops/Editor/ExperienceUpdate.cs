using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LineFour.Oido;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LineFour.Streets {
public static class ExperienceUpdate {
    public static void Build(){
        EditorSceneManager.OpenScene("Assets/Scene/Oido_Run.unity");
        var scene=SceneManager.GetActiveScene();
        var previous=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Original station architecture");
        if(previous)Object.DestroyImmediate(previous);
        var root=new GameObject("Original station architecture");
        var names=new[]{"Jeongwang","SingilOncheon","Ansan"};
        for(int i=0;i<3;i++){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blender/StationBuildings/"+names[i]+".fbx");
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            obj.transform.SetParent(root.transform,false);
            // Blender -Y is +Z in Unity. Station models centre on their platforms.
            obj.transform.rotation=Quaternion.Euler(0,180,0)*obj.transform.rotation;
            obj.transform.position=new Vector3(2.65f,0,RouteSchedule.Stops[i+1]-93);
            foreach(var renderer in obj.GetComponentsInChildren<Renderer>()){
                var mats=renderer.sharedMaterials;
                for(int m=0;m<mats.Length;m++){
                    var imported=mats[m];
                    string path="Assets/Blender/StationBuildings/Station_"+imported.name+".mat";
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(!mat){
                        mat=new Material(Shader.Find("Standard"));mat.color=imported.color;
                        mat.mainTexture=imported.mainTexture;mat.SetFloat("_Glossiness",.18f);
                        AssetDatabase.CreateAsset(mat,path);
                    }
                    mats[m]=mat;
                }
                renderer.sharedMaterials=mats;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
        var city=scene.GetRootGameObjects().Single(o=>o.name=="역세권 상가");
        int moved=0;
        foreach(var filter in city.GetComponentsInChildren<MeshFilter>()){
            if(!filter.name.Contains("HighSt") && !filter.name.Contains("Rear"))continue;
            var renderer=filter.GetComponent<Renderer>();var bounds=renderer.bounds;
            // Reserve the measured entrances of the four authored stations.
            bool station=RouteSchedule.Stops.Take(4).Any(stop=>bounds.min.z<stop+48 && bounds.max.z>stop-245);
            if(!station && Mathf.Abs(filter.transform.position.x)>100){
                filter.transform.position-=Vector3.right*(filter.transform.position.x<0?-85:85);
            }
            if(station && Mathf.Abs(filter.transform.position.x)<80){
                filter.transform.position+=Vector3.right*(filter.transform.position.x<0?-85:85);moved++;
            }
        }
        var camera=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>().cabCamera;
        camera.transform.position=new Vector3(-65,36,RouteSchedule.Stops[2]-70);
        camera.transform.LookAt(new Vector3(-20,6,RouteSchedule.Stops[2]-93));
        Shot(camera,"RestoredStation");
        // The imported Blender files only contribute the original architecture.
        // The existing platforms, track and animated screen doors stay in the scene.
        var sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
        if(!sim || !Object.FindFirstObjectByType<PlatformScreenDoors>())throw new InvalidDataException("Missing original station components");
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        if(!EditorSceneManager.SaveScene(scene))throw new IOException("Save");
        File.WriteAllText("Library/ExperienceCheck.txt","Station streets moved: "+moved+"\n");
        SessionState.SetInt("ExperienceStage",1);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]static void Init(){EditorApplication.update+=Tick;}
    static int frame;
    static void Require(bool pass,string text){if(!pass)throw new InvalidDataException(text);}
    static void Tick(){
        int stage=SessionState.GetInt("ExperienceStage",0);
        if(stage==0||!EditorApplication.isPlaying||Time.frameCount<Math.Max(15,frame))return;
        try{
            var sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
            if(stage==1){
                sim.BeginRun();
                var forward=sim.cabCamera.transform.forward;
                Require(Mathf.Abs(forward.x)<.001f&&Mathf.Abs(sim.cabCamera.transform.eulerAngles.z)<.01f,"Cab level");
                var saloons=sim.consist.Cast<Transform>().Where(t=>t.name.StartsWith("Saloon occupants")).ToArray();
                Require(saloons.Length==10&&saloons.All(t=>t.childCount==15),"150 commuters");
                Shot(sim.cabCamera,"CabLevel");
                while(sim.View!=0)sim.CycleView();
                var q=sim.cabCamera.transform.rotation;sim.TurnView(35,12);
                Require(Quaternion.Angle(q,sim.cabCamera.transform.rotation)>25,"Orbit");
                Shot(sim.cabCamera,"StationClear");
                while(sim.View!=2)sim.CycleView();
                var move=typeof(LineFour.Oido.Line4Drive).GetMethod("Walk",BindingFlags.NonPublic|BindingFlags.Instance);
                Shot(sim.cabCamera,"SaloonEntry");
                var start=sim.cabCamera.transform.position;
                move.Invoke(sim,new object[]{new Vector2(0,1),4f});sim.TurnView(0,0);
                Require(Vector3.Distance(start,sim.cabCamera.transform.position)>8,"Walk saloon");
                foreach(var cabin in saloons.Take(1))
                    foreach(var rider in cabin.Cast<Transform>().Take(3))
                        foreach(var ren in rider.GetComponentsInChildren<Renderer>())
                            Debug.Log("PERSON "+rider.name+" pos "+rider.position+" scale "+rider.lossyScale+" bounds "+ren.bounds);
                foreach(var obj in SceneManager.GetActiveScene().GetRootGameObjects())
                    if(obj.name.Contains("OidoStation"))foreach(var ren in obj.GetComponentsInChildren<Renderer>().Take(5))Debug.Log("STATION "+ren.name+" "+ren.bounds);
                Shot(sim.cabCamera,"CrowdedSaloon");
                // Test walking across every gangway to the rear cabin and back.
                move.Invoke(sim,new object[]{new Vector2(0,1),120f});sim.TurnView(0,0);
                Require(sim.cabCamera.transform.position.z-sim.consist.position.z<-199,"End of consist");
                move.Invoke(sim,new object[]{new Vector2(0,-1),120f});sim.TurnView(0,0);
                Require(sim.cabCamera.transform.position.z-sim.consist.position.z>-4,"Front limit");
                sim.run.Service();frame=Time.frameCount+10;
            }else{
                if(sim.run.DoorTravel<.99f)return;
                Require(sim.doorCars.Any(r=>r.GetBlendShapeWeight(0)>99||r.GetBlendShapeWeight(1)>99),"Doors");
                File.AppendAllText("Library/ExperienceCheck.txt","PASS 150 commuters; level cab; orbit; walking 10 cars; animated doors\n");
                SessionState.SetInt("ExperienceStage",0);EditorApplication.Exit(0);return;
            }
            SessionState.SetInt("ExperienceStage",stage+1);
        }catch(Exception e){Debug.LogException(e);File.AppendAllText("Library/ExperienceCheck.txt",e.ToString());SessionState.SetInt("ExperienceStage",0);EditorApplication.Exit(1);}
    }
    static void Shot(Camera camera,string name){
        var rt=RenderTexture.GetTemporary(1100,700,24);var prev=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(1100,700,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1100,700),0,0);tex.Apply();File.WriteAllBytes("Library/"+name+".png",tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
    }
}}
