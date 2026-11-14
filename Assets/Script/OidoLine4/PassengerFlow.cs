using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Oido
{
    // Three cohorts retain the people who just alighted until the train leaves the platform.
    public sealed class PassengerFlow : MonoBehaviour
    {
        const int Capacity = 22;
        sealed class Person { public Transform root,left,right; }
        Person[,] people=new Person[3,Capacity];
        float[] doors;
        Transform train;
        Material cloth;
        List<Mesh> meshes=new List<Mesh>();
        public int VisibleCount {get; private set;}
        public int WalkingCount {get; private set;}
        public void Prepare(float[] centers,Transform consist,Shader fabric)
        {
            doors=centers;train=consist;
            if(doors==null||doors.Length<4)return;
            cloth=new Material(fabric);
            string[] colors={"356175","AB644D","55634A","C18B47","826583","374C78","9B505C"};
            Mesh leg=Part("Trousers",new[]{new Shape(0,-.33f,0,.145f,.69f,.185f,"343C49"),new Shape(0,-.665f,.046f,.156f,.12f,.25f,"242932")});
            for(int c=0;c<3;c++)for(int j=0;j<Capacity;j++){
                int k=(j+c*3)%colors.Length;
                if(meshes.Count<=1+k){
                    string skin=new[]{"C88968","E2B699","AE795F"}[k%3],coat=colors[k];
                    Part("Commuter "+k,new[]{
                        new Shape(0,1.02f,0,.43f,.66f,.31f,coat),new Shape(0,1.42f,0,.25f,.25f,.23f,skin),
                        new Shape(0,1.56f,.013f,.235f,.315f,.245f,skin),new Shape(0,1.66f,-.02f,.252f,.18f,.253f,k%2==0?"322F30":"503C34"),
                        new Shape(-.227f,1.04f,0,.112f,.53f,.14f,coat),new Shape(.227f,1.04f,0,.112f,.53f,.14f,coat),
                        new Shape(-.23f,.776f,.016f,.094f,.13f,.10f,skin),new Shape(.23f,.776f,.016f,.094f,.13f,.10f,skin),
                        new Shape(0,1.03f,-.164f,.26f,.38f,.14f,k%2==0?"444348":"826548")});
                }
                var p=new Person();p.root=new GameObject("Passenger "+c+"-"+j).transform;p.root.SetParent(transform,false);
                Piece(p.root,meshes[1+k],Vector3.zero);
                p.left=Piece(p.root,leg,new Vector3(-.094f,.735f,0));p.right=Piece(p.root,leg,new Vector3(.094f,.735f,0));
                p.root.localScale=Vector3.one*(.91f+.03f*((c+j)%6));people[c,j]=p;
            }
        }
        struct Shape {
            public Vector3 center,size;public Color color;
            public Shape(float x,float y,float z,float w,float h,float d,string rgb) {
                center=new Vector3(x,y,z);size=new Vector3(w,h,d);ColorUtility.TryParseHtmlString("#"+rgb,out color);
            }
        }
        Mesh Part(string label,Shape[] shapes)
        {
            var verts=new List<Vector3>();var colors=new List<Color>();var tris=new List<int>();
            foreach(var s in shapes){
                int first=verts.Count;
                float[] radius={.56f,.94f,1,.62f};float[] ys={-.5f,-.36f,.29f,.5f};
                for(int row=0;row<4;row++)for(int a=0;a<8;a++){
                    float angle=2*Mathf.PI*a/8;verts.Add(s.center+Vector3.Scale(s.size,new Vector3(.5f*radius[row]*Mathf.Cos(angle),ys[row],.5f*radius[row]*Mathf.Sin(angle))));colors.Add(s.color);
                }
                for(int row=0;row<3;row++)for(int a=0;a<8;a++){
                    int n=first+row*8+a,m=first+row*8+(a+1)%8;
                    tris.AddRange(new[]{n,n+8,m,m,n+8,m+8});
                }
                for(int a=1;a<7;a++)tris.AddRange(new[]{first,first+a,first+a+1,first+24,first+25+a,first+24+a});
            }
            var mesh=new Mesh{name=label};mesh.SetVertices(verts);mesh.SetColors(colors);mesh.SetTriangles(tris,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);return mesh;
        }
        Transform Piece(Transform parent,Mesh mesh,Vector3 pos){
            var g=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=pos;
            g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=cloth;return g.transform;
        }

        static int CrowdAt(int station) => 16 + (station * 5 + 2) % 7;

        public void Display(RunState r)
        {
            VisibleCount = WalkingCount = 0;
            if (doors == null || doors.Length < 4) return;

            float stop = RunState.Stops[r.Target];
            float previous = RunState.Stops[Mathf.Max(0, r.Target - 1)];
            bool nearStop = Mathf.Abs(stop - r.Distance) < 250;
            bool doorsClosing = r.Phase == DoorPhase.Closing || r.Complete;

            for (int cohort = 0; cohort < 3; cohort++)
            {
                int role = (cohort - r.Target % 3 + 3) % 3;
                bool boarding = role == 0;
                bool alreadyExited = role == 1;
                int count;
                if (boarding)
                    count = nearStop && r.Target < RunState.Stops.Length - 1 ? CrowdAt(r.Target) : 0;
                else if (alreadyExited)
                    count = r.Target >= 2 && r.Distance - previous < 235 ? CrowdAt(r.Target - 2) : 0;
                else
                    count = r.Served >= 0 && r.Target > 0 ? CrowdAt(r.Target - 1) : 0;

                for (int j = 0; j < Capacity; j++)
                {
                    Person person = people[cohort, j];
                    bool visible = j < count;
                    if (person.root.gameObject.activeSelf != visible) person.root.gameObject.SetActive(visible);
                    if (!visible) continue;
                    VisibleCount++;

                    float doorZ = doors[Mathf.RoundToInt(j * (doors.Length - 1f) / (count - 1))];
                    float offset = (j % 3 - 1) * .18f;
                    float trainZ = train.position.z + doorZ;
                    var inside = new Vector3(-.64f, 1.40f, trainZ + .6f);
                    var doorway = new Vector3(-1.51f, 1.30f, trainZ);
                    var wait = new Vector3(-3.08f - .17f * (j % 3), 1.265f, stop + doorZ + .48f + offset);
                    var exit = new Vector3(-4.1f - .20f * (j % 4), 1.265f, (alreadyExited ? previous : stop) + doorZ - 1.15f + offset);

                    // Let alighting passengers clear the doors before the waiting group boards.
                    float delay = (boarding ? 1.4f : 0) + .045f * (j % 5);
                    float duration = (boarding ? 1.22f : 1.04f) + .04f * (j % 4);
                    float progress = alreadyExited || doorsClosing ? 1 :
                        r.Phase == DoorPhase.Boarding ? Mathf.Clamp01((r.Dwell - delay) / duration) : 0;
                    Vector3 start = boarding ? wait : inside;
                    Vector3 end = boarding ? inside : exit;
                    Vector3 position = progress < .5f ? Vector3.Lerp(start, doorway, progress * 2) :
                        Vector3.Lerp(doorway, end, progress * 2 - 1);

                    bool walking = progress > 0 && progress < 1;
                    float swing = 0;
                    float heading = boarding ? 90 : -90;
                    if (walking)
                    {
                        WalkingCount++;
                        swing = Mathf.Sin(progress * Mathf.PI * (boarding ? 6 : 8)) * 23;
                        Vector3 direction = Vector3.Lerp(doorway - start, end - doorway, progress);
                        heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    }
                    else if (boarding && progress == 0)
                    {
                        heading += Mathf.Sin(Time.time * .8f + j * 1.7f) * 8;
                    }

                    position.y += Mathf.Abs(swing) * .0008f;
                    person.root.SetPositionAndRotation(position, Quaternion.Euler(0, heading, 0));
                    person.left.localRotation = Quaternion.Euler(swing, 0, 0);
                    person.right.localRotation = Quaternion.Euler(-swing, 0, 0);
                }
            }
        }
        void OnDestroy(){foreach(var mesh in meshes)Destroy(mesh);if(cloth)Destroy(cloth);}
    }
}
