using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LineFour.Oido;
using Object=UnityEngine.Object;
using OidoRunState=LineFour.Oido.RunState;

namespace LineFour.Streets {
public static class RouteUpgradeCheck {
    public static void Run(){
        foreach(var guid in AssetDatabase.FindAssets("t:Model")){
            string path=AssetDatabase.GUIDToAssetPath(guid);
            if(!path.StartsWith("Assets/Blender/"))continue;
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer && !importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
        }
        var scene=EditorSceneManager.OpenScene("Assets/Scene/Oido_Run.unity");
        foreach(var name in new[]{"MetroLoop","Rain","Next00","Next50","Closing"})
            Check(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/RouteAudio/"+name+(name=="Rain"?".ogg":".wav")),"Missing audio "+name);
        SessionState.SetInt("RouteCheck",1);
        File.WriteAllText("Library/RouteUpgradeCheck.txt","Testing curves, lights, weather and audio.");
        EditorApplication.isPlaying=true;
    }
    static int frame;
    [InitializeOnLoadMethod]static void Init(){EditorApplication.update+=Tick;}
    static void Check(bool pass,string message){if(!pass)throw new InvalidDataException(message);}

    public static void CheckTrackGeometry(){
        CurveRoute.ResetPath();
        var path=CurveRoute.Path;
        float stop=path.TravelAt(RouteSchedule.Stops[1]);
        Check(Mathf.Abs(stop-1454)<4,"OpenStreetMap stop-to-stop distance");
        Check(Mathf.Abs(Quaternion.Angle(path.AtTravel(0).Rotation,path.AtTravel(stop).Rotation)-89.2f)<.5f,"OpenStreetMap approach headings");
        Check(path.FirstCurveStart>260 && path.FirstCurveEnd<1250,"Mapped confines of the Oido curve");
        foreach(bool parallel in new[]{false,true}){
            TrackMesh.SleeperRange(path,480,570,parallel,out int first,out int edge);
            TrackMesh.SleeperRange(path,570,660,parallel,out int next,out int last);
            Check(edge==next && first<edge && next<last,"Sleeper chunk seams");
            for(int i=first;i<last-1;i++){
                var a=path.AtTravel(i*path.SleeperSpacing,parallel);
                var b=path.AtTravel((i+1)*path.SleeperSpacing,parallel);
                Check(Mathf.Abs(Vector3.Distance(a.Position,b.Position)-.6f)<.006f,"Sleeper pitch on each track");
                Check(Mathf.Abs(Vector3.Dot(a.Rotation*Vector3.right,(b.Position-a.Position).normalized))<.025f,"Sleeper normal on the bend");
            }
        }
        var straight=new TrackPath(new[]{Vector3.zero,Vector3.forward*620});
        var mesh=TrackMesh.Build(straight,0,9,out _);
        try{
            var points=mesh.vertices;
            var heads=mesh.GetTriangles(2).Select(i=>points[i])
                .Where(v=>Mathf.Abs(v.z)<.001f && Mathf.Abs(v.y-TrackMesh.RailTop)<.001f && Mathf.Abs(v.x)<1)
                .Select(v=>v.x).Distinct().OrderBy(x=>x).ToArray();
            Check(heads.Length==4 && Mathf.Abs(heads[2]-heads[1]-1.435f)<.0001f,"Inside gauge measured on mesh");
            Check(mesh.GetTriangles(2).Any(i=>Mathf.Abs(points[i].y-TrackMesh.RailSeat)<.0001f) &&
                Mathf.Abs(mesh.GetTriangles(3).Max(i=>points[i].y)-TrackMesh.RailSeat)<.0001f,"Rail foot and sleeper surface share height");
        }finally{Object.DestroyImmediate(mesh);}

        var grade=new TrackPath(new[]{Vector3.zero,new Vector3(0,0,100),new Vector3(-60,5,200),
            new Vector3(-160,10,250),new Vector3(-300,10,250)});
        var graded=grade.AtTravel(145);
        Check(graded.Position.y>0 && (graded.Rotation*Vector3.forward).y>.01f,"Track frame follows grade");
        var left=graded.Point(-TrackMesh.Gauge*.5f,TrackMesh.RailTop);
        var right=graded.Point(TrackMesh.Gauge*.5f,TrackMesh.RailTop);
        Check(Mathf.Abs(Vector3.Distance(left,right)-1.435f)<.0001f,"Gauge stays constant on a grade");
        Check(Quaternion.Angle(grade.AtTravel(0).Rotation,grade.AtTravel(grade.FirstLength).Rotation)>89.9f,"Smooth right-angle turn");
    }

    public static void CheckMappedTrack(){
        var path=CurveRoute.Path;
        Check(path.HasMappedTrack,"Mapped continuation is missing");
        var stops=RouteSchedule.Stops;
        Check(Mathf.Abs(path.TravelAt(stops[50])-86100)<100,"Map-based end-to-end distance");
        Check(Mathf.Abs(path.TravelAt(stops[2])-path.TravelAt(stops[1])-2767)<15,"Jeongwang to Singiloncheon");
        Check(Mathf.Abs(path.TravelAt(stops[49])-path.TravelAt(stops[48])-7876)<20,"Byeollae Byeolgaram to Onam");
        for(int i=1;i<stops.Length;i++){
            float travel=path.TravelAt(stops[i]);
            float before=path.TravelAt(stops[i]-.25f);
            float after=path.TravelAt(stops[i]+.25f);
            Check(Mathf.Abs(travel-before-.25f)<.03f && Mathf.Abs(after-travel-.25f)<.03f,
                "Unit-scale stop position at "+RouteSchedule.Names[i]);
            var frame=path.Frame(stops[i]);
            Check(Vector3.Distance(frame.Position,path.AtTravel(travel).Position)<.025f,"Track meets the stop marker");
            Check(Vector3.Distance(frame.Position,path.AtTravel(travel-205).Position-frame.Rotation*Vector3.back*205)<2,
                "Usable platform approach at "+RouteSchedule.Names[i]);
            for(int point=1;point<10;point++){
                float source=Mathf.Lerp(stops[i-1],stops[i],point*.1f);
                Check(Mathf.Abs(path.SourceAt(path.TravelAt(source))-source)<.025f,"Authored-distance round trip");
            }
        }
        float straight=(stops[5]+stops[6])*.5f;
        Check(!CurveRoute.TryCurveLimit(straight,out _,out _),"No imposed curve limit on Gojan–Jungang straight");
        var late=new OidoRunState {Served=49,Target=50,Distance=stops[50]-12,Speed=.125f};
        float initial=late.Remaining;
        for(int frame=0;frame<120;frame++)
            late.Step(1f/240,false,false,false);
        Check(initial-late.Remaining>.04f,"Low-speed movement at long chainage");
    }

    static void Tick(){
        int stage=SessionState.GetInt("RouteCheck",0);
        if(stage==0||!EditorApplication.isPlaying||Time.frameCount<Math.Max(25,frame))return;
        try{
            var sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
            var weather=sim.GetComponent<RouteAtmosphere>();
            if(stage==1){
                Check(sim.GetComponent<CurveRoute>() && weather && sim.GetComponent<RouteAudio>(),"Runtime components");
                CheckTrackGeometry();
                CheckMappedTrack();
                foreach(var s in RouteSchedule.Stops){
                    var across=CurveRoute.Map(new Vector3(5.3f,0,s))-CurveRoute.Map(new Vector3(0,0,s));
                    Check(Vector3.Distance(across,CurveRoute.Heading(s)*Vector3.right*5.3f)<.01f,"Station alignment");
                }
                Check(Quaternion.Angle(CurveRoute.Heading(0),CurveRoute.Heading(RouteSchedule.Stops[1]))>60,"Oido to Jeongwang turning angle");
                var speedCheck=new OidoRunState{Served=0,Target=1,Distance=0,Speed=OidoRunState.MaxSpeed};
                var track=CurveRoute.Path;
                float restrictedEnd=track.FirstCurveEnd+205.2f;
                float travel=0;
                bool approachBraking=false;
                for(int step=0;step<5000 && travel<restrictedEnd+15;step++){
                    speedCheck.Step(.05f,true,false,false);
                    travel=track.TravelAt(speedCheck.Distance);
                    if(travel<track.FirstCurveStart && speedCheck.CurveBraking)approachBraking=true;
                    if(travel>=track.FirstCurveStart && travel<=restrictedEnd)
                        Check(speedCheck.Speed*3.6f<=40.01f,"Oido curve limit across the whole train");
                }
                Check(approachBraking && travel>=restrictedEnd+15 && speedCheck.Speed*3.6f>40,"Curve approach braking and speed release");
                Check(RouteAtmosphere.SelectWeather(.64f)==RouteWeather.Clear &&
                    RouteAtmosphere.SelectWeather(.70f)==RouteWeather.Overcast &&
                    RouteAtmosphere.SelectWeather(.9f)==RouteWeather.Rain,"Weighted weather");
                sim.BeginRun();sim.run.Served=0;sim.run.Target=1;sim.run.Distance=300;sim.run.Speed=10;
                while(sim.View!=0)sim.CycleView();frame=Time.frameCount+10;
            }else if(stage==2){
                var front=sim.doorCars[0].GetComponentInParent<LODGroup>().transform;
                Check(Mathf.Abs(front.position.x)>3,"Cars following curved path");
                Check(sim.GetComponents<AudioSource>().Any(a=>a.isPlaying&&a.clip&&a.clip.name=="MetroLoop"),"Rolling audio");
                weather.Apply(150,RouteWeather.Clear,30);Check(weather.Daylight>.95f,"Day");
                Shot(sim.cabCamera,"CurvedRouteDay");
                weather.Apply(295,RouteWeather.Clear,30);Shot(sim.cabCamera,"CurvedRouteSunset");
                weather.Apply(450,RouteWeather.Rain,30);Check(weather.Daylight<.02f&&weather.RainAmount>.9f,"Night rain");
                Shot(sim.cabCamera,"CurvedRouteNight");
                while(sim.View!=1)sim.CycleView();
                weather.Apply(450,RouteWeather.Rain,30);
                Shot(sim.cabCamera,"NightCab");
                while(sim.View!=1)sim.CycleView();
                sim.run.Distance=470;sim.run.Speed=0;frame=Time.frameCount+20;
            }else{
                Check(sim.GetComponent<RouteAudio>().SpokenStations>=1,"Station announcements");
                Shot(sim.cabCamera,"CurvedCab");
                File.AppendAllText("Library/RouteUpgradeCheck.txt","PASS 50 mapped intervals; 51 platform approaches; articulated train; loaded and playing audio; 65/20/15 weather; day 150s, sunset 295s, night/rain 450s.");
                SessionState.SetInt("RouteCheck",0);EditorApplication.Exit(0);return;
            }
            SessionState.SetInt("RouteCheck",stage+1);
        }catch(Exception e){File.AppendAllText("Library/RouteUpgradeCheck.txt",e.ToString());Debug.LogException(e);SessionState.SetInt("RouteCheck",0);EditorApplication.Exit(1);}
    }
    static void Shot(Camera camera,string name){
        var rt=RenderTexture.GetTemporary(1100,700,24);var prev=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(1100,700,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1100,700),0,0);tex.Apply();File.WriteAllBytes("Library/"+name+".png",tex.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);
    }
}}
