using UnityEngine;

namespace LineFour
{
    public sealed class RunState
    {
        public static readonly float[] Stops = { 0, 540, 1130 };
        public static readonly string[] Names = { "혜화", "동대문", "동대문역사문화공원" };
        public float Distance, Speed, Dwell;
        public int Target = 1, Served;
        public bool Doors, Complete;
        public bool InZone => Mathf.Abs(Stops[Target] - Distance) <= 4;
        public bool Stopped => Speed < 0.01f;

        public void Step(float dt, bool power, bool brake, bool emergency)
        {
            if (Complete) return;
            if (Doors) { Dwell += dt; return; }
            float previous = Speed;
            float acceleration = emergency ? -2.15f : brake ? -1.23f :
                power ? Mathf.Lerp(1.02f, 0.48f, Speed / 18) : -0.09f;
            Speed = Mathf.Clamp(Speed + acceleration * dt, 0, 18);
            Distance += (previous + Speed) * 0.5f * dt;
            if (Distance >= Stops[Target] + 26) { Distance = Stops[Target] + 26; Speed = 0; }
        }

        public void Service()
        {
            if (Complete || !Stopped || !InZone) return;
            if (!Doors) { Doors = true; Dwell = 0; return; }
            if (Dwell < 3) return;
            Doors = false;
            Served = Target;
            Complete = Target == Stops.Length - 1;
            if (!Complete) Target++;
        }

        public void RestartSection()
        {
            if (Complete) { Served = 0; Target = 1; }
            Distance = Stops[Served]; Speed = 0; Dwell = 0; Doors = false; Complete = false;
        }
    }

    public sealed class Line4Drive : MonoBehaviour
    {
        public Transform consist;
        public Camera cabCamera;
        public float cabEyeZ = -1.7f;
        public readonly RunState run = new RunState();
        public int View { get; private set; }
        private Vector3 origin;
        private Font font;
        private GUIStyle style;
        private AudioSource rolling, chime;
        private bool power, brake, emergency;
        private static readonly Color Ink = new Color(.035f,.09f,.13f,.95f);
        private static readonly Color Blue = new Color(.18f,.78f,.96f);
        private static readonly Color Pale = new Color(.72f,.83f,.87f);

        void Start()
        {
            origin = consist.position;
            font = Font.CreateDynamicFontFromOSFont(new[] {"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"}, 24);
            rolling = gameObject.AddComponent<AudioSource>(); chime = gameObject.AddComponent<AudioSource>();
            rolling.clip = MakeSound(false); rolling.loop = true; rolling.volume=0; rolling.Play();
            chime.clip = MakeSound(true); chime.volume=.3f;
            UpdateCamera();
        }

