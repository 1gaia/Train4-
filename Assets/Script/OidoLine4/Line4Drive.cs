using UnityEngine;

namespace LineFour.Oido
{
    public enum DoorPhase { Closed, Opening, Boarding, Closing }
    public sealed class RunState
    {
        public static readonly float[] Stops = RouteSchedule.Stops;
        public static readonly string[] Names = RouteSchedule.Names;
        public const float Tolerance=4;
        public int Score, Misses, LastAccuracy;
        public float LastError;
        public string Receipt="첫 승하차 대기";
        public bool Missed;
        public float Distance, Speed, Dwell, DoorTravel;
        public int Target, Served = -1;
        public DoorPhase Phase;
        public bool Doors => Phase!=DoorPhase.Closed;
        public bool Complete;
        public bool InZone => Mathf.Abs(Stops[Target] - Distance) <= Tolerance;
        public bool Stopped => Speed < 0.01f;

        public void Step(float dt, bool power, bool brake, bool emergency)
        {
            if (Complete || Missed) return;
            if (Phase==DoorPhase.Opening) {
                DoorTravel=Mathf.Min(1,DoorTravel+dt/1.6f);
                if(DoorTravel>=1) { Phase=DoorPhase.Boarding; Score+=25;Receipt=Names[Target]+"  문 열기 +25"; }
                return;
            }
            if (Phase==DoorPhase.Boarding) { Dwell+=dt; return; }
            if (Phase==DoorPhase.Closing) {
                DoorTravel=Mathf.Max(0,DoorTravel-dt/1.8f);
                if(DoorTravel==0) {
                    Phase=DoorPhase.Closed; Served=Target;
                    LastError=Distance-Stops[Target];
                    LastAccuracy=Mathf.RoundToInt(100*Mathf.Clamp01(1-Mathf.Abs(LastError)/Tolerance));
                    Score+=225+LastAccuracy;
                    Receipt=Names[Target]+"  승하차 +200  ·  정차 +"+LastAccuracy+"  ·  닫기 +25";
                    Complete=Target==Stops.Length-1;
                    if(!Complete) Target++;
                }
                return;
            }
            if (Served<0 || (Stopped && InZone)) {Speed=0;return;}
            float previous = Speed;
            float acceleration = emergency ? -2.15f : brake ? -1.23f :
                power ? Mathf.Lerp(1.02f, 0.48f, Speed / 18) : -0.09f;
            Speed = Mathf.Clamp(Speed + acceleration * dt, 0, 18);
            Distance += (previous + Speed) * 0.5f * dt;
            if (Distance > Stops[Target]+Tolerance) {
                Score-=100;Misses++;Missed=true;Speed=0;Receipt=Names[Target]+"  무정차 통과 −100";
            }
        }

        public void Service()
        {
            if (Complete || Missed || !Stopped || !InZone) return;
            if (Phase==DoorPhase.Closed) { Phase=DoorPhase.Opening; Dwell=0; Speed=0; }
            else if (Phase==DoorPhase.Boarding && Dwell>=3) Phase=DoorPhase.Closing;
        }

        public void RestartSection()
        {
            if(Doors)return;
            if (Complete) { Served=-1; Score=Misses=LastAccuracy=0;LastError=0;Receipt="첫 승하차 대기"; }
            Target=Served+1; Distance=Stops[Mathf.Max(0,Served)]; Speed=0;
            if(Missed) Distance=Stops[Target]-24;
            Missed=false;
            DoorTravel=0; Dwell=0; Phase=DoorPhase.Closed; Complete=false;
        }
    }

    public sealed class Line4Drive : MonoBehaviour
    {
        const float CabEyeHeight = 2.89f;

