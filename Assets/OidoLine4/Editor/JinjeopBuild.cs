using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LineFour.Oido.EditorTools
{
    [InitializeOnLoad] internal static class JinjeopBuild
    {
        const string Folder="Assets/OidoLine4";
        const string Trigger="Library/Line4_51.request", Verify="Library/Line4_51.verify", Report="Library/Line4_51.txt";
        static int stage,frame;
        static JinjeopBuild(){EditorApplication.update+=Poll;}
        static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            try{
                if(File.Exists(Trigger)){
                    if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
                    if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                    File.Delete(Trigger);Build();return;
                }
                if(File.Exists(Verify)&&EditorApplication.isPlaying&&Time.frameCount>15)PlayCheck();
            }catch(Exception e){if(File.Exists(Verify))File.Delete(Verify);File.AppendAllText(Report,"FAIL "+e+"\n");Debug.LogException(e);}
        }
        static void ScoreTests()
        {
            var r=new RunState();
            for(int i=0;i<RunState.Stops.Length;i++){
                float error=i==1?2:i==2?-4:0;
                r.Distance=RunState.Stops[i]+error;r.Speed=0;
                r.Step(.1f,true,false,false);Check(r.Stopped,"Mandatory station stop");
                int before=r.Score;r.Service();r.Step(1.7f,true,false,false);r.Service();
                Check(r.Score==before+25 && r.Phase==DoorPhase.Boarding,"Open bonus once");
                r.RestartSection();Check(r.Doors,"No duplicate boarding reward on retry");
                r.Step(3.1f,true,false,false);r.Service();r.Step(1.9f,true,false,false);
                Check(r.Served==i&&r.Score-before==350-Mathf.RoundToInt(Mathf.Abs(error)*25),"Score at "+i);
            }
            Check(r.Complete,"Terminus");r.RestartSection();Check(r.Score==0,"New run clears score");
            r.Target=1;r.Served=0;r.Distance=RunState.Stops[1]+3.99f;r.Speed=6;
            r.Step(.1f,false,false,false);r.Step(.1f,false,false,false);r.Service();
            Check(r.Score==-100&&r.Misses==1&&r.Missed&&!r.Doors,"Single skip debit");
            r.RestartSection();Check(r.Target==1 && r.Score==-100 && r.Distance==RunState.Stops[1]-24,"Mandatory reapproach");
        }
        static float[] DoorCenters(Line4Drive sim)
        {
            var result=new List<float>();var mesh=new Mesh();
            for(int car=0;car<10;car++){
                int i=car*3;var r=sim.doorCars[i];int shape=sim.platformShapes[i];
                r.SetBlendShapeWeight(shape,0);r.BakeMesh(mesh);var a=mesh.vertices;
                r.SetBlendShapeWeight(shape,100);r.BakeMesh(mesh);var b=mesh.vertices;r.SetBlendShapeWeight(shape,0);
                var samples=new List<float>();
                for(int j=0;j<a.Length;j++){
                    var start=r.transform.TransformPoint(a[j]);var finish=r.transform.TransformPoint(b[j]);
                    if(Mathf.Abs(start.z-finish.z)>.4f && start.x<0)samples.Add(start.z);
                }
                samples.Sort();int begin=0,number=0;
                for(int j=1;j<=samples.Count;j++)if(j==samples.Count||samples[j]-samples[j-1]>1.25f){
                    result.Add((samples[begin]+samples[j-1])*.5f);begin=j;number++;
                }
                Check(number==4,"Passenger door alignment on car "+car+": "+number);
            }
            Object.DestroyImmediate(mesh);return result.OrderByDescending(z=>z).ToArray();
        }
        static void CopyAt(Transform template,Transform parent,float delta,string name){
            var g=Object.Instantiate(template.gameObject,parent);g.name=name;g.transform.position=template.position+Vector3.forward*delta;
        }
        static void Extend(Transform root,Transform old,string folder)
        {
            var materials=Line4Environment.Palette(folder);
            var g=new SubwayGeometry(materials,folder);
            float end=RunState.Stops.Last()+150;
            var track=old.Find("Standard gauge double track/Track_0000");
            var outside=old.Find("Ansan Line | Open alignment/Wayside_0000");Check(track&&outside,"Existing reusable track");
            for(int z=8580;z<end;z+=40)CopyAt(track,root,z+260,"Track "+z);
            // 80 m trackside modules use shared meshes. Station throats have their own wider opening.
            Transform tube=null;
            for(int z=8620;z<end;z+=80){
                int next=Array.FindIndex(RunState.Stops,s=>s+26>=z+40);if(next<0)next=RunState.Stops.Length-1;
                if(!RouteSchedule.Tunnel(next)){CopyAt(outside,root,z+260,"Wayside "+z);continue;}
                if(tube==null){
                    g.Box(2.65f,-.55f,40,27,.3f,80,0);
                    foreach(float x in new[]{-10.6f,15.9f}){
                        g.Box(x,3.25f,40,.42f,7.9f,80,0);
                        for(int l=4;l<80;l+=12)g.Box(x+(x<0?.24f:-.24f),3.12f,l,.12f,.2f,2.1f,7);
                    }
                    g.Soffit(-10.8f,16.1f,7.4f,0,80,0);
                    g.Flush(root,"TunnelShared");tube=root.Find("TunnelShared");tube.position=Vector3.forward*z;
                }else CopyAt(tube,root,z-tube.position.z,"Tunnel "+z);
            }
            Line4Environment.Stations(g,root,folder,14);
        }
        static void Build()
        {
            File.WriteAllText(Report,"Beginning 51-stop service verification\n");ScoreTests();
            var scene=SceneManager.GetActiveScene();Check(scene.path==Folder+"/Oido_Run.unity","Open Oido_Run");
            var sim=Object.FindFirstObjectByType<Line4Drive>();Check(sim,"Driving controller");
            var old=scene.GetRootGameObjects().First(o=>o.name=="Oido - Geumjeong").transform;
            sim.passengerDoors=DoorCenters(sim);sim.passengerFabric=AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/PassengerFabric.shader");
            Check(sim.passengerFabric && sim.passengerFabric.isSupported,"Passenger shader");
            var root=scene.GetRootGameObjects().FirstOrDefault(o=>o.name=="Geumjeong - Jinjeop");
            if(!root){
                string data=AssetDatabase.GenerateUniqueAssetPath(Folder+"/JinjeopStations");
                AssetDatabase.CreateFolder(Folder,Path.GetFileName(data));root=new GameObject("Geumjeong - Jinjeop");
                try{Extend(root.transform,old,data);}catch{Object.DestroyImmediate(root);throw;}
            }
            for(int i=0;i<51;i++){
                string id=RouteSchedule.Ids[i].ToString();
                var station=(i<14?old:root.transform).Cast<Transform>().Single(t=>t.name.StartsWith(id+" "));
                Check(station.Find("Platform_"+id)&&station.Find("S"+id),"Missing station "+id);
            }
            EditorSceneManager.MarkSceneDirty(scene);Check(EditorSceneManager.SaveScene(scene),"Save");AssetDatabase.SaveAssets();
            File.AppendAllText(Report,"SCENE_OK 51 stations, 40 real platform door centers, score boundaries 0/2/4 m, skip and retry, terminal and replay\n");
            File.WriteAllText(Verify,"run");EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.isPlaying=true;
        }
        static void PlayCheck()
        {
            var sim=Object.FindFirstObjectByType<Line4Drive>();if(!sim||Time.frameCount<frame)return;
            var flow=sim.GetComponent<PassengerFlow>();if(!flow)return;
            if(stage==0){
                Check(sim.Opening && flow.VisibleCount==14,"Opening with waiting passengers");sim.BeginRun();sim.CycleView();
                Check(Mathf.Abs(sim.cabCamera.transform.position.y-3.03f)<.01f,"Raised cab");stage++;frame=Time.frameCount+2;
            }else if(stage==1){
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/Line4_RaisedCab.png"));sim.run.Service();stage++;
            }else if(stage==2 && flow.WalkingCount>0){
                Check(sim.run.Score==25,"Live boarding bonus");stage++;
            }else if(stage==3 && sim.run.Dwell>=3){
                sim.run.Service();stage++;
            }else if(stage==4 && sim.run.Served==0){
                Check(sim.run.Score==350,"Live stop total");
                sim.run.Target=50;sim.run.Served=49;sim.run.Distance=RunState.Stops[50];sim.run.Service();stage++;
            }else if(stage==5 && flow.WalkingCount>0){
                Check(flow.VisibleCount>=14,"Terminal alighting");stage++;
            }else if(stage==6 && sim.run.Dwell>=3){
                sim.run.Service();stage++;
            }else if(stage==7 && sim.run.Complete){
                sim.ReplayOpening();stage++;frame=Time.frameCount+3;
            }else if(stage==8){
                Check(sim.Opening && sim.run.Score==0 && sim.run.Target==0 && sim.consist.position.z==0,"Fresh opening");
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/Line4_Opening.png"));
                File.Delete(Verify);File.AppendAllText(Report,"PLAY_OK title, raised cab, passenger boarding and terminal alighting, live scoring, fresh opening at 0 points.\n");
            }
        }
    }
}
