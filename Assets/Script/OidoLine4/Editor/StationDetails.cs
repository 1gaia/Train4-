using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace LineFour.Oido.EditorTools
{
    internal sealed class StationDetails
    {
        readonly string folder;
        readonly Dictionary<string,Material> signs=new Dictionary<string,Material>();
        // Fascia palette and roof sections for the fourteen station art directions.
        static readonly int[] Accent={17,18,20,16,17,21,18,16,20,19,16,20,21,18};
        static readonly int[] Roof={1,3,0,2,3,1,2,0,1,2,0,2,3,1};
        public StationDetails(string assetFolder) {folder=assetFolder;}

        public void Panel(Transform parent,string key,Vector3 pos,float yaw,float width,float height)
        {
            if(!signs.TryGetValue(key,out Material mat)){
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OidoLine4/Signs/"+key+".png");
                if(!texture)throw new System.InvalidOperationException("Station artwork missing: "+key);
                mat=new Material(Shader.Find("Unlit/Texture")) {name=key,mainTexture=texture};
                AssetDatabase.CreateAsset(mat,folder+"/Sign_"+key+".mat");
                signs.Add(key,mat);
            }
            var panel=GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.name=key;panel.transform.SetParent(parent,false);
            panel.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));
            panel.transform.localScale=new Vector3(width,height,1);
            panel.GetComponent<MeshRenderer>().sharedMaterial=mat;
        }
        static float RoofY(int style,float u)
        {
            if(style==0)return 6.45f+.57f*u;
            if(style==1)return 6.35f+.70f*Mathf.Abs(u);
            if(style==2)return 6.4f+1.05f*(1-u*u);
            return 6.38f+.71f*(1-Mathf.Abs(u));
        }
        public void Canopy(SubwayGeometry g,float track,int side,float center,int station)
        {
            int style=Roof[station%Roof.Length],accent=Accent[station%Accent.Length];
            float x=track+side*5.33f;
            const float step=1.2f;
            for(int bay=0;bay<9;bay++){
                float z=center+(bay-4)*22;
                for(int strip=0;strip<6;strip++){
                    float u=-1+strip/3f, v=u+1/3f;
                    float a=RoofY(style,u),b=RoofY(style,v);
                    float pitch=Mathf.Atan2((b-a)*side,step)*Mathf.Rad2Deg;
                    g.Box(x+side*(u+v)*1.8f,(a+b)*.5f,z,Mathf.Sqrt(step*step+(b-a)*(b-a))+.025f,.17f,21.1f,
                        strip==0?accent:14,Quaternion.Euler(0,0,pitch));
                    g.Beam(new Vector3(x+side*u*3.6f,a-.16f,z-8),new Vector3(x+side*v*3.6f,b-.16f,z-8),.14f,.19f,8);
                }
                g.Box(x-side*3.6f,RoofY(style,-1)-.15f,z,.16f,.39f,21.1f,accent);
                foreach(float arm in new[]{-2.5f,2.5f})
                    g.Beam(new Vector3(x,4.45f,z),new Vector3(x+arm,6.25f,z),.16f,.16f,accent);
            }
        }
        public void Platform(SubwayGeometry g,Transform root,float track,int side,float stop,int i)
        {
            int accent=Accent[i%Accent.Length];float hub=track+side*6.08f;
            // A front-facing station name and boarding marks remain visible above the platform furniture.
            float boardZ=stop-9;
            foreach(float dx in new[]{-2.7f,2.7f})g.Box(hub+dx,3.3f,boardZ,.15f,4.25f,.17f,8);
            g.Box(hub,5.08f,boardZ,6.02f,1.61f,.22f,accent);
            foreach(int dir in new[]{-1,1})
                Panel(root,"S"+RouteSchedule.Ids[i],new Vector3(hub,5.08f,boardZ+dir*.12f),dir<0?0:180,5.75f,1.44f);
            for(int car=0;car<10;car++){
                float z=stop-4.1f-car*20.48f;
                foreach(float dz in new[]{-.8f,.8f})
                    g.Box(track+side*2.5f,1.27f,z+dz,.92f,.021f,.095f,5);
            }
            // Passenger amenities along the platform, outside the door clearance.
            for(int bank=0;bank<3;bank++){
                float z=stop-44-bank*64;
                g.Box(hub,2.36f,z,1.0f,2.2f,.71f,accent);
                g.Box(hub-side*.514f,2.55f,z,.043f,.82f,.49f,8);
                g.Box(hub-side*.544f,2.62f,z,.025f,.48f,.4f,14);
                g.Box(hub,1.76f,z+1.7f,.52f,1f,.57f,2);
                g.Box(hub,2.27f,z+1.7f,.46f,.035f,.49f,8);
                g.Box(hub,4.52f,z+3,2.43f,.82f,.18f,8);
                foreach(float dx in new[]{-.87f,.87f})g.Box(hub+dx,5.27f,z+3,.06f,.81f,.06f,2);
                Panel(root,i==13?"Transfer":"Exit",new Vector3(hub,4.52f,z+3.105f),180,2.28f,.70f);
            }
            float outside=track+side*13.3f, hallZ=stop-60-(i%3)*16;
            bool bridge=i==0||i==3||i==4||i==6||i==12||i==13;
            if(bridge){
                // Stair flights land flush with the passenger footbridge.
                float deck=8.1f,landing=hallZ+8;
                for(int step=0;step<34;step++){
                    float rise=.20f*(step+1);
                    g.Box(hub,1.24f+rise*.5f,landing-13.4f+step*.40f,2.12f,rise,.415f,9);
                }
                foreach(float dx in new[]{-1.12f,1.12f})
                    g.Beam(new Vector3(hub+dx,2.35f,landing-13.55f),new Vector3(hub+dx,9.13f,landing),.1f,.10f,2);
                if(side<0){
                    g.Box(2.65f,8.02f,landing+1.3f,19.2f,.32f,3.2f,0);
                    foreach(float dz in new[]{-.2f,2.8f}) {
                        g.Box(2.65f,8.78f,landing+dz,19.1f,1.22f,.12f,22);
                        for(int j=0;j<10;j++)g.Box(-6.86f+j*2.1f,9.2f,landing+dz,.10f,2.2f,.12f,accent);
                    }
                    g.Box(2.65f,10.43f,landing+1.3f,19.5f,.27f,3.6f,accent);
                }
            } else {
                g.Box(outside,3.3f,hallZ,7.2f,6.7f,13,accent);
                g.Box(outside,6.76f,hallZ,8.1f,.33f,14,8);
                for(int j=0;j<5;j++)g.Box(outside-side*3.63f,3.55f,hallZ-5+j*2.4f,.07f,2.7f,1.94f,22);
            }
            // Distinct concourse-side landmarks for a coastal terminus, gallery, campus,
            // greener suburban stops, and the two dense city interchanges.
            if(i==0) {
                for(int fin=0;fin<5;fin++){
                    float z=stop-17-fin*3.6f;
                    g.Beam(new Vector3(outside,1.1f,z),new Vector3(outside+side*2.6f,7.2f+fin*.4f,z+2.3f),.39f,.65f,17);
                }
            } else if(i==2||i==5||i==7||i==8||i==9||i==10||i==11) {
                for(int j=0;j<6;j++){
                    float z=stop-20-24*j;
                    g.Box(hub,1.54f,z,1.24f,.6f,3.1f,i==7?16:9);
                    if(i==5||i==7){g.Box(hub,2.8f,z,.23f,2.1f,1.42f,accent,Quaternion.Euler(18,0,0));}
                    else {g.Box(hub,2.12f,z,.19f,1.16f,.21f,10);
                        for(int crown=0;crown<3;crown++)g.Box(hub,2.72f+crown*.39f,z,1.44f-crown*.35f,.55f,1.83f-crown*.44f,13,Quaternion.Euler(0,28*crown,0));}
                }
            } else {
                for(int j=0;j<3;j++){
                    float z=stop-26-j*19;
                    g.Box(outside,2.6f,z,5.3f,5.2f,8,accent);
                    g.Box(outside-side*2.68f,2.9f,z,.09f,2.3f,7.2f,22);
                    g.Box(outside,5.35f,z,6.1f,.24f,8.9f,8);
                }
            }
        }
    }
}
