using System;
using UnityEngine;
using Random = System.Random;

namespace LineFour.Streets
{
    internal sealed partial class StreetMesh
    {
        void Crown(Vector3 center, Vector3 size, int tile, Random rng)
        {
            const int sides=8;
            Vector3[] lower=new Vector3[sides],upper=new Vector3[sides],rim=new Vector3[sides];
            for(int i=0;i<sides;i++){
                float a=i*Mathf.PI*2/sides, radius=.89f+.18f*(float)rng.NextDouble();
                var ring=new Vector3(Mathf.Cos(a)*size.x*radius,0,Mathf.Sin(a)*size.z*radius);
                lower[i]=center+ring*.63f-Vector3.up*size.y*.67f;
                rim[i]=center+ring+Vector3.up*((float)rng.NextDouble()-.5f)*size.y*.48f;
                upper[i]=center+ring*.66f+Vector3.up*size.y*.64f;
            }
            for(int i=0;i<sides;i++){
                int j=(i+1)%sides;
                Face(lower[i],rim[i],rim[j],lower[j],tile);
                Face(rim[i],upper[i],upper[j],rim[j],tile);
                Face(upper[i],center+Vector3.up*size.y,upper[j],upper[j],tile);
                Face(lower[j],center-Vector3.up*size.y,lower[i],lower[i],tile);
            }
        }

        void Tree(Random rng,float x,float z)
        {
            float h=3.8f+(float)rng.NextDouble()*1.3f;
            Solid(x-.59f,-.024f,z-.6f,x+.59f,.055f,z+.6f,4,9);
            Solid(x-.105f,.055f,z-.09f,x+.105f,h-.85f,z+.09f,489,489);
            int leaves=485+rng.Next(4);
            Crown(new Vector3(x,h,z),new Vector3(1.38f,1.49f,1.20f),leaves,rng);
            Crown(new Vector3(x+.68f,h-.48f,z+.16f),new Vector3(.94f,1.0f,.87f),leaves,rng);
            Crown(new Vector3(x-.71f,h-.36f,z-.25f),new Vector3(.84f,1.08f,.91f),leaves,rng);
        }

        void Lamp(float x)
        {
            Solid(x-.18f,0,-4.31f,x+.18f,.19f,-3.97f,4,6);
            Solid(x-.065f,.19f,-4.21f,x+.065f,5.43f,-4.08f,5,6);
            Solid(x-.052f,5.32f,-5.49f,x+.052f,5.42f,-4.08f,5,6);
            Solid(x-.25f,5.22f,-5.86f,x+.25f,5.38f,-5.14f,5,6);
            Face(new(x-.22f,5.217f,-5.18f),new(x-.22f,5.217f,-5.83f),new(x+.22f,5.217f,-5.83f),new(x+.22f,5.217f,-5.18f),12);
        }

        void Car(float x,float z,Random rng)
        {
            int paint=new[]{6,0,14,5,9,1}[rng.Next(6)];
            float y=-.11f;
            Solid(x-2.07f,y+.26f,z-.82f,x+2.08f,y+.81f,z+.82f,paint,paint);
            float l=x-1.27f,r=x+1.10f, t=y+1.48f,b=y+.81f;
            for(int s=-1;s<=1;s+=2){
                float ext=z+s*.79f, top=z+s*.65f;
                Vector3 a=new(l,b,ext),d=new(r,b,ext),c=new(r-.51f,t,top),e=new(l+.32f,t,top);
                if(s<0)Face(a,e,c,d,41);else Face(d,c,e,a,41);
                foreach(float axle in new[]{x-1.36f,x+1.35f}){
                    const int n=10;
                    for(int i=0;i<n;i++){
                        float a1=i*2*Mathf.PI/n,a2=(i+1)*2*Mathf.PI/n;
                        Vector3 Wheel(float ang,float radius)=>new(axle+Mathf.Cos(ang)*radius,y+.34f+Mathf.Sin(ang)*radius,z+s*.844f);
                        var p=Wheel(a1,.31f);var q=Wheel(a2,.31f);var u=Wheel(a1,.14f);var v=Wheel(a2,.14f);
                        if(s<0){Face(q,p,u,v,5);Face(v,u,Wheel(a1,0),Wheel(a1,0),6);}
                        else{Face(p,q,v,u,5);Face(u,v,Wheel(a1,0),Wheel(a1,0),6);}
                    }
                }
            }
            Face(new(l,b,z+.79f),new(l+.32f,t,z+.65f),new(l+.32f,t,z-.65f),new(l,b,z-.79f),41);
            Face(new(r,b,z-.79f),new(r-.51f,t,z-.65f),new(r-.51f,t,z+.65f),new(r,b,z+.79f),41);
            Solid(l+.32f,t-.02f,z-.65f,r-.51f,t+.04f,z+.65f,paint,paint);
            for(int i=-1;i<=1;i+=2)Solid(x+2.08f,y+.52f,z+i*.59f-.14f,x+2.10f,y+.69f,z+i*.59f+.14f,12,12);
        }

