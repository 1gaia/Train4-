using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LineFour.Streets
{
    internal static class RailwayScenery
    {
        static Mesh Module(bool tunnel)
        {
            string name=tunnel?"TunnelServices":"OpenWayside";
            var builder=new StreetMesh();builder.Railway(tunnel);
            var mesh=builder.Finish(name);
            string path="Assets/StationShops/"+name+".asset";
            var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!stored){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            stored.Clear();stored.vertices=mesh.vertices;stored.normals=mesh.normals;
            stored.uv=mesh.uv;stored.triangles=mesh.triangles;stored.UploadMeshData(false);
            EditorUtility.SetDirty(stored);Object.DestroyImmediate(mesh);return stored;
        }

        static Material RailMaterial(string name,string texture,float density)
        {
            string path="Assets/StationShops/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){
                material=new Material(Shader.Find("LineFour/Rail surface"));
                AssetDatabase.CreateAsset(material,path);
            }
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/StationShops/"+texture+".png");
            material.SetFloat("_Density",density);EditorUtility.SetDirty(material);
            return material;
        }

        public static void Build(Scene scene,Transform parent,Material material)
        {
            var modules=new[]{Module(false),Module(true)};
            var route=new GameObject("철도 시설").transform;
            route.SetParent(parent,false);
            int[] counts={0,0};
            var ballast=RailMaterial("Ballast aggregate","RailGravel",.94f);
            var timber=RailMaterial("Sleeper timber","SleeperGrain",1.6f);
            int tracks=0;
            foreach(var root in scene.GetRootGameObjects().Where(r=>r.name=="Oido - Geumjeong" || r.name=="Geumjeong - Jinjeop")){
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)){
                    if(renderer.name.StartsWith("Track_",StringComparison.Ordinal)||renderer.name.StartsWith("Track ",StringComparison.Ordinal)){
                        var slots=renderer.sharedMaterials;
                        bool gravel=false,wood=false;
                        for(int i=0;i<slots.Length;i++){
                            if(slots[i].name.StartsWith("Ballast",StringComparison.OrdinalIgnoreCase)){slots[i]=ballast;gravel=true;}
                            if(slots[i].name.StartsWith("Sleeper",StringComparison.OrdinalIgnoreCase)){slots[i]=timber;wood=true;}
                        }
                        if(gravel&&wood){renderer.sharedMaterials=slots;tracks++;}
                    }
                    bool open=renderer.name.StartsWith("Wayside_",StringComparison.Ordinal) || renderer.name.StartsWith("Wayside ",StringComparison.Ordinal);
                    bool tube=renderer.name=="TunnelShared" || renderer.name.StartsWith("Tunnel ",StringComparison.Ordinal);
                    if(!open && !tube)continue;
                    float z=renderer.bounds.center.z;
                    var obj=new GameObject(tube?"Tunnel equipment":"Open railway",typeof(MeshFilter),typeof(MeshRenderer));
                    obj.transform.SetParent(route,false);obj.transform.position=Vector3.forward*z;
                    obj.GetComponent<MeshFilter>().sharedMesh=modules[tube?1:0];
                    var mr=obj.GetComponent<MeshRenderer>();mr.sharedMaterial=material;mr.shadowCastingMode=ShadowCastingMode.Off;
                    if(open)renderer.enabled=false;
                    counts[tube?1:0]++;
                }
            }
            if(counts.Min()==0 || tracks==0)throw new InvalidOperationException("Existing railway modules unavailable");
            var sun=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(light=>light.type==LightType.Directional);
            if(sun){
                sun.shadows=LightShadows.Hard;sun.shadowStrength=.57f;sun.shadowBias=.065f;
                sun.shadowNormalBias=.24f;sun.shadowCustomResolution=1024;
                sun.color=new Color(1f,.95f,.86f);sun.intensity=1.02f;
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.66f,.74f,.83f);
            RenderSettings.ambientEquatorColor=new Color(.51f,.54f,.55f);
            RenderSettings.ambientGroundColor=new Color(.39f,.38f,.35f);
        }
    }
}