        public Transform consist;
        public Camera cabCamera;
        public SkinnedMeshRenderer[] doorCars;
        public int[] platformShapes;
        public float cabEyeZ = -1.88f;
        public float[] passengerDoors;
        public Shader passengerFabric;
        [SerializeField] PlatformScreenDoors oidoScreenDoors;
        PassengerFlow passengers;
        public bool Opening { get; private set; }=true;
        float titleTime;
        public readonly RunState run = new RunState();
        public int View { get; private set; }
        private Vector3 origin;
        LODGroup[] carLods;
        private Font font;
        private GUIStyle style;
        private AudioSource rolling, chime;
        private bool power, brake, emergency;
        private float yaw,pitch,lastDoorWeight=-1;
        private static readonly Color Ink = new Color(.035f,.09f,.13f,.95f);
        private static readonly Color Blue = new Color(.18f,.78f,.96f);
        private static readonly Color Pale = new Color(.72f,.83f,.87f);

        void Start()
        {
            origin = consist.position;
            carLods=consist.GetComponentsInChildren<LODGroup>();
            font = Font.CreateDynamicFontFromOSFont(new[] {"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"}, 24);
            rolling = gameObject.AddComponent<AudioSource>(); chime = gameObject.AddComponent<AudioSource>();
            rolling.clip = MakeSound(false); rolling.loop = true; rolling.volume=0; rolling.Play();
            chime.clip = MakeSound(true); chime.volume=.3f;
            passengers=gameObject.AddComponent<PassengerFlow>();
            passengers.Prepare(passengerDoors,consist,passengerFabric);
            UpdateCamera();
        }
        public void BeginRun()
        {
            Opening = false;
            View = 1;
            yaw = pitch = 0;
            UpdateCamera();
        }
        public void ReplayOpening()
        {
            if(!run.Complete)return;
            run.RestartSection();consist.position=origin;Opening=true;View=0;yaw=pitch=titleTime=0;
            ReleaseMouse();passengers.Display(run);
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
            if(Opening) {
                titleTime+=Time.deltaTime;
                Vector3 eye=Vector3.Lerp(new Vector3(-6.8f,3.5f,6),new Vector3(-5.6f,3.25f,4),Mathf.PingPong(titleTime*.07f,1));
                cabCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(new Vector3(0,2,-4)-eye));
                float k=Mathf.Min(Screen.width/1200f,Screen.height/680f);
                Vector2 mouse=new Vector2(Input.mousePosition.x/k,(Screen.height-Input.mousePosition.y)/k);
                if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)||
                    (Input.GetMouseButtonDown(0)&&StartButton(Screen.height/k).Contains(mouse)))BeginRun();
                passengers.Display(run); return;
            }
            if (Input.GetKeyDown(KeyCode.C)) CycleView();
            if (Input.GetKeyDown(KeyCode.F)) {yaw=pitch=0;orbitDistance=42;}
            if (Input.GetMouseButtonDown(1)) { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
            if (!Input.GetMouseButton(1) || Input.GetKeyDown(KeyCode.Escape)) ReleaseMouse();
            else if (Cursor.lockState==CursorLockMode.Locked) {
                TurnView(Input.GetAxisRaw("Mouse X")*2.8f,-Input.GetAxisRaw("Mouse Y")*2.8f);
            }
            if (Input.GetKeyDown(KeyCode.R)) {
                if(run.Complete){ReplayOpening();return;}
                run.RestartSection();
            }
            if (Input.GetKeyDown(KeyCode.E)) {
                var was=run.Phase; run.Service(); if(was!=run.Phase) chime.Play();
            }
            if(View==2)Walk(new Vector2((Input.GetKey(KeyCode.D)?1:0)-(Input.GetKey(KeyCode.A)?1:0),
                (Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0)),Time.deltaTime);
            if(View==0)orbitDistance=Mathf.Clamp(orbitDistance-Input.mouseScrollDelta.y*3,14,92);
            power=(View!=2 && Input.GetKey(KeyCode.W))||Input.GetKey(KeyCode.UpArrow);
            brake=(View!=2 && Input.GetKey(KeyCode.S))||Input.GetKey(KeyCode.DownArrow);
            emergency=Input.GetKey(KeyCode.Space);
            run.Step(Time.deltaTime,power,brake,emergency);
            float weight=100*Mathf.SmoothStep(0,1,run.DoorTravel);
            if(weight!=lastDoorWeight) {
                for(int i=0;i<doorCars.Length;i++) doorCars[i].SetBlendShapeWeight(platformShapes[i],weight);
                if (oidoScreenDoors) oidoScreenDoors.SetOpening(run.Target == 0 ? weight / 100f : 0);
                lastDoorWeight=weight;
            }
            consist.position=origin+Vector3.forward*run.Distance;
            passengers.Display(run);
            rolling.volume=.17f*run.Speed/18; rolling.pitch=.48f+run.Speed*.08f;
            UpdateCamera();
        }

        Vector3 saloonEye=new Vector3(0,2.82f,-5.4f);
        float orbitDistance=42;
        void Walk(Vector2 input,float dt)
        {
            Vector3 delta=Quaternion.Euler(0,180+yaw,0)*new Vector3(input.x,0,input.y);
            saloonEye+=Vector3.ClampMagnitude(delta,1)*dt*2.3f;
            saloonEye.z=Mathf.Clamp(saloonEye.z,-201.1f,-3.3f);
            float boundary=Mathf.Abs(Mathf.Repeat(-saloonEye.z+.24f,20.48f)-10.24f);
            float halfWidth=boundary>8.8f?.35f:.48f;
            saloonEye.x=Mathf.Clamp(saloonEye.x,-halfWidth,halfWidth);
        }

        void UpdateCamera()
        {
            Vector3 nose=consist.position;
            if(carLods!=null)foreach(var group in carLods){
                float center=group.transform.position.z-nose.z;
                group.ForceLOD(View==2 && Mathf.Abs(center-saloonEye.z)<26?0:-1);
            }
            Vector3 eye, aim;
            if (View==1) { eye=new Vector3(.36f,CabEyeHeight,cabEyeZ-.16f); aim=eye+new Vector3(0,-.13f,1)*25; }
            else if (View==2) { eye=saloonEye; aim=eye+new Vector3(0,-.02f,-1)*12; }
            else {
                aim=new Vector3(0,1.9f,-18);
                eye=aim+Quaternion.Euler(-Mathf.Clamp(39+pitch,10,79),20+yaw,0)*Vector3.forward*orbitDistance;
                cabCamera.transform.SetPositionAndRotation(nose+eye,Quaternion.LookRotation(aim-eye));
                return;
            }
            cabCamera.transform.SetPositionAndRotation(nose+eye,Quaternion.LookRotation(aim-eye)*Quaternion.Euler(pitch,yaw,0));
        }
        public void TurnView(float horizontal,float vertical)
        {

            yaw=Mathf.Repeat(yaw+horizontal+180,360)-180;
            pitch=Mathf.Clamp(pitch+vertical,-75,75);
            UpdateCamera();
        }
        public void CycleView()
        {
            View=(View+1)%3; yaw=pitch=0; ReleaseMouse(); UpdateCamera();
        }
        void ReleaseMouse() { if(Cursor.lockState!=CursorLockMode.None) Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
        void OnApplicationFocus(bool focus) { if(!focus) ReleaseMouse(); }
        void OnDisable() { ReleaseMouse(); }

        void RectColor(float x,float y,float w,float h,Color c)
        {
            GUI.color=c; GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture); GUI.color=Color.white;
        }
        void Label(float x,float y,float w,float h,string msg,int size,Color color,bool bold=false)
        {
            style.fontSize=size; style.fontStyle=bold?FontStyle.Bold:FontStyle.Normal; style.normal.textColor=color;
            GUI.Label(new Rect(x,y,w,h),msg,style);
        }
        static Rect StartButton(float h) => new Rect(56,h*.5f+146,365,48);
        void Title(float h)
        {
            float y=h*.5f-230;
            RectColor(26,y,444,451,Ink);RectColor(26,y,444,5,Blue);
            Label(56,y+26,375,28,"SEOUL METRO     /     LINE 04",16,Blue,true);
            Label(53,y+63,395,83,"4호선 운전실",48,Color.white,true);
            Label(56,y+151,370,34,"오이도  →  진접      51개 역 각역정차",20,Pale,true);
            RectColor(56,y+202,363,1,new Color(.27f,.39f,.44f));
            Label(56,y+213,375,29,"정차 표지 ±4 m 안에서 멈추세요.",17,Color.white,true);
            Label(56,y+248,380,26,"승하차 +200   ·   정차 오차 보너스 0~100",16,Pale);
            Label(56,y+278,380,26,"문 열기 +25   ·   닫기 +25   ·   통과 −100",16,Pale);
            Label(56,y+324,385,27,"W 가속    S 제동    E 출입문    C 시점",15,Color.white);
            Rect b=StartButton(h);RectColor(b.x,b.y,b.width,b.height,Blue);
            Label(b.x+18,b.y,340,48,"운행 시작                         ENTER  ↵",20,Ink,true);
        }
        void OnGUI()
        {
            if (Event.current.type!=EventType.Repaint) return;
            if(style==null) style=new GUIStyle(GUI.skin.label){font=font,alignment=TextAnchor.MiddleLeft,padding=new RectOffset()};
            float s=Mathf.Min(Screen.width/1200f,Screen.height/680f), w=Screen.width/s, h=Screen.height/s;
            Matrix4x4 saved=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(s,s,1));
            if(Opening){Title(h);GUI.matrix=saved;return;}
            float remaining=RunState.Stops[run.Target]-run.Distance;
            RectColor(24,22,262,64,Ink); RectColor(24,22,5,64,Blue);
            Label(43,24,220,22,"SEOUL METRO   /   LINE 04",13,Blue,true);
            Label(43,44,235,34,"오이도 → 진접",22,Color.white,true);
            RectColor(24,94,382,64,Ink);
            Label(42,96,350,27,"운행 점수   "+run.Score.ToString("N0")+" 점",18,Blue,true);
            Label(42,124,355,28,run.Receipt,14,Pale);
            RectColor(w-360,22,336,64,Ink);
            Label(w-343,25,250,22,run.Complete?"운행 종료":(run.Served<0?"출발역":"정차역")+"   "+(run.Target+1)+" / "+RunState.Stops.Length,13,Blue,true);
            Label(w-343,44,225,31,RunState.Names[run.Target],21,Color.white,true);
            Label(w-127,43,100,30,(Mathf.Abs(remaining)<12?remaining.ToString("0.00"):Mathf.RoundToInt(remaining).ToString())+" m",19,Pale);
            if(Mathf.Abs(remaining)<100){
                RectColor(w-360,91,336,53,Ink);
                Label(w-344,92,310,23,"정차 허용 ±4 m     목표 0.00 m",14,Pale,true);
                float x=w-192;RectColor(x-104,124,208,3,Pale);RectColor(x-35,121,70,8,Blue);
                RectColor(x-1,117,2,16,Color.white);
                RectColor(x-Mathf.Clamp(remaining,-12,12)*8.7f-2,116,4,19,Color.yellow);
            }
            string action = run.Complete?"운행 완료   ·   R  다시 운전":
                run.Phase==DoorPhase.Opening?"승강장 쪽 문 열리는 중":
                run.Phase==DoorPhase.Closing?"문 닫히는 중":run.Doors?
                (run.Dwell<3?"승하차 중   "+Mathf.CeilToInt(3-run.Dwell)+"초":"E  문 닫고 승하차 완료"):
                run.Missed?"통과 −100   ·   R  재접근":
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
            Label(w-425,h-94,397,31,View==2?"WASD 객실 이동    ↑ 가속    ↓ 제동    E 승하차":"W 가속     S 제동     Space 비상 제동     E 승하차",15,Color.white,true);
            Label(w-425,h-61,410,26,View==0?"우클릭 회전   휠 거리   F 초기화   C 운전석":"C 시점    우클릭 드래그 둘러보기    F 정면    R 재시작",14,Pale);
            if(run.Complete){
                float x=w*.5f-207,y=h*.5f-86;
                RectColor(x,y,414,172,Ink);
                Label(x+22,y+12,370,28,"진접 종착   /   51개 역 승하차 완료",19,Blue,true);
                Label(x+22,y+49,365,61,run.Score.ToString("N0")+" 점",38,Color.white,true);
                Label(x+22,y+119,375,30,"통과 "+run.Misses+"회     R  새 운행",17,Pale);
            }
            GUI.matrix=saved;
        }
    }
}
