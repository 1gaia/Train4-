using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

namespace LineFour.Oido.EditorTools
{
    internal sealed class SubwayGeometry
    {
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector3> normals=new List<Vector3>();
        readonly Dictionary<int,List<int>> faces=new Dictionary<int,List<int>>();
        readonly Material[] paints;
        readonly string meshFolder;
        static readonly Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward};
        public SubwayGeometry(Material[] materials,string directory) {paints=materials;meshFolder=directory;}
        public void Box(float x,float y,float z,float sx,float sy,float sz,int mat, Quaternion? rotation=null)
        {
            var center=new Vector3(x,y,z);var half=new Vector3(sx,sy,sz)*.5f;
            if(!faces.TryGetValue(mat,out var triangles)) faces.Add(mat,triangles=new List<int>());
            for(int i=0;i<3;i++) for(int sign=-1;sign<=1;sign+=2)
            {
                var q=rotation??Quaternion.identity;
                var n=q*(axes[i]*sign); var origin=center+n*half[i];
                var u=q*Vector3.Scale(axes[(i+1)%3],half);var v=q*Vector3.Scale(axes[(i+2)%3],half)*sign;
                int first=vertices.Count;
                vertices.Add(origin-u-v);vertices.Add(origin+u-v);vertices.Add(origin+u+v);vertices.Add(origin-u+v);
                for(int j=0;j<4;j++) normals.Add(n);
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
                triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
            }
        }
        public void Beam(Vector3 a,Vector3 b,float width,float depth,int mat)
        {
            Vector3 mid=(a+b)*.5f;
            Box(mid.x,mid.y,mid.z,width,depth,(b-a).magnitude,mat,Quaternion.FromToRotation(Vector3.forward,(b-a).normalized));
        }
        public void Soffit(float left,float right,float y,float start,float end,int mat)
        {
            if(!faces.TryGetValue(mat,out var tris))faces.Add(mat,tris=new List<int>());
            int n=vertices.Count;
            vertices.AddRange(new[]{new Vector3(left,y,start),new Vector3(right,y,start),new Vector3(right,y,end),new Vector3(left,y,end)});
            for(int i=0;i<4;i++)normals.Add(Vector3.down);
            tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        public void Flush(Transform parent,string label)
        {
            if(vertices.Count==0)return;
            var mesh=new Mesh {name=label,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);
            int[] slots=faces.Keys.OrderBy(k=>k).ToArray();mesh.subMeshCount=slots.Length;
            for(int i=0;i<slots.Length;i++) mesh.SetTriangles(faces[slots[i]],i,false);
            mesh.RecalculateBounds();
            var reusable=(label.StartsWith("Track_")||label.StartsWith("Wayside_"))?
                AssetDatabase.LoadAssetAtPath<Mesh>("Assets/OidoLine4/RouteAssets/"+label+".asset"):null;
            if(reusable) { UnityEngine.Object.DestroyImmediate(mesh); mesh=reusable; }
            else AssetDatabase.CreateAsset(mesh,meshFolder+"/"+label+".asset");
            var g=new GameObject(label,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);
            g.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=g.GetComponent<MeshRenderer>();renderer.sharedMaterials=slots.Select(i=>paints[i]).ToArray();
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            vertices.Clear();normals.Clear();faces.Clear();
        }
    }