        public void Furnish(float length, Random rng,bool rear,bool core)
        {
            Origin(0,0);
            for(float x=-length/2+5;x<length/2-2;x+=17+rng.Next(6)){
                Tree(rng,x,-3.42f);
                if(x+7<length/2-1 && !rear) Lamp(x+7);
            }
            if(!rear && length>33){
                // Small rear courtyards break up the area between the two streets.
                Solid(-length*.46f,-.17f,14,length*.46f,-.12f,36,4,48);
                if(!core){
                for(float stall=-length*.42f;stall<length*.42f;stall+=5.7f){
                    Solid(stall,-.115f,18,stall+.09f,-.098f,21.2f,6,6);
                    if(rng.Next(3)==0)Car(stall+2.6f,19.6f,rng);
                }
                for(float garden=-length*.38f;garden<length*.42f;garden+=19){
                    Solid(garden-2.2f,-.13f,28,garden+2.2f,.035f,31,6,486);
                    Tree(rng,garden,29.5f);
                }
                }
                Car(-length*.24f,-5.94f,rng);
                if(rng.Next(3)!=0)Car(length*.22f,-5.94f,rng);
                for(int i=0;i<6;i++){
                    float x=length/2-3.5f;
                    Solid(x,-.115f,-8.0f+i*.49f,x+2.6f,-.096f,-7.77f+i*.49f,12,12);
                }
                // Kerbstones and a storm drain along the boarding side.
                for(float x=-length/2;x<length/2;x+=3)
                    Solid(x,-.122f,-4.81f,Mathf.Min(x+2.97f,length/2),.01f,-4.65f,6,6);
            }
        }

        public void Blade(float x,int sign)
        {
            Solid(x-.12f,5.5f,-1.49f,x+.12f,5.68f,1.34f,5,5);
            Solid(x-.19f,3.63f,-1.56f,x+.19f,5.48f,-.36f,sign,5);
        }

        public void CityTower(float width,float depth,int floors,Random rng,int sign)
        {
            float l=-width/2,r=width/2;
            Solid(l,0,0,r,5,depth,6,491);
            Panel(l+.25f,.3f,r-.25f,3.6f,-.025f,495+rng.Next(4));
            Panel(l+.4f,3.65f,r-.4f,4.75f,-.035f,sign);
            int glass=115+rng.Next(13), bays=Mathf.Max(3,(int)(width/3.7f));
            for(int i=0;i<floors;i++){
                float y=5+i*2.85f,f=i>floors*.68f?1.9f:.4f;
                float inset=i>floors*.68f?1.05f:.22f;
                Solid(l+inset,y,f,r-inset,y+2.85f,depth,2,6);
                for(int j=0;j<bays;j++){
                    float span=(width-2*inset)/bays,a=l+inset+j*span;
                    Panel(a+.05f,y+.12f,a+span-.05f,y+2.74f,f-.023f,glass);
                }
                float top=y+2.71f;
                Solid(l+inset-.07f,top,f-.16f,r-inset+.07f,top+.14f,depth,6,6);
                foreach(int side in new[]{-1,1}){
                    float x=side<0?l+inset-.015f:r-inset+.015f;
                    float a=side<0?depth-.35f:f+.35f,b=side<0?f+.35f:depth-.35f;
                    Face(new(x,y+.25f,a),new(x,top,a),new(x,top,b),new(x,y+.25f,b),glass);
                }
            }
            float roof=5+floors*2.85f;
            Solid(l+.9f,roof,3,r-.9f,roof+.64f,depth-.3f,2,491);
            Solid(l+2,roof+.6f,depth*.49f,r-2,roof+2.75f,depth-1.4f,6,491);
        }

