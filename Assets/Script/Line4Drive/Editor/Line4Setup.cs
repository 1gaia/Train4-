using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System;
using System.IO;
using System.Linq;

namespace LineFour.EditorTools
{
    [InitializeOnLoad]
    internal static class Line4Setup
    {
        const string Folder="Assets/Line4Drive";
        const string SceneFolder="Assets/Scene";
        const string SceneFile=SceneFolder+"/Line4_Run.unity";
        const string StartRequest="Library/Line4_Create.request";
        const string VerifyRequest="Library/Line4_Verify.request";
        const string Report="Library/Line4_Result.txt";
        static Line4Setup() {EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if(File.Exists(StartRequest) && !EditorApplication.isPlayingOrWillChangePlaymode) {
                File.Delete(StartRequest);
                try { Build(); } catch(Exception e) {File.AppendAllText(Report,"ERROR "+e+"\n");Debug.LogException(e);}
            }
            if(File.Exists(VerifyRequest) && EditorApplication.isPlaying && Time.frameCount>12)
            {
                var sim=UnityEngine.Object.FindFirstObjectByType<Line4Drive>();
                if(!sim || !sim.cabCamera.isActiveAndEnabled || sim.consist.GetComponentsInChildren<LODGroup>().Length!=10) return;
                File.Delete(VerifyRequest);
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/Line4_Play.png"));
                File.AppendAllText(Report,"PLAY_OK frame="+Time.frameCount+" camera="+sim.cabCamera.transform.position+"\n");
            }
        }
        static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        static void TestControls()
        {
            var r=new RunState();
            r.Service();Require(!r.Doors,"station interlock");
            for(int i=0;i<150;i++)r.Step(.02f,true,false,false);
            Require(r.Speed>2 && r.Distance>2,"traction");
            for(int i=0;i<150;i++)r.Step(.02f,false,false,true);
            Require(r.Stopped,"emergency brake");
            for(int target=1;target<3;target++) {
                r.Distance=RunState.Stops[target]-1;r.Service();Require(r.Doors,"open");
                r.Service();Require(r.Doors,"dwell interlock");r.Step(3.1f,true,false,false);
                Require(r.Stopped,"traction interlock");r.Service();Require(r.Served==target,"serve");
            }
            Require(r.Complete,"terminus");r.RestartSection();Require(r.Target==1 && r.Distance==0,"replay");
        }
        static Renderer Level(Transform root,string name) =>
            root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r=>r.name==name);
        static void Build()
        {
            File.WriteAllText(Report,"Starting scene creation\n");
            Require(!File.Exists(SceneFile),"Generated scene already exists");
            TestControls();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Blender/Line4_Unity.fbx");
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Blender/Line4_Palette.png");
            Require(source && atlas,"Train FBX or palette missing");
            // Preserve all open scenes before switching to the driving scene.
            for(int i=0;i<SceneManager.sceneCount;i++) {
                var old=SceneManager.GetSceneAt(i);
                if(old.isDirty) {
                    string saved=string.IsNullOrEmpty(old.path)?AssetDatabase.GenerateUniqueAssetPath(SceneFolder+"/Backup/Previous_Untitled.unity"):
                        AssetDatabase.GenerateUniqueAssetPath(SceneFolder+"/Backup/Previous_"+old.name+".unity");
                    Require(EditorSceneManager.SaveScene(old,saved,true),"Could not preserve "+old.name);
                    File.AppendAllText(Report,"Prior scene saved to "+saved+"\n");
                }
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string data=AssetDatabase.GenerateUniqueAssetPath(Folder+"/RouteAssets");
            AssetDatabase.CreateFolder(Folder,Path.GetFileName(data));
            var game=new GameObject("Line 4 | Route operation"); var sim=game.AddComponent<Line4Drive>();
            var consist=new GameObject("4054 | Ten-car consist").transform;sim.consist=consist;
            var train=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
            PrefabUtility.UnpackPrefabInstance(train,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            train.transform.SetParent(consist,false);
            var front=Level(train.transform,"Car_4054_LOD0");var rear=Level(train.transform,"Car_4954_LOD0");
            Require(front && rear,"Could not identify cab cars: "+string.Join(", ",train.GetComponentsInChildren<MeshRenderer>().Take(6).Select(r=>r.name)));
            var forward=front.bounds.center-rear.bounds.center;forward.y=0;
            train.transform.rotation=Quaternion.FromToRotation(forward.normalized,Vector3.forward)*train.transform.rotation;
            var b=front.bounds;
            Require(b.size.z>18 && b.size.z<23 && b.size.x>2.9f && b.size.x<4,"Unexpected car dimensions "+b.size);
            train.transform.position+=new Vector3(-b.center.x,.228f-b.min.y,-b.max.z);
            File.AppendAllText(Report,"Cab dimensions "+b.size+"; nose "+front.bounds.max+"\n");
            foreach(var group in train.GetComponentsInChildren<LODGroup>(true))UnityEngine.Object.DestroyImmediate(group);
            for(int i=0;i<10;i++)
            {
                string name="Car_"+(4054+100*i);
                var car=train.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
                var group=car.gameObject.AddComponent<LODGroup>();
                var levels=new LOD[3];
                for(int lod=0;lod<3;lod++) {
                    var r=Level(car,name+"_LOD"+lod);Require(r,"LOD mesh missing "+name);
                    r.enabled=true;r.gameObject.SetActive(true);r.shadowCastingMode=ShadowCastingMode.Off;
                    levels[lod]=new LOD(new[]{.17f,.055f,.004f}[lod],new[]{r});
                }
                group.SetLODs(levels);group.RecalculateBounds();
            }
            var remaps=new System.Collections.Generic.Dictionary<Material,Material>();
            foreach(var r in train.GetComponentsInChildren<MeshRenderer>(true)) {
                var slots=r.sharedMaterials;
                for(int i=0;i<slots.Length;i++) {
                    var original=slots[i]; if(!original)continue;
                    if(!remaps.TryGetValue(original,out var m)){
                        m=new Material(Shader.Find("Standard")){name=original.name,mainTexture=atlas,color=Color.white};
                        m.SetFloat("_Glossiness",.30f);
                        if(original.name.IndexOf("Glass",StringComparison.OrdinalIgnoreCase)>=0) {
                            m.SetFloat("_Mode",3);m.SetOverrideTag("RenderType","Transparent");
                            m.SetInt("_SrcBlend",(int)BlendMode.One);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                            m.SetInt("_ZWrite",0);m.EnableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=3000;
                            m.color=new Color(1,1,1,.27f);
                        }
                        AssetDatabase.CreateAsset(m,data+"/Train "+original.name+".mat");remaps.Add(original,m);
                    }
                    slots[i]=m;
                }
                r.sharedMaterials=slots;
            }
            Line4Environment.Build(new GameObject("Hyehwa - Dongdaemun - DDP").transform,data);
            var camera=new GameObject("Driving camera",typeof(Camera),typeof(AudioListener));
            camera.tag="MainCamera";sim.cabCamera=camera.GetComponent<Camera>();sim.cabCamera.nearClipPlane=.055f;
            sim.cabCamera.farClipPlane=285;sim.cabCamera.fieldOfView=65;
            sim.cabCamera.clearFlags=CameraClearFlags.SolidColor;sim.cabCamera.backgroundColor=new Color(.105f,.15f,.18f);
            camera.transform.position=new Vector3(-4.65f,2.8f,9.5f);
            camera.transform.LookAt(new Vector3(0,1.9f,-3.5f));
            var light=new GameObject("Tunnel illumination").AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.03f; light.color=new Color(.97f,.96f,.9f);
            light.transform.rotation=Quaternion.Euler(41,-27,0);light.shadows=LightShadows.None;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.60f,.66f,.70f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=sim.cabCamera.backgroundColor;RenderSettings.fogDensity=.008f;
            Require(EditorSceneManager.SaveScene(scene,SceneFile),"Saving route scene failed");
            if(EditorBuildSettings.scenes.Length==0)EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(SceneFile,true)};
            AssetDatabase.SaveAssets();Selection.activeObject=null;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,2,-5),camera.transform.rotation,15);
            File.AppendAllText(Report,"SCENE_OK "+SceneFile+"; 10 LOD groups; 3 platforms; controls test passed\n");
            File.WriteAllText(VerifyRequest,"play");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying=true;
        }
    }
}
