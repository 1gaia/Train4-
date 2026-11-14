using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Oido
{
    // Three cohorts retain the people who just alighted until the train leaves the platform.
    public sealed class PassengerFlow : MonoBehaviour
    {
        const int Capacity = 22;
        sealed class Person { public Transform root; public SkinnedMeshRenderer skin; }
        readonly Person[,] people=new Person[3,Capacity];
        readonly List<Material> clothes=new List<Material>();
        static readonly string[] Models={"casual_jacket_standing","woman_tee_standing","casual_shirt_standing","mechanic_overalls_standing","woman_tee_sitting","veteran_cap_sitting"};
        readonly GameObject[] models=new GameObject[Models.Length];
        float[] doors;
        Transform train;
        public int VisibleCount {get; private set;}
        public int WalkingCount {get; private set;}
        public void Prepare(float[] centers,Transform consist,Shader fabric)
        {
            doors=centers;train=consist;
            for(int i=0;i<Models.Length;i++)models[i]=Resources.Load<GameObject>("Commuters/"+Models[i]);
            var texture=Resources.Load<Texture2D>("Commuters/People");
            foreach(var tint in new[]{"FFFFFF","E4EAF9","EDDAD0","D6E2D9","E5D9E6"}){
                ColorUtility.TryParseHtmlString("#"+tint,out var color);
                var m=new Material(Shader.Find("Standard")){mainTexture=texture,color=color,enableInstancing=true};
                m.SetFloat("_Glossiness",.13f);m.EnableKeyword("_ALPHATEST_ON");m.SetFloat("_Cutoff",.43f);m.renderQueue=2450;
                clothes.Add(m);
            }
            for(int c=0;c<3;c++)for(int j=0;j<Capacity;j++){
                var root=PersonModel((j*3+c)%4,transform,c*Capacity+j);
                people[c,j]=new Person{root=root,skin=root.GetComponentInChildren<SkinnedMeshRenderer>()};
            }
            for(int car=0;car<10;car++){
                var cabin=new GameObject("Saloon occupants "+(car+1)).transform;cabin.SetParent(train,false);
                float center=0;
                for(int d=0;d<4;d++)center+=doors[car*4+d]*.25f;
                cabin.localPosition=Vector3.forward*center;
                for(int j=0;j<15;j++){
                    bool sitting=j<12;
                    int side=j%2==0?-1:1;
                    int bay=j/4;
                    float z=sitting?(bay-1)*4.4f+((j/2)%2==0?-.60f:.61f): (j-13)*4.4f+1.19f;
                    int seed=car*17+j;
                    var rider=PersonModel(sitting?4+(seed/3+car)%2:seed%4,cabin,seed);
                    rider.localPosition=new Vector3(side*(sitting?1.03f:.72f),1.24f,z);
                    rider.localRotation=Quaternion.Euler(0,side*90+(sitting?0:(seed%5-2)*12),0);
                }
            }
        }
        Transform PersonModel(int model,Transform parent,int seed)
        {
            var holder=new GameObject("Commuter "+seed).transform;
            holder.SetParent(parent,false);
            var body=Instantiate(models[model],holder,false);
            holder.localScale=Vector3.one*(.94f+.022f*(seed%6));
            foreach(var renderer in body.GetComponentsInChildren<Renderer>()){
                renderer.sharedMaterial=clothes[seed%clothes.Count];
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return holder;
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
                    if(person.skin && person.skin.sharedMesh.blendShapeCount>=2){
                        person.skin.SetBlendShapeWeight(0,Mathf.Max(0,swing)*4);
                        person.skin.SetBlendShapeWeight(1,Mathf.Max(0,-swing)*4);
                    }
                }
            }
        }
        void OnDestroy(){foreach(var m in clothes)Destroy(m);}
    }
}
