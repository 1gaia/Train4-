using System;
using System.IO;
using System.Linq;
using LineFour.Oido;
using UnityEditor;
using UnityEngine;

namespace LineFour.Streets
{
    [InitializeOnLoad] internal static class StreetRouteCheck
    {
        const string Request="Library/StreetRouteCheck.request", Report="Library/StreetRouteCheck.txt";
        static readonly int[] Stations={0,13,32,49};
        static int sample,firstFrame;
        static double start;
        static bool placed;
        static StreetRouteCheck(){EditorApplication.update+=Update;}
        static void Update()
        {
            if(!File.Exists(Request)||!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount<8)return;
            try{
                var sim=UnityEngine.Object.FindFirstObjectByType<LineFour.Oido.Line4Drive>();
                var scenery=UnityEngine.Object.FindFirstObjectByType<RouteStreetVisibility>();
                if(!sim||!scenery)throw new InvalidDataException("Missing controller or route scenery");
                var stops=RouteSchedule.Stops;
                int i=Stations[sample];
                if(!placed){
                    if(sample==0){
                        File.WriteAllText(Report,$"{SystemInfo.graphicsDeviceName}, Game view {Screen.width} x {Screen.height}\n");
                        sim.BeginRun();
                    }
                    sim.run.Target=i+1;sim.run.Served=i;
                    sim.run.Distance=(stops[i]+stops[i+1])*.5f-35;
                    sim.run.Speed=14;
                    int view=sample>=2?1:0;
                    while(sim.View!=view)sim.CycleView();
                    placed=true;firstFrame=-1;start=Time.realtimeSinceStartupAsDouble;return;
                }
                double elapsed=Time.realtimeSinceStartupAsDouble-start;
                if(firstFrame<0){
                    if(elapsed<1.5)return;
                    start=Time.realtimeSinceStartupAsDouble;firstFrame=Time.frameCount;return;
                }
                if(elapsed<3)return;
                int active=RouteSchedule.Names.Count(n=>scenery.transform.Find(n).gameObject.activeInHierarchy);
                if(active>4 || active<2 || !scenery.transform.Find(RouteSchedule.Names[i]).gameObject.activeInHierarchy ||
                    !scenery.transform.Find(RouteSchedule.Names[i+1]).gameObject.activeInHierarchy)
                    throw new InvalidDataException("Visibility failed at "+i);
                if(sim.run.Distance<=(stops[i]+stops[i+1])*.5f-30)
                    throw new InvalidDataException("Train did not move at "+i);
                File.AppendAllText(Report,$"{RouteSchedule.Names[i]} / {RouteSchedule.Names[i+1]}: {(Time.frameCount-firstFrame)/elapsed:F1} FPS, {active} visible districts, view {sim.View}\n");
                placed=false;sample++;
                if(sample==Stations.Length){File.AppendAllText(Report,"PASS\n");Finish();}
            }catch(Exception e){File.AppendAllText(Report,"FAIL "+e);Finish();}
        }
        static void Finish(){File.Delete(Request);EditorApplication.isPlaying=false;sample=0;placed=false;}
        [MenuItem("Tools/Line 4/Check moving street views")]
        static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.WriteAllText(Request,"test");
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            var game=EditorWindow.focusedWindow;
            if(game && game.GetType().Name=="GameView" && !game.maximized){
                SessionState.SetBool("LineFour.StreetCheck.RestoreGame",true);
                game.maximized=true;
            }
            EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] static void Watch()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("LineFour.StreetCheck.RestoreGame",false))return;
                SessionState.EraseBool("LineFour.StreetCheck.RestoreGame");
                foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    if(window.GetType().Name=="GameView" && window.maximized){window.maximized=false;break;}
            };
            EditorApplication.update+=()=>{
                const string pending="Library/StreetRouteCheck.start";
                if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||!File.Exists(pending))return;
                File.Delete(pending);Begin();
            };
        }
    }
}
