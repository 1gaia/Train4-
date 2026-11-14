using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LineFour.Oido;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;
using static LineFour.Streets.Architecture;

namespace LineFour.Streets
{
    [InitializeOnLoad]
    internal static class StationShopBuilder
    {
        const string Dir="Assets/StationShops", Blocks=Dir+"/Blocks";
        const string Request="Library/StationShops.request", Report="Library/StationShops.txt";

        // In route order. Each station has two dominant massing types. Infill
        // varies by lot so even adjacent stations with related uses read differently.
        static readonly Architecture[] Main={
            Market,Workshop,Brick,Workshop,Workshop,Studio,Office,Studio,Brick,Market,
            Workshop,Terrace,Arcade,Market,Office,Office,Arcade,Office,Terrace,Market,
            Arcade,Brick,Terrace,Office,Arcade,Terrace,Studio,Office,Brick,Studio,
            Office,Market,Arcade,Studio,Workshop,Market,Studio,Brick,Studio,Terrace,
            Arcade,Brick,Market,Brick,Workshop,Office,Terrace,Market,Terrace,Brick,Market
        };
        static readonly Architecture[] Secondary={
            Brick,Market,Workshop,Arcade,Brick,Office,Arcade,Brick,Market,Workshop,
            Market,Brick,Office,Workshop,Arcade,Studio,Workshop,Studio,Brick,Terrace,
            Market,Market,Brick,Market,Studio,Brick,Terrace,Studio,Workshop,Arcade,
            Brick,Brick,Office,Workshop,Office,Arcade,Arcade,Studio,Market,Brick,
            Office,Workshop,Arcade,Terrace,Market,Arcade,Brick,Studio,Office,Market,Terrace
        };
        static readonly int[][] Palette={
            new[]{1,8,9,0}, new[]{1,1,9,8}, new[]{0,8,10,9}, new[]{6,2,7,0},
            new[]{0,8,1,6}, new[]{14,4,9,6}, new[]{1,0,12,2}
        };
        static StationShopBuilder() => EditorApplication.update+=Poll;
        static void Poll()
        {
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(Request);
            try{Build();}catch(Exception e){File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
        }

        static float Range(Random rng,float a,float b) => a+(b-a)*(float)rng.NextDouble();

        static float Surface(int station) => RouteSchedule.Tunnel(station)?7.52f:-.42f;

        static float Elevation(float z)
        {
            var stops=RouteSchedule.Stops;
            int next=Array.FindIndex(stops,stop=>z<=stop+30);
            if(next<=0)return next==0?Surface(0):Surface(stops.Length-1);
            return Mathf.Lerp(Surface(next-1),Surface(next),(z-stops[next-1]-30)/125f);
        }

        sealed class Neighborhood
        {
            readonly int station;
            readonly Transform parent;
            readonly List<Vector2>[] frontage;
            readonly Material material;
            readonly HashSet<Architecture> forms=new();
            int count, nearCount;
            float leftSpan,rightSpan;
            public int Count=>count;
            public Neighborhood(int station,Transform parent,Material material,List<Vector2>[] frontage)
            {
                this.station=station;this.parent=parent;this.material=material;this.frontage=frontage;
                parent.position=Vector3.forward*RouteSchedule.Stops[station];
            }

            public void Street(int side,bool distant)
            {
                var rng=new Random(RouteSchedule.Ids[station]*1877+(side+2)*67+(distant?7919:0));
                var stops=RouteSchedule.Stops;
                float begin=station==0?-260:(stops[station-1]-stops[station])*.5f;
                float end=station==stops.Length-1?150:(stops[station+1]-stops[station])*.5f;
                int blocks=Mathf.CeilToInt((end-begin)/(distant?101:78));
                float z=begin;
                for(int i=0;i<blocks;i++)
                {
                    float remaining=end-z;
                    float alley=i+1==blocks?0:Range(rng,3.9f,9.7f);
                    float slice=i+1==blocks?remaining:remaining/(blocks-i)*Range(rng,.82f,1.18f);
                    float length=slice-alley;
                    AddBlock(rng,side,distant,i,z+length/2,length);
                    z+=slice;
                }
                if(!distant){
                    if(side<0)leftSpan=end-begin;else rightSpan=end-begin;
                    var road=new StreetMesh();
                    float half=(end-begin)*.5f;
                    for(float start=-half;start<half;start+=20)
                        road.StreetGround(start,Mathf.Min(start+20,half),true);
                    SaveBlock(road,$"{RouteSchedule.Ids[station]}_{(side<0?"W":"E")}_Ground",side,22,(begin+end)*.5f,false);
                }
            }

            void AddBlock(Random rng,int side,bool distant,int block,float center,float length)
            {
                var g=new StreetMesh();
                bool core=Mathf.Abs(center+70)<210;
                g.Pavement(length);
                float x=-length/2;
                int lot=0;
                while(length/2-x>5)
                {
                    var pick=rng.Next(100);
                    var kind=pick<49?Main[station]:pick<82?Secondary[station]:(Architecture)rng.Next(7);
                    float width=kind==Market?Range(rng,7.8f,12):kind==Office||kind==Workshop?Range(rng,13,19.4f):Range(rng,9,15.4f);
                    float spare=length/2-x;
                    if(spare-width<7.4f)width=spare;
                    float gap=Range(rng,.35f,1.5f);
                    float setback=Range(rng,.06f,1.24f)+(kind==Terrace?.42f:0);
                    float depth=distant?Range(rng,8.2f,12.3f):Range(rng,6.4f,8.15f);
                    int floors=kind switch {
                        Market=>rng.Next(1,4),Workshop=>rng.Next(1,3),Office=>rng.Next(4,8),
                        Arcade=>rng.Next(3,6),Terrace=>rng.Next(3,6),_=>rng.Next(2,5)
                    };
                    if(core && !distant)floors+=rng.Next(2,5);
                    if(distant&&kind==Office)floors++;
                    var palette=Palette[(int)kind];
                    int sign=rng.Next(4)==0?16+rng.Next(16):128+rng.Next(357);
                    if(!distant && block<4 && lot==0){
                        int identity=block+(side>0?4:0);
                        sign=identity==0?64+station:128+station*7+identity-1;
                    }
                    g.Origin(x+width/2,setback);
                    if(distant && rng.Next(100)<(Main[station]==Market?18:36))
                        g.Apartment(width-gap,depth,rng.Next(5,12),palette[rng.Next(palette.Length)]);
                    else
                        g.Building(rng,kind,width-gap,depth,floors,palette[rng.Next(palette.Length)],sign,16+rng.Next(16),distant);
                    if(!distant){
                        float middle=parent.position.z+center-side*(x+width/2);
                        frontage[side<0?0:1].Add(new Vector2(middle-(width-gap)/2,middle+(width-gap)/2));
                    }
                    x+=width;count++;lot++;forms.Add(kind);
                    if(!distant)nearCount++;
                }
                g.Furnish(length,rng,distant,core);
                if(core || distant){
                    float start=-length/2+1;
                    while(start<length/2-8){
                        float width=Mathf.Min(Range(rng,13,23),length/2-start);
                        float lane=distant?Range(rng,21,23):Range(rng,22,23);
                        g.Origin(start+width/2,lane);
                        int sign=128+rng.Next(357);
                        if(distant)g.CityTower(width-.8f,Range(rng,12,16),rng.Next(core?12:7,core?24:14),rng,sign);
                        else g.Building(rng,rng.Next(2)==0?Arcade:Studio,width-.65f,12,rng.Next(5,10),Palette[3][rng.Next(4)],sign,16+rng.Next(16),false);
                        count++;start+=width;
                    }
                }
                if(core && !distant){
                    g.Origin(0,0);
                    for(float signX=-length/2+5;signX<length/2-2;signX+=11+rng.Next(7))
                        g.Blade(signX,128+rng.Next(357));
                }
                string name=$"{RouteSchedule.Ids[station]}_{(side<0?"W":"E")}_{(distant?"Rear":"HighSt")}_{block}";
                float lateral=distant?Range(rng,66,70):Range(rng,22.0f,23.55f);
                SaveBlock(g,name,side,lateral,center,distant);
            }

            void SaveBlock(StreetMesh g,string name,int side,float lateral,float center,bool distant)
            {
                float worldZ=parent.position.z+center, surface=Elevation(worldZ);
                Mesh mesh=g.Finish(name);
                // Grades occur only at the open/underground transitions.
                float halfSpan=mesh.bounds.extents.x+1;
                if(!Mathf.Approximately(Elevation(worldZ-halfSpan),Elevation(worldZ+halfSpan))){
                    var points=mesh.vertices;
                    for(int i=0;i<points.Length;i++)
                        points[i].y+=Elevation(worldZ-side*points[i].x)-surface;
                    mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();
                }
                string path=Blocks+"/"+name+".asset";
                var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(stored)
                {
                    stored.Clear();
                    stored.vertices=mesh.vertices;stored.uv=mesh.uv;stored.normals=mesh.normals;
                    stored.triangles=mesh.triangles;stored.bounds=mesh.bounds;
                    stored.UploadMeshData(false);Object.DestroyImmediate(mesh);
                    EditorUtility.SetDirty(stored);
                }
                else{stored=mesh;AssetDatabase.CreateAsset(stored,path);}
                var row=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
                row.transform.SetParent(parent,false);
                row.transform.localPosition=new Vector3((side>0?5.3f:0)+side*lateral,surface,center);
                if(station<4 && !name.EndsWith("_Ground")){
                    if(worldZ+halfSpan>RouteSchedule.Stops[station]-245 && worldZ-halfSpan<RouteSchedule.Stops[station]+48)
                        row.transform.localPosition+=Vector3.right*(side*85);
                }
                row.transform.localRotation=Quaternion.Euler(0,side*90,0);
                row.GetComponent<MeshFilter>().sharedMesh=stored;
                var renderer=row.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                if(distant)renderer.shadowCastingMode=ShadowCastingMode.Off;
            }

            public string Audit()
            {
                if(nearCount<25 || forms.Count<3 || Mathf.Min(leftSpan,rightSpan)<320)throw new InvalidDataException(parent.name+" has insufficient coverage");
                return $"{parent.name}: {count} buildings ({nearCount} frontage), {forms.Count} forms, frontage spans {leftSpan:F0}/{rightSpan:F0} m";
            }
        }

        [MenuItem("Tools/Line 4/Build station shopping streets")]
        internal static void Build()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scene/Oido_Run.unity")throw new InvalidOperationException("Open Oido_Run");
            if(Main.Length!=RouteSchedule.Stops.Length || Secondary.Length!=Main.Length)throw new InvalidDataException("Station profiles");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/District.mat");
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"/DistrictAtlas.png");
            if(!atlas)throw new InvalidDataException("Missing district sign atlas");
            if(!mat){
                mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,Dir+"/District.mat");
            }
            mat.mainTexture=atlas;mat.SetFloat("_Glossiness",.16f);EditorUtility.SetDirty(mat);
            if(!AssetDatabase.IsValidFolder(Blocks))AssetDatabase.CreateFolder(Dir,"Blocks");
            var previous=scene.GetRootGameObjects().FirstOrDefault(x=>x.name=="역세권 상가");
            var root=new GameObject("역세권 상가");
            int total=0;
            var lines=new List<string>();
            var frontage=new[]{new List<Vector2>(),new List<Vector2>()};
            try{
                for(int i=0;i<Main.Length;i++){
                    var district=new GameObject(RouteSchedule.Names[i]);
                    district.transform.SetParent(root.transform,false);
                    var neighborhood=new Neighborhood(i,district.transform,mat,frontage);
                    foreach(int side in new[]{-1,1}){
                        neighborhood.Street(side,false);
                        neighborhood.Street(side,true);
                    }
                    lines.Add(neighborhood.Audit());total+=neighborhood.Count;
                }
                foreach(var side in frontage){
                    var ordered=side.OrderBy(lot=>lot.x).ToArray();
                    float gap=0;
                    for(int i=1;i<ordered.Length;i++)gap=Mathf.Max(gap,ordered[i].x-ordered[i-1].y);
                    if(ordered[0].x>0 || ordered.Last().y<RouteSchedule.Stops.Last() || gap>14)
                        throw new InvalidDataException("Gap in route frontage: "+gap);
                    lines.Add($"Verified {ordered.Length} street-front buildings from {ordered[0].x:F1} to {ordered.Last().y:F1} m; widest opening {gap:F1} m.");
                }
                root.AddComponent<RouteStreetVisibility>();
                RailwayScenery.Build(scene,root.transform,mat);
                AssetDatabase.SaveAssets();
            }catch{Object.DestroyImmediate(root);throw;}
            if(previous)Object.DestroyImmediate(previous);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException("Scene save failed");
            lines.Insert(0,$"Saved {total} buildings at {Main.Length} stations; {root.GetComponentsInChildren<MeshFilter>().Length} block and road meshes.");
            File.WriteAllLines(Report,lines);
            Preview();
        }

        static void Preview()
        {
            var cameraObject=new GameObject("Street review camera",typeof(Camera));
            var camera=cameraObject.GetComponent<Camera>();
            var target=RenderTexture.GetTemporary(720,420,24);
            var old=RenderTexture.active;
            var sheet=new Texture2D(1440,840,TextureFormat.RGB24,false);
            try{
                camera.targetTexture=target;camera.fieldOfView=60;camera.farClipPlane=260;
                int[] sample={0,12,32,49};
                for(int i=0;i<sample.Length;i++){
                    int station=sample[i];float z=(RouteSchedule.Stops[station]+RouteSchedule.Stops[station+1])*.5f;
                    float ground=Elevation(z);
                    if(i==0){
                        camera.transform.position=new Vector3(12,30,z+14);camera.transform.LookAt(new Vector3(0,1.9f,z-18));
                    }else if(i==3){
                        camera.transform.position=new Vector3(.6f,3.03f,z);
                        camera.transform.LookAt(new Vector3(1,2.75f,z+50));
                    }else{
                        camera.transform.position=new Vector3(-16.5f,ground+(i==1?3.5f:13),z);
                        camera.transform.LookAt(new Vector3(-26,ground+4.7f,z-28));
                    }
                    camera.Render();RenderTexture.active=target;
                    sheet.ReadPixels(new Rect(0,0,720,420),(i%2)*720,(1-i/2)*420);
                }
                sheet.Apply();File.WriteAllBytes("Library/Line4ContinuousRoute.png",sheet.EncodeToPNG());
            }finally{
                RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(sheet);Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
