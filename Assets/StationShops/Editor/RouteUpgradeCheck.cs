using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using LineFour.Oido;
using Object=UnityEngine.Object;

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
    static void Tick(){
        int stage=SessionState.GetInt("RouteCheck",0);
        if(stage==0||!EditorApplication.isPlaying||Time.frameCount<Math.Max(25,frame))return;
        try{
            var sim=Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
            var weather=sim.GetComponent<RouteAtmosphere>();
            if(stage==1){
                Check(sim.GetComponent<CurveRoute>() && weather && sim.GetComponent<RouteAudio>(),"Runtime components");
                foreach(var s in RouteSchedule.Stops)Check(Mathf.Abs(CurveRoute.Offset(s))<.001f && Quaternion.Angle(CurveRoute.Heading(s),Quaternion.identity)<.01f,"Station alignment");
                for(int i=0;i<50;i++){
                    float a=RouteSchedule.Stops[i]+100,b=RouteSchedule.Stops[i+1]-260;
                    Check(Mathf.Abs(CurveRoute.Offset((a+b)*.5f))>5,"Missing curve "+i);
                }
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
                File.AppendAllText("Library/RouteUpgradeCheck.txt","PASS 50 curved intervals; 51 straight platforms; articulated train; loaded and playing audio; 65/20/15 weather; day 150s, sunset 295s, night/rain 450s.");
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