        AudioClip MakeSound(bool bell)
        {
            const int rate = 22050;
            float[] samples = new float[rate];
            for (int i=0;i<rate;i++)
            {
                float t=(float)i/rate;
                if (bell) {
                    float local=t<.35f?t:t-.35f, hz=t<.35f?660:880;
                    samples[i]=.4f*Mathf.Sin(2*Mathf.PI*hz*local)*Mathf.Exp(-7*local)*Mathf.Clamp01(local*140);
                } else {
                    samples[i]=.34f*Mathf.Sin(2*Mathf.PI*68*t)+.12f*Mathf.Sin(2*Mathf.PI*136*t)
                        +.11f*Mathf.Sin(2*Mathf.PI*343*t)*Mathf.Pow(.5f+.5f*Mathf.Cos(2*Mathf.PI*4*t),12);
                }
            }
            var clip=AudioClip.Create(bell?"Platform chime":"Traction loop", rate,1,rate,false);
            clip.SetData(samples,0); return clip;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.C)) View=(View+1)%3;
            if (Input.GetKeyDown(KeyCode.R)) run.RestartSection();
            if (Input.GetKeyDown(KeyCode.E)) {
                bool was=run.Doors; run.Service(); if(was!=run.Doors) chime.Play();
            }
            power=Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow);
            brake=Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow);
            emergency=Input.GetKey(KeyCode.Space);
            run.Step(Time.deltaTime,power,brake,emergency);
            consist.position=origin+Vector3.forward*run.Distance;
            rolling.volume=.17f*run.Speed/18; rolling.pitch=.48f+run.Speed*.08f;
            UpdateCamera();
        }

        void UpdateCamera()
        {
            Vector3 nose=Vector3.forward*run.Distance;
            Vector3 eye, aim;
            if (View==1) { eye=new Vector3(.57f,2.73f,cabEyeZ); aim=eye+new Vector3(0,-.035f,1)*25; }
            else if (View==2) { eye=new Vector3(0,2.52f,-5.4f); aim=eye+new Vector3(0,-.02f,-1)*12; }
            else { eye=new Vector3(-4.65f,2.8f,9.5f); aim=new Vector3(0,1.9f,-3.5f); }
            cabCamera.transform.SetPositionAndRotation(nose+eye,Quaternion.LookRotation(aim-eye));
        }

        void RectColor(float x,float y,float w,float h,Color c)
        {
            GUI.color=c; GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture); GUI.color=Color.white;
        }
        void Label(float x,float y,float w,float h,string msg,int size,Color color,bool bold=false)
        {
            style.fontSize=size; style.fontStyle=bold?FontStyle.Bold:FontStyle.Normal; style.normal.textColor=color;
            GUI.Label(new Rect(x,y,w,h),msg,style);
        }
        void OnGUI()
        {
            if (Event.current.type!=EventType.Repaint) return;
            if(style==null) style=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleLeft,padding=new RectOffset()};
            float s=Mathf.Min(Screen.width/1200f,Screen.height/680f), w=Screen.width/s, h=Screen.height/s;
            Matrix4x4 saved=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));
            float remaining=RunState.Stops[run.Target]-run.Distance;
            RectColor(24,22,262,64,Ink); RectColor(24,22,5,64,Blue);
            Label(43,24,220,22,"SEOUL METRO   /   LINE 04",13,Blue,true);
            Label(43,44,225,34,"4호선  ·  하행 운전",22,Color.white,true);
            RectColor(w-360,22,336,64,Ink);
            Label(w-343,25,108,22,run.Complete?"운행 종료":"다음 정차역",13,Blue,true);
            Label(w-343,44,225,31,RunState.Names[run.Target],21,Color.white,true);
            Label(w-120,43,93,30,Mathf.RoundToInt(Mathf.Abs(remaining))+" m",19,Pale);
            string action = run.Complete?"운행 완료   ·   R  다시 운전":run.Doors?
                (run.Dwell<3?"승하차 중   "+Mathf.CeilToInt(3-run.Dwell)+"초":"E  문 닫고 승하차 완료"):
                remaining < -4?"정차 위치 통과   ·   R  구간 재시작":
                run.InZone && run.Stopped?"정위치 정차   ·   E  문 열기":
                remaining <= run.Speed*run.Speed/2.46f+12?"정차 지점 접근   ·   S  제동":
                run.Distance<2?"W  길게 눌러 출발":"정차 표지 ±4 m 이내에 정차";
            RectColor(24,h-112,433,88,Ink); RectColor(24,h-112,433,3,Blue);
            Label(43,h-101,83,55,Mathf.RoundToInt(run.Speed*3.6f).ToString("00"),38,Color.white,true);
            Label(104,h-92,73,34,"km/h",15,Pale);
            Label(178,h-103,260,34,action,15,Color.white,true);
            Label(178,h-70,250,26,emergency?"비상 제동":run.Doors?"출입문 열림":brake?"제동":power?"가속":"타행 / 대기",14,Blue);
            RectColor(43,h-44,113,4,new Color(.23f,.32f,.37f)); RectColor(43,h-44,113*run.Speed/18,4,Blue);
            RectColor(w-442,h-97,418,73,Ink);
            Label(w-425,h-94,397,31,"W 가속     S 제동     Space 비상 제동     E 승하차",15,Color.white,true);
            Label(w-425,h-61,397,26,"C 시점  ·  "+new[]{"선두 외부","운전석","객실"}[View]+"       R 구간 재시작",15,Pale);
            GUI.matrix=saved;
        }
    }
}
