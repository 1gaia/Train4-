using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Streets
{
    internal enum Architecture { Market, Brick, Terrace, Office, Arcade, Workshop, Studio }

    // Coordinates run along the street (x), up (y), and into the lot (z).
    internal sealed partial class StreetMesh
    {
        readonly List<Vector3> vertices = new();
        readonly List<Vector2> uvs = new();
        readonly List<int> indices = new();
        Vector3 offset;
        public void Origin(float x, float z) => offset = new Vector3(x, 0, z);

        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int tile)
        {
            int start = vertices.Count;
            vertices.Add(a + offset); vertices.Add(b + offset);
            vertices.Add(c + offset); vertices.Add(d + offset);
            float u = (tile % 16) / 16f, v = 1 - (tile / 16 + 1) / 32f;
            const float pad = .00065f;
            uvs.Add(new Vector2(u + pad, v + pad)); uvs.Add(new Vector2(u + pad, v + .03125f - pad));
            uvs.Add(new Vector2(u + .0625f - pad, v + .03125f - pad)); uvs.Add(new Vector2(u + .0625f - pad, v + pad));
            indices.Add(start); indices.Add(start+1); indices.Add(start+2);
            indices.Add(start); indices.Add(start+2); indices.Add(start+3);
        }

        void Panel(float l, float b, float r, float t, float z, int tile) =>
            Face(new(l,b,z), new(l,t,z), new(r,t,z), new(r,b,z), tile);

        // Omit underside faces of solids that sit on another solid or the pavement.
        void Solid(float l,float b,float f,float r,float t,float k,int wall,int roof)
        {
            Panel(l,b,r,t,f,wall);
            Face(new(r,b,k),new(r,t,k),new(l,t,k),new(l,b,k),wall);
            Face(new(l,b,k),new(l,t,k),new(l,t,f),new(l,b,f),wall);
            Face(new(r,b,f),new(r,t,f),new(r,t,k),new(r,b,k),wall);
            Face(new(l,t,f),new(l,t,k),new(r,t,k),new(r,t,f),roof);
        }

        public void StreetGround(float begin,float end,bool elevated)
        {
            // One continuous roadbed under the lots, side streets and both ranks.
            float bottom=elevated?-8.45f:-.31f;
            Solid(begin,bottom,-8.85f,end,-.17f,96,4,48);
        }

        public void Pavement(float length)
        {
            Origin(0,0);
            Solid(-length/2, -.175f, -4.75f, length/2, -.025f, 12.4f, 4, 490);
            Solid(-length/2, -.175f,-8.3f,length/2,-.12f,-4.75f,4,48);
            for(float x=-length/2+2;x<length/2-2;x+=8)
                Solid(x,-.12f,-7.98f,x+3,-.10f,-7.90f,12,12);
        }

        public void Building(System.Random rng, Architecture form, float width, float depth,
                             int floors, int wall, int shop, int otherShop, bool distant)
        {
            float l=-width/2, r=width/2, floor=2.65f+(float)rng.NextDouble()*.8f;
            float crown=3.65f+(floors-1)*floor;
            float inset=form==Architecture.Arcade?1.45f:0;
            int trim = new[]{6,9,5,12}[rng.Next(4)];
            Solid(l,0,inset,r,3.65f,depth,wall,trim);
            int storefronts=width<9.8f?1:width>16.5f?3:2;
            for(int j=0;j<storefronts;j++)
            {
                float a=l+.16f+j*(width-.32f)/storefronts;
                float b=l+.16f+(j+1)*(width-.32f)/storefronts-.13f;
                int sign=j==0?shop:j==1?otherShop:16+rng.Next(16);
                Panel(a,.22f,b,2.59f,inset-.02f,495+rng.Next(4));
                float door=Mathf.Lerp(a,b,.62f);
                Solid(door,.12f,inset-.10f,door+.08f,2.53f,inset,5,6);
                Solid(b-.19f,.1f,inset-.10f,b-.08f,2.58f,inset,6,6);
                Panel(door+.22f,.81f,door+.29f,1.41f,inset-.115f,12);
                Solid(a,2.67f,inset-.24f,b,3.54f,inset+.02f,trim,trim);
                Panel(a+.04f,2.72f,b-.04f,3.49f,inset-.255f,sign);
                if(form==Architecture.Arcade)
                    Solid(a,0,-.18f,a+.29f,3.61f,.18f,wall,trim);
                if(form==Architecture.Market || (!distant && rng.Next(4)==0))
                {
                    int color = new[]{9,10,14,1}[rng.Next(4)];
                    for(int s=0;s<6;s++)
                    {
                        float x=Mathf.Lerp(a,b,s/6f), y=Mathf.Lerp(a,b,(s+1)/6f);
                        int cloth=s%2==0?color:12;
                        Face(new(x,2.27f,-1.2f),new(x,2.67f,-.17f),new(y,2.67f,-.17f),new(y,2.27f,-1.2f),cloth);
                        Panel(x,2.10f,y,2.27f,-1.20f,cloth);
                    }
                }
            }

            float front=0, left=l, right=r;
            int bays=Mathf.Clamp(Mathf.RoundToInt(width/(2.2f+(float)rng.NextDouble()*1.8f)),2,7);
            int glass=115+rng.Next(13);
            for(int level=1;level<floors;level++)
            {
                float y=3.65f+(level-1)*floor;
                if(form==Architecture.Terrace && (level==floors-1 || level==2))
                {
                    front+=1.14f; right-=.43f;
                    Solid(left,y,front-1.2f,right,y+.37f,front-.99f,trim,trim);
                }
                if(form==Architecture.Studio && level==floors-1){left+=width*.19f;front+=.65f;}
                if(form==Architecture.Arcade && level==1) front=-.42f;
                Solid(left,y,front,right,y+floor,depth,wall,9);
                float step=(right-left-.50f)/bays;
                float sill=form==Architecture.Office?.12f:form==Architecture.Brick?.70f:.42f;
                for(int j=0;j<bays;j++)
                {
                    float a=left+.25f+j*step, b=a+step-(form==Architecture.Office?.055f:.22f);
                    float f=front-.03f;
                    if(form==Architecture.Studio && j%2==0)
                    {
                        f-=.49f;
                        Solid(a-.04f,y+.15f,f,b+.04f,y+floor-.12f,front+.01f,trim,trim);
                    }
                    Panel(a,y+sill,b,y+floor-.19f,f-.012f,Mathf.Min(127,glass+(level+j)%2));
                }
                if(form==Architecture.Office || form==Architecture.Brick)
                    Solid(left-.04f,y,front-.17f,right+.04f,y+.14f,depth+.04f,trim,trim);
                for(int side=-1;side<=1;side+=2)
                {
                    float x=side<0?left-.015f:right+.015f;
                    for(int j=0;j<2;j++){
                        float z=front+1+j*(depth-front)*.46f, end=z+1.65f;
                        float a=side<0?end:z, b=side<0?z:end;
                        Face(new(x,y+.68f,a),new(x,y+floor-.42f,a),new(x,y+floor-.42f,b),new(x,y+.68f,b),glass);
                    }
                }
            }
            if(form==Architecture.Workshop || form==Architecture.Market)
            {
                float ridge=crown+.85f+(float)rng.NextDouble()*1.45f;
                float mid=(depth+front)/2;
                Face(new(left,crown,front-.24f),new(left,ridge,mid),new(right,ridge,mid),new(right,crown,front-.24f),491);
                Face(new(left,ridge,mid),new(left,crown,depth+.22f),new(right,crown,depth+.22f),new(right,ridge,mid),491);
                Face(new(left,crown,depth),new(left,ridge,mid),new(left,crown,front),new(left,crown,mid),wall);
                Face(new(right,crown,front),new(right,ridge,mid),new(right,crown,depth),new(right,crown,mid),wall);
            }
            else
            {
                Solid(left,crown,front,right,crown+.33f,front+.17f,wall,trim);
                Solid(left,crown,depth-.17f,right,crown+.33f,depth,wall,trim);
                Solid(left,crown,front+.17f,left+.18f,crown+.33f,depth-.17f,wall,trim);
                Solid(right-.18f,crown,front+.17f,right,crown+.33f,depth-.17f,wall,trim);
                float hut=Mathf.Lerp(left+.5f,right-2.8f,(float)rng.NextDouble());
                Solid(hut,crown,depth-2.6f,hut+2.15f,crown+1.65f,depth-.45f,wall,6);
                Panel(hut+.25f,crown+.05f,hut+1.03f,crown+1.49f,depth-2.62f,5);
                float unit=Mathf.Lerp(left+.6f,right-1.4f,(float)rng.NextDouble());
                Solid(unit,crown,front+1.0f,unit+1.12f,crown+.65f,front+2.03f,11,492);
            }
            if(!distant && floors>2 && width>10 && rng.Next(3)!=0)
            {
                Solid(r-.53f,5.1f,-1.35f,r-.36f,5.24f,0,5,5);
                Solid(r-.62f,3.85f,-1.37f,r-.30f,5.13f,-.42f,otherShop,trim);
            }
        }

        public Mesh Finish(string name)
        {
            var mesh=new Mesh { name=name };
            mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }
}