    internal static class Line4Environment
    {
        // Concrete, track bed, steel, sleepers, ivory tiles, line blue, tactile yellow,
        // luminaire, charcoal trim, platform surface, wood seat, safety signal green.
        static readonly string[] Colors={"47565D","393B40","A7B1B7","655853","D5DEDA","008FC1",
            "EFB94A","FFEDC1","192D38","A6B0AF","A8794E","5AFFA0","86966E","677F56","97ADBA","B6A9A1",
            "A4634D","367B88","647B95","BF9963","687D73","6C5264","3F6677","798F94"};
        static readonly string[] Names={"Concrete","Ballast","Rail steel","Sleepers","Tile","Line 4","Tactile",
            "Warm lighting","Enamel","Platform","Bench","Signal","Verge","Tree canopy","Factory cladding","Housing",
            "Terracotta","Petrol blue","Slate","Ochre","Sage","Plum","Facade glass","Roof seam"};
        public static Material[] Palette(string folder)
        {
            var result=new Material[Colors.Length];
            for(int i=0;i<result.Length;i++)
            {
                ColorUtility.TryParseHtmlString("#"+Colors[i],out var color);
                var m=new Material(Shader.Find("Standard")) {name=Names[i],color=color};
                m.SetFloat("_Glossiness",i==2?.55f:.19f);
                if(i==2)m.SetFloat("_Metallic",.46f);
                if(i==7||i==11) {m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.85f);}
                AssetDatabase.CreateAsset(m,folder+"/"+Names[i]+".mat");result[i]=m;
            }
            return result;
        }
        static Transform Group(string name,Transform parent)
        {
            var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;
        }
        static void Sign(Transform parent,string text,Vector3 point, float yaw,float height)
        {
            var g=new GameObject(text,typeof(TextMesh));g.transform.SetParent(parent,false);
            g.transform.SetPositionAndRotation(point,Quaternion.Euler(0,yaw,0));
            var tm=g.GetComponent<TextMesh>();tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;
            tm.fontSize=72;tm.characterSize=height/6;tm.color=Color.white;
            g.GetComponent<MeshRenderer>().sharedMaterial=tm.font.material;
        }
        public static void Build(Transform root,string folder)
        {
            var g=new SubwayGeometry(Palette(folder),folder);
            var tracks=Group("Standard gauge double track",root);
            float routeEnd=RunState.Stops[RunState.Stops.Length-1]+110;
            for(int from=-260;from<routeEnd;from+=40)
            {
                float z=from+20;
                g.Box(2.65f,-.48f,z,12.6f,.36f,40,0);
                foreach(float x in new[]{0f,5.3f})
                {
                    g.Box(x,-.22f,z,3.22f,.22f,40,1);
                    for(float sleeper=from+.34f;sleeper<from+40;sleeper+=.68f)
                        g.Box(x,-.02f,sleeper,2.43f,.17f,.23f,3);
                    foreach(float rail in new[]{x-.75f,x+.75f})
                    {
                        g.Box(rail,.085f,z,.143f,.04f,40,2);
                        g.Box(rail,.145f,z,.035f,.085f,40,2);
                        g.Box(rail,.205f,z,.065f,.045f,40,2);
                    }
                }
                g.Flush(tracks,"Track_"+(from+260).ToString("0000"));
            }
            var landscape=Group("Ansan Line | Open alignment",root);
            for(int from=-260;from<routeEnd;from+=80)
            {
                float z=from+40;
                g.Box(0,-1.07f,z,340,.8f,80,12);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side<0?-12:17;
                    g.Box(x,.24f,z,.23f,2.10f,80,0);
                    for(int j=0;j<4;j++) {
                        float site=from+7+j*18, loc=x+side*(4+j%2*3);
                        g.Box(loc,1.05f,site,.36f,3.2f,.36f,10);
                        g.Box(loc,3.55f,site,3.7f,3.2f,4.3f,13);
                    }
                    int block=(from+260)/80;
                    float h=block%3==0?15:6+(block%4)*2;
                    float facade=x+side*(31+block%3*4);
                    g.Box(facade,h/2-.7f,z,17,h,26,block%2==0?14:15);
                    for(int floor=2;floor<h;floor+=3)g.Box(facade-side*8.53f,floor,z,.07f,.9f,23,8);
                }
                for(int j=0;j<2;j++) {
                    float pole=from+j*40+10;
                    g.Box(10.2f,3.35f,pole,.20f,7.6f,.2f,2);
                    g.Box(4.45f,6.75f,pole,11.6f,.16f,.18f,2);
                }
                foreach(float wire in new[]{0f,5.3f})g.Box(wire,5.65f,z,.031f,.04f,80,8);
                g.Flush(landscape,"Wayside_"+(from+260).ToString("0000"));
            }
            Stations(g,root,folder,0);
        }
        public static void Stations(SubwayGeometry g,Transform root,string folder,int first)
        {
            var signs=new StationDetails(folder);
            for(int i=first;i<RunState.Stops.Length;i++)
            {
                int id=RouteSchedule.Ids[i];bool tunnel=RouteSchedule.Tunnel(i);
                float stop=RunState.Stops[i],center=stop-93;
                var station=Group(id+" "+RouteSchedule.English[i].ToUpperInvariant(),root);
                if(tunnel)g.Soffit(-10,15.4f,7.45f,stop-212,stop+26,0);
                foreach(int side in new[]{-1,1})
                {
                    float track=side<0?0:5.3f;
                    float edge=track+side*1.64f,wall=track+side*9.1f;
                    g.Box(edge+side*3.72f,.5f,center,7.44f,1.48f,238,9);
                    g.Box(edge+side*.31f,1.25f,center,.42f,.035f,238,6);
                    g.Box(edge+side*.04f,1.259f,center,.07f,.02f,238,4);
                    if(tunnel)g.Box(wall,4.0f,center,.33f,7.3f,238,4);
                    else signs.Canopy(g,track,side,center,i);
                    g.Box(wall,2.05f,center,.26f,2.15f,238,4);
                    g.Box(wall-side*.15f,2.85f,center,.07f,.29f,238,5);
                    g.Box(wall-side*.15f,1.43f,center,.07f,.22f,238,8);
                    g.Box(track+side*3.18f,5.77f,center,.32f,.13f,237,7);
                    for(float z=stop-206;z<stop+24;z+=12)
                    {
                        float col=track+side*6.05f;
                        g.Box(col,3.63f,z,.54f,4.78f,.70f,4);
                        g.Box(col,1.56f,z,.58f,.64f,.74f,8);
                        g.Box(col,3.15f,z,.57f,.23f,.73f,5);
                        g.Box(wall-side*.14f,3.74f,z,.035f,4.8f,.035f,0);
                        g.Box(col,5.9f,z,6.25f,.3f,.4f,9);
                    }
                    for(float z=stop-184;z<stop+24;z+=40)
                    {
                        float x=track+side*6.75f;
                        g.Box(x,1.75f,z,.58f,.14f,3,10);
                        g.Box(x+side*.25f,2.01f,z,.12f,.55f,3,10);
                        foreach(float dz in new[]{-.96f,.96f})g.Box(x,1.5f,z+dz,.4f,.48f,.15f,8);
                        float bx=wall-side*.21f;
                        g.Box(bx,3.57f,z,.16f,1.18f,9.4f,8);
                        signs.Panel(station,"S"+id,new Vector3(bx-side*.093f,3.56f,z),side<0?-90:90,4.3f,1.08f);
                    }
                    signs.Platform(g,station,track,side,stop,i);
                }
                g.Box(-1.93f,1.91f,stop,.09f,1.4f,.09f,2);
                g.Box(-1.93f,2.66f,stop,.47f,.43f,.1f,6);
                foreach(int direction in new[]{-1,1})
                    signs.Panel(station,"Stop10",new Vector3(-1.93f,2.66f,stop+direction*.057f),direction<0?0:180,.45f,.39f);
                g.Flush(station,"Platform_"+id);
            }
        }
    }
}