        public void Apartment(float w,float d,int floors,int finish)
        {
            float l=-w/2,r=w/2,h=floors*2.84f;
            Solid(l,0,0,r,h,d,finish,491);
            int bays=Mathf.Max(3,Mathf.RoundToInt(w/3f));
            for(int i=0;i<floors;i++){
                float y=i*2.84f;
                for(int j=0;j<bays;j++){
                    float a=l+.35f+j*(w-.7f)/bays,b=a+(w-.7f)/bays-.15f;
                    Panel(a,y+.52f,b,y+2.54f,-.052f,115+(j%3)*2);
                }
                Solid(l,y+.35f,-.60f,r,y+.52f,.02f,6,6);
                if(i>0)Solid(l,y+.88f,-.63f,r,y+1.04f,-.55f,14,6);
                for(int side=-1;side<=1;side+=2){
                    float x=side<0?l-.015f:r+.015f;
                    float a=side<0?d-1:1,b=side<0?1:d-1;
                    Face(new(x,y+.8f,a),new(x,y+2.32f,a),new(x,y+2.32f,b),new(x,y+.8f,b),119);
                }
            }
            Solid(l+.42f,0,d*.4f,l+2.6f,h+2.1f,d-.5f,finish,491);
            Solid(l,h,-.1f,r,h+.34f,.15f,6,6);
        }

        public void Railway(bool tunnel)
        {
            if(tunnel){
                foreach(float x in new[]{-10.22f,15.52f}){
                    int side=x<0?1:-1;
                    for(int k=-40;k<40;k+=8){
                        Solid(x-.055f,.01f,k+.07f,x+.055f,6.98f,k+7.95f,493,4);
                        float bracket=x+side*.16f;
                        Solid(bracket-.1f,1.52f,k+1,bracket+.1f,2.39f,k+1.12f,6,6);
                    }
                    foreach(float h in new[]{1.64f,1.92f,2.18f})
                        Solid(x-.24f,h,-40,x+.24f,h+.078f,40,5,5);
                    for(int k=-36;k<40;k+=16){
                        float box=x+side*.31f;
                        Solid(box-.16f,3.48f,k,box+.16f,3.76f,k+2.18f,12,6);
                    }
                }
            }else{
                Solid(-170,-1.39f,-40,170,-.68f,40,4,10);
                for(int k=-40;k<40;k+=5){
                    Solid(-12f,-.68f,k,-3.8f,-.60f,k+5,4,494);
                    Solid(9.12f,-.68f,k,17,-.60f,k+5,4,494);
                }
                foreach(float x in new[]{-12f,17f}){
                    Solid(x-.14f,-.66f,-40,x+.14f,.22f,40,493,6);
                    for(int k=-40;k<40;k+=4)Solid(x-.034f,.22f,k,x+.034f,1.16f,k+.08f,5,6);
                    foreach(float h in new[]{.49f,.82f,1.13f})Solid(x-.035f,h,-40,x+.035f,h+.044f,40,6,6);
                }
                for(int k=-30;k<=10;k+=40){
                    Solid(10.00f,-.66f,k-.30f,10.41f,-.20f,k+.30f,4,6);
                    Solid(10.10f,-.2f,k-.12f,10.30f,7.11f,k+.12f,6,6);
                    Solid(-1.37f,6.66f,k-.08f,10.22f,6.84f,k+.08f,6,6);
                    for(int i=0;i<2;i++){
                        float x=i*5.3f;
                        Solid(x-.04f,5.68f,k-.04f,x+.04f,6.67f,k+.04f,5,5);
                        for(int n=0;n<4;n++)Solid(x-.115f,6.22f+n*.095f,k-.10f,x+.115f,6.27f+n*.095f,k+.10f,9,9);
                    }
                }
                foreach(float x in new[]{0f,5.3f})
                    Solid(x-.018f,5.65f,-40,x+.018f,5.69f,40,5,5);
            }
        }
    }
}
