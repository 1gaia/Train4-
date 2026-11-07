using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using System;
using System.IO;
using System.Linq;

namespace LineFour.Oido.EditorTools
{
    [InitializeOnLoad]
    internal static class Line4Setup
    {
        const string Folder="Assets/OidoLine4";
        const string SceneFile=Folder+"/Oido_Run.unity";
        const string StartRequest="Library/AnsanLine_Upgrade.request";
        const string VerifyRequest="Library/AnsanLine_Verify.request";
        const string Report="Library/AnsanLine_Result.txt";
        static int verificationStage;
        static Line4Setup() {EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if(File.Exists(StartRequest) && EditorApplication.isPlaying) {
                EditorApplication.isPlaying=false; return;
            }
            if(File.Exists(StartRequest) && !EditorApplication.isPlayingOrWillChangePlaymode) {
                File.Delete(StartRequest);
                try { Build(); } catch(Exception e) {File.AppendAllText(Report,"ERROR "+e+"\n");Debug.LogException(e);}
            }
            if(File.Exists(VerifyRequest) && EditorApplication.isPlaying && Time.frameCount>12)
            {
                var sim=UnityEngine.Object.FindFirstObjectByType<Line4Drive>();
                if(!sim || !sim.cabCamera.isActiveAndEnabled || sim.consist.GetComponentsInChildren<LODGroup>().Length!=10) return;
                if(verificationStage==0) {
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/AnsanLine_Aerial.png"));
                    Quaternion before=sim.cabCamera.transform.rotation;
                    sim.TurnView(35,12);
                    Require(Quaternion.Angle(before,sim.cabCamera.transform.rotation)<.01f && sim.cabCamera.transform.position.y>20,"fixed overhead");
                    for(int view=1;view<=2;view++){
                        sim.CycleView();var initial=sim.cabCamera.transform.rotation;sim.TurnView(35,12);
                        Require(Quaternion.Angle(initial,sim.cabCamera.transform.rotation)>30,"interior mouse look");
                    }
                    sim.CycleView();sim.run.Service();verificationStage=1;
                } else if(verificationStage==1 && sim.run.Phase==DoorPhase.Boarding && sim.run.Dwell>.2f) {
                    for(int i=0;i<sim.doorCars.Length;i++) {
                        var car=sim.doorCars[i];int chosen=sim.platformShapes[i];
                        Require(car.GetBlendShapeWeight(chosen)>99 && car.GetBlendShapeWeight(1-chosen)==0,"animated platform side");
                    }
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/Oido_Open.png"));verificationStage=2;
                } else if(verificationStage==2 && sim.run.Dwell>3.1f) {
                    sim.run.Service();verificationStage=3;
                } else if(verificationStage==3 && !sim.run.Doors) {
                    Require(sim.run.Target==1 && sim.run.Distance==0,"origin departure");
                    sim.run.Target=13;sim.run.Served=12;sim.run.Distance=RunState.Stops[13];verificationStage=4;
                } else if(verificationStage==4 && sim.cabCamera.transform.position.z>RunState.Stops[13]) {
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/AnsanLine_Geumjeong.png")); verificationStage=5;
                } else if(verificationStage==5) {
                    sim.run.Served=-1;sim.run.RestartSection();
                    const string resume="Library/AnsanLine_resume.txt";
                    if(File.Exists(resume)){
                        var fields=File.ReadAllText(resume).Trim().Split(',');
                        int served=int.Parse(fields[0]);
                        sim.run.Served=served;sim.run.RestartSection();
                        sim.run.Distance=Mathf.Clamp(float.Parse(fields[1],System.Globalization.CultureInfo.InvariantCulture),
                            RunState.Stops[served],RunState.Stops[served+1]+26);
                        File.Delete(resume);
                    }
                    File.Delete(VerifyRequest);
                    File.AppendAllText(Report,"PLAY_OK aerial fixed; cab and saloon rotated; door interlocks; terminus visited; current distance "+sim.run.Distance+"\n");
                }
            }
        }
        static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
        static void TestControls()
        {
            var r=new RunState();
            for(int target=0;target<RunState.Stops.Length;target++) {
                if(target>0){
                    for(int i=0;i<150;i++)r.Step(.02f,true,false,false);
                    Require(r.Speed>2,"traction");r.Service();Require(!r.Doors,"moving interlock");
                    r.Distance=RunState.Stops[target]; r.Service();Require(!r.Doors,"at platform but moving");
                    for(int i=0;i<150;i++)r.Step(.02f,false,false,true);
                    Require(r.Stopped,"emergency brake");
                    r.Distance=RunState.Stops[target]+5;r.Service();Require(!r.Doors,"outside stopping zone");
                }
                r.Distance=RunState.Stops[target];r.Service();r.Step(.8f,true,false,false);
                Require(r.Doors && r.DoorTravel>.45f && r.DoorTravel<.55f && r.Stopped,"opening travel");
                r.Step(.9f,true,false,false);r.Service();Require(r.Phase==DoorPhase.Boarding,"minimum dwell");
                r.Step(3.1f,true,false,false);r.Service();r.Step(.9f,true,false,false);
                Require(r.Phase==DoorPhase.Closing && r.Stopped && r.Served<target,"closing interlock");
                r.Step(1,true,false,false);Require(!r.Doors && r.Served==target,"serve");
            }
            Require(r.Complete,"terminus");r.RestartSection();Require(r.Target==0 && r.Distance==0,"replay");
        }
        static Renderer Level(Transform root,string name) =>
            root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name==name);
        static int PlatformMorph(SkinnedMeshRenderer r)
        {
            Require(r.sharedMesh.blendShapeCount==2,"Each car requires two moving sides: "+r.name);
            var posed=new Mesh();r.BakeMesh(posed);var closed=posed.vertices;
            int platform=-1;
            for(int shape=0;shape<2;shape++) {
                r.SetBlendShapeWeight(shape,100);r.BakeMesh(posed);var opened=posed.vertices;
                int count=0;float side=0;
                for(int i=0;i<closed.Length;i++){
                    if((closed[i]-opened[i]).sqrMagnitude<1e-8f)continue;
                    var a=r.transform.TransformPoint(closed[i]);var b=r.transform.TransformPoint(opened[i]);
                    if(Mathf.Abs(a.z-b.z)<.45f)continue;
                    side+=a.x;count++;
                }
                Require(count>10,"No sliding vertices "+r.name+" #"+shape);
                if(side/count<0) {Require(platform<0,"Both morphs on same side");platform=shape;}
                r.SetBlendShapeWeight(shape,0);
            }
            UnityEngine.Object.DestroyImmediate(posed);
            Require(platform>=0,"No platform-facing door "+r.name);return platform;
        }
        static void Build()
        {
            File.WriteAllText(Report,"Starting scene creation\n");
            TestControls();
            if(File.Exists(SceneFile)) { UpgradeRoute(); return; }
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Line4_Sliding.fbx");
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Line4_Palette.png");
            Require(source && atlas,"Train FBX or palette missing");
            // Preserve all open scenes before switching to the driving scene.
            for(int i=0;i<SceneManager.sceneCount;i++) {
                var old=SceneManager.GetSceneAt(i);
                if(old.isDirty) {
                    string saved=string.IsNullOrEmpty(old.path)?AssetDatabase.GenerateUniqueAssetPath(Folder+"/Previous_Untitled.unity"):
                        AssetDatabase.GenerateUniqueAssetPath(Folder+"/Previous_"+old.name+".unity");
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
            sim.doorCars=new SkinnedMeshRenderer[30];sim.platformShapes=new int[30];
            for(int i=0;i<10;i++)
            {
                string name="Car_"+(4054+100*i);
                var car=train.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
                var group=car.gameObject.AddComponent<LODGroup>();
                var levels=new LOD[3];
                for(int lod=0;lod<3;lod++) {
                    var r=Level(car,name+"_LOD"+lod);Require(r,"LOD mesh missing "+name);
                    r.enabled=true;r.gameObject.SetActive(true);r.shadowCastingMode=ShadowCastingMode.Off;
                    var skin=(SkinnedMeshRenderer)r;sim.doorCars[3*i+lod]=skin;
                    sim.platformShapes[3*i+lod]=PlatformMorph(skin);
                    levels[lod]=new LOD(new[]{.17f,.055f,.004f}[lod],new[]{r});
                }
                group.SetLODs(levels);group.RecalculateBounds();
            }
            var remaps=new System.Collections.Generic.Dictionary<Material,Material>();
            foreach(var r in train.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
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
            Line4Environment.Build(new GameObject("Oido - Geumjeong").transform,data);
            var camera=new GameObject("Driving camera",typeof(Camera),typeof(AudioListener));
            camera.tag="MainCamera";sim.cabCamera=camera.GetComponent<Camera>();sim.cabCamera.nearClipPlane=.055f;
            sim.cabCamera.farClipPlane=285;sim.cabCamera.fieldOfView=65;
            sim.cabCamera.clearFlags=CameraClearFlags.Skybox;sim.cabCamera.backgroundColor=new Color(.56f,.76f,.86f);
            camera.transform.position=new Vector3(12,30,14);
            camera.transform.LookAt(new Vector3(0,1.9f,-18));
            var light=new GameObject("Tunnel illumination").AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.03f; light.color=new Color(.97f,.96f,.9f);
            light.transform.rotation=Quaternion.Euler(41,-27,0);light.shadows=LightShadows.None;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.60f,.66f,.70f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=sim.cabCamera.backgroundColor;RenderSettings.fogDensity=.0035f;
            Require(EditorSceneManager.SaveScene(scene,SceneFile),"Saving route scene failed");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(SceneFile,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=SceneFile)).ToArray();
            AssetDatabase.SaveAssets();Selection.activeObject=null;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,2,-5),camera.transform.rotation,15);
            File.AppendAllText(Report,"SCENE_OK "+SceneFile+"; 10 LOD groups; 14 stops; 30 verified platform morphs; all station interlock tests passed\n");
            File.WriteAllText(VerifyRequest,"play");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying=true;
        }
        static void UpgradeRoute()
        {
            var scene=SceneManager.GetActiveScene();
            Require(scene.path==SceneFile,"Open the Oido driving scene before the route upgrade");
            var old=scene.GetRootGameObjects().Single(o=>o.name=="Oido - Hanyang Univ. at Ansan"||o.name=="Oido - Geumjeong");
            var sim=UnityEngine.Object.FindFirstObjectByType<Line4Drive>();
            Require(sim && sim.consist && sim.doorCars.Length==30,"Original consist missing");
            string data=AssetDatabase.GenerateUniqueAssetPath(Folder+"/GeumjeongStations");
            AssetDatabase.CreateFolder(Folder,Path.GetFileName(data));
            var route=new GameObject("Oido - Geumjeong");
            try {
                Line4Environment.Build(route.transform,data);
                for(int i=0;i<RunState.Stops.Length;i++){
                    int id=456-i;
                    var station=route.transform.Cast<Transform>().Single(t=>t.name.StartsWith(id+" "));
                    Require(station.Find("Platform_"+id) && station.Find("S"+id),"Missing platform/sign "+id);
                }
            } catch {UnityEngine.Object.DestroyImmediate(route); throw;}
            File.Copy(SceneFile,Path.Combine("Library","Oido_before_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity"));
            UnityEngine.Object.DestroyImmediate(old);
            var cam=sim.cabCamera;cam.transform.position=new Vector3(12,30,14);
            cam.transform.LookAt(new Vector3(0,1.9f,-18));
            Require(EditorSceneManager.SaveScene(scene),"Could not save expanded route");
            AssetDatabase.SaveAssets();
            File.AppendAllText(Report,"SCENE_OK Oido_Run, 14 platforms and sign sets, 14 stopping cycles, existing consist retained\n");
            File.WriteAllText(VerifyRequest,"play");verificationStage=0;
            EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;
        }
    }
}
