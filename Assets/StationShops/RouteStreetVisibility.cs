using UnityEngine;
using LineFour.Oido;

namespace LineFour.Streets
{
    // Districts are stored in schedule order. Keep the approaches in view while
    // the renderer skips the rest of the 33 km route.
    public sealed class RouteStreetVisibility : MonoBehaviour
    {
        Transform[] districts;
        Camera drivingCamera;
        LineFour.Oido.Line4Drive drive;
        float lastZ=float.PositiveInfinity;
        void Start()
        {
            drive=FindAnyObjectByType<LineFour.Oido.Line4Drive>();
            drivingCamera=drive?drive.cabCamera:Camera.main;
            districts=new Transform[RouteSchedule.Stops.Length];
            for(int i=0;i<districts.Length;i++)districts[i]=transform.Find(RouteSchedule.Names[i]);
            LateUpdate();
        }
        void LateUpdate()
        {
            if(!drivingCamera || districts==null)return;
            float z=drive?drive.run.Distance:drivingCamera.transform.position.z;
            var path=drive?CurveRoute.Path:null;
            float distance=path!=null?path.TravelAt(z):z;
            if(Mathf.Abs(lastZ-distance)<30)return;
            lastZ=distance;
            float range=drivingCamera.farClipPlane+140;
            var stops=RouteSchedule.Stops;
            for(int i=0;i<districts.Length;i++){
                if(!districts[i])continue;
                float a=i==0?stops[i]-260:(stops[i-1]+stops[i])*.5f;
                float b=i+1==stops.Length?stops[i]+150:(stops[i]+stops[i+1])*.5f;
                if(path!=null){a=path.TravelAt(a);b=path.TravelAt(b);}
                bool visible=distance+range>=a && distance-range<=b;
                if(districts[i].gameObject.activeSelf!=visible)districts[i].gameObject.SetActive(visible);
            }
        }
    }
}
