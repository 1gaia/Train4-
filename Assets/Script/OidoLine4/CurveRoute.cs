using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LineFour.Oido
{
    [DefaultExecutionOrder(100)]
    public sealed class CurveRoute : MonoBehaviour
    {
        struct Car { public Transform root; public Vector3 position; public Quaternion rotation; }
        readonly List<Car> cars=new List<Car>();
        readonly List<Mesh> generated=new List<Mesh>();
        Line4Drive drive;
        public static float Offset(float s)
        {
            var stops=RouteSchedule.Stops;
            int i=Array.BinarySearch(stops,s);
            if(i>=0)return 0;
            i=~i-1;
            if(i<0 || i>=stops.Length-1)return 0;
            float a=stops[i]+100,b=stops[i+1]-260;
            if(s<=a || s>=b)return 0;
            float u=(s-a)/(b-a),wave=Mathf.Sin(u*Mathf.PI);
            return (i%2==0?1:-1)*Mathf.Min(24,(b-a)*(b-a)/5000)*wave*wave*wave*wave;
        }
        public static Quaternion Heading(float s) =>
            Quaternion.Euler(0,Mathf.Atan2(Offset(s+.25f)-Offset(s-.25f),.5f)*Mathf.Rad2Deg,0);
        public static Vector3 Map(Vector3 source) =>
            new Vector3(Offset(source.z),source.y,source.z)+Heading(source.z)*new Vector3(source.x,0,0);
        void Start()
        {
            drive=GetComponent<Line4Drive>();
            foreach(var group in drive.consist.GetComponentsInChildren<LODGroup>()){
                var t=group.transform;
                cars.Add(new Car{root=t,position=t.position,rotation=t.rotation});
            }
            cars.Sort((a,b)=>b.position.z.CompareTo(a.position.z));
            foreach(var renderer in drive.consist.GetComponentsInChildren<MeshRenderer>()){
                if(!renderer.name.StartsWith("Console_"))continue;
                var car=renderer.name.Contains("4954")?cars[9]:cars[0];
                renderer.transform.SetParent(car.root,true);
            }
            for(int i=0;i<10;i++){
                var riders=drive.consist.Find("Saloon occupants "+(i+1));
                if(riders)riders.SetParent(cars[i].root,true);
            }
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()){
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
                    if(filter.transform.IsChildOf(drive.consist)||filter.GetComponentInParent<PassengerFlow>())continue;
                    if(!filter.sharedMesh || !filter.sharedMesh.isReadable)continue;
                    var source=filter.sharedMesh;
                    var points=source.vertices;bool changed=false;
                    for(int i=0;i<points.Length;i++){
                        var world=filter.transform.TransformPoint(points[i]);
                        var curved=Map(world);
                        if((world-curved).sqrMagnitude>.000001f)changed=true;
                        points[i]=filter.transform.InverseTransformPoint(curved);
                    }
                    if(!changed)continue;
                    var mesh=Instantiate(source);mesh.name=source.name+" curved";
                    mesh.vertices=points;mesh.RecalculateNormals();mesh.RecalculateBounds();
                    filter.sharedMesh=mesh;generated.Add(mesh);
                }
            }
        }
        void LateUpdate()
        {
            foreach(var car in cars){
                var location=car.position+Vector3.forward*drive.run.Distance;
                car.root.SetPositionAndRotation(Map(location),Heading(location.z)*car.rotation);
            }
            // Boarding cohorts use route coordinates before being mapped to the curve.
            foreach(var t in GetComponent<PassengerFlow>().GetComponentsInChildren<Transform>())
                if(t.parent==drive.transform && t.name.StartsWith("Commuter")){
                    var pos=t.position;var rot=t.rotation;
                    t.SetPositionAndRotation(Map(pos),Heading(pos.z)*rot);
                }
        }
        void OnDestroy(){foreach(var mesh in generated)Destroy(mesh);}
    }
}
