using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

namespace LineFour.EditorTools
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
        public void Box(float x,float y,float z,float sx,float sy,float sz,int mat)
        {
            var center=new Vector3(x,y,z);var half=new Vector3(sx,sy,sz)*.5f;
            if(!faces.TryGetValue(mat,out var triangles)) faces.Add(mat,triangles=new List<int>());
            for(int i=0;i<3;i++) for(int sign=-1;sign<=1;sign+=2)
            {
                var n=axes[i]*sign; var origin=center+Vector3.Scale(n,half);
                var u=Vector3.Scale(axes[(i+1)%3],half);var v=Vector3.Scale(axes[(i+2)%3],half)*sign;
                int first=vertices.Count;
                vertices.Add(origin-u-v);vertices.Add(origin+u-v);vertices.Add(origin+u+v);vertices.Add(origin-u+v);
                for(int j=0;j<4;j++) normals.Add(n);
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
                triangles.Add(first);triangles.Add(first+2);triangles.Add(first+3);
            }
        }
        public void Flush(Transform parent,string label)
        {
            if(vertices.Count==0)return;
            var mesh=new Mesh {name=label,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);
            int[] slots=faces.Keys.OrderBy(k=>k).ToArray();mesh.subMeshCount=slots.Length;
            for(int i=0;i<slots.Length;i++) mesh.SetTriangles(faces[slots[i]],i,false);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,meshFolder+"/"+label+".asset");
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
            "EFB94A","FFEDC1","192D38","A6B0AF","A8794E","5AFFA0"};
        static readonly string[] Names={"Concrete","Ballast","Rail steel","Sleepers","Tile","Line 4","Tactile",
            "Warm lighting","Enamel","Platform","Bench","Signal"};
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
            var module = new RailwayModule();
            var g=new SubwayGeometry(Palette(folder),folder);
            var tracks=Group("Standard gauge double track",root);
            for (int from = -260; from < 1220; from += RailwayModule.Length)
                module.AddSection(tracks, from);
            var tunnel=Group("Tunnel and service walkways",root);
            float start=-260;
            foreach(float stop in RunState.Stops)
            {
                Tube(g,tunnel,start,stop-212); start=stop+26;
            }
            Tube(g,tunnel,start,1220);
            string[] stationNames={"HYEHWA","DONGDAEMUN","DONGDAEMUN HISTORY & CULTURE PARK"};
            for(int i=0;i<RunState.Stops.Length;i++)
            {
                float stop=RunState.Stops[i],center=stop-93;
                var station=Group((420+i)+" "+stationNames[i],root);
                g.Box(2.65f,6.33f,center,24.8f,.45f,238,0);
                foreach(int side in new[]{-1,1})
                {
                    float track=side<0?0:5.3f;
                    float edge=track+side*1.64f,wall=track+side*9.1f;
                    g.Box(edge+side*3.72f,.5f,center,7.44f,1.48f,238,9);
                    g.Box(edge+side*.31f,1.25f,center,.42f,.035f,238,6);
                    g.Box(edge+side*.04f,1.259f,center,.07f,.02f,238,4);
                    g.Box(wall,3.66f,center,.26f,5.05f,238,4);
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
                        Sign(station,(420+i)+"   /   "+stationNames[i],new Vector3(bx-side*.093f,3.56f,z),side<0?-90:90,.25f);
                    }
                }
                g.Box(-1.93f,1.91f,stop,.09f,1.4f,.09f,2);
                g.Box(-1.93f,2.66f,stop,.47f,.43f,.1f,6);
                Sign(station,"10",new Vector3(-1.93f,2.66f,stop-.06f),0,.20f);
                g.Flush(station,"Platform_"+(420+i));
            }
        }
        static void Tube(SubwayGeometry g,Transform parent,float from,float to)
        {
            while(from<to)
            {
                float len=Mathf.Min(50,to-from),z=from+len/2;
                g.Box(2.65f,5.92f,z,15.4f,.40f,len,0);
                foreach(float x in new[]{-4.95f,10.25f})
                {
                    int inward=x<0?1:-1;
                    g.Box(x,2.68f,z,.3f,6.1f,len,0);
                    g.Box(x+inward*.6f,.48f,z,1.24f,.37f,len,9);
                    g.Box(x+inward*.22f,1.54f,z,.12f,.11f,len,8);
                    for(float at=from+2;at<to && at<from+len;at+=8)
                    {
                        g.Box(x+inward*.19f,2.75f,at,.12f,.19f,1.85f,7);
                        g.Box(x+inward*.22f,3.05f,at+1,.18f,5.64f,.21f,8);
                    }
                }
                if(len>30) {
                    g.Box(2.53f,1.75f,z,.12f,2.9f,.12f,2);
                    g.Box(2.53f,3.1f,z,.44f,.83f,.25f,8);
                    g.Box(2.53f,3.26f,z-.14f,.17f,.17f,.04f,11);
                }
                g.Flush(parent,"Tunnel_"+Mathf.RoundToInt(from+260).ToString("0000")); from+=len;
            }
        }
    }
}
