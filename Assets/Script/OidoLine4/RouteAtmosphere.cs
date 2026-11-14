using UnityEngine;
using UnityEngine.Rendering;

namespace LineFour.Oido
{
    public enum RouteWeather { Clear, Overcast, Rain }
    public sealed class RouteAtmosphere : MonoBehaviour
    {
        public float Elapsed {get;private set;}
        public RouteWeather Weather {get;private set;}
        public float RainAmount {get;private set;}
        public float Daylight {get;private set;}
        Line4Drive drive;
        Light sun,cabin;
        readonly Light[] street=new Light[8];
        Material lamp;
        readonly System.Collections.Generic.List<System.Tuple<Renderer,int>> lamps=new System.Collections.Generic.List<System.Tuple<Renderer,int>>();
        Material sky,rainMaterial;
        ParticleSystem rain;
        float nextWeather=180, cloud,clock;
        MaterialPropertyBlock block;
        Renderer[] nightLights;
        public static RouteWeather SelectWeather(float value) =>
            value<.65f?RouteWeather.Clear:value<.85f?RouteWeather.Overcast:RouteWeather.Rain;
        void Start()
        {
            drive=GetComponent<Line4Drive>();
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Directional){sun=light;break;}
            cabin=new GameObject("Cab and saloon lighting",typeof(Light)).GetComponent<Light>();
            cabin.transform.SetParent(drive.cabCamera.transform,false);cabin.transform.localPosition=new Vector3(0,.55f,-.25f);
            cabin.type=LightType.Point;cabin.range=8;cabin.color=new Color(1,.92f,.77f);cabin.shadows=LightShadows.None;
            lamp=new Material(Shader.Find("Standard"));lamp.color=new Color(1,.79f,.42f);
            lamp.EnableKeyword("_EMISSION");lamp.SetColor("_EmissionColor",new Color(1,.67f,.25f)*2.4f);
            for(int i=0;i<street.Length;i++){
                var light=new GameObject("Street lighting "+i,typeof(Light)).GetComponent<Light>();
                light.type=LightType.Point;light.range=19;light.color=new Color(1,.76f,.43f);light.shadows=LightShadows.None;
                var bulb=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(light.transform,false);bulb.transform.localScale=Vector3.one*.17f;
                bulb.GetComponent<Renderer>().sharedMaterial=lamp;
                street[i]=light;
            }
            sky=new Material(Shader.Find("LineFour/Changing Sky"));RenderSettings.skybox=sky;
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;
            block=new MaterialPropertyBlock();
            var lights=new System.Collections.Generic.List<Renderer>();
            foreach(var renderer in drive.consist.GetComponentsInChildren<Renderer>())
                lights.Add(renderer);
            nightLights=lights.ToArray();
            foreach(var renderer in nightLights){
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)if(slots[i].name.Contains("Light"))lamps.Add(System.Tuple.Create(renderer,i));
            }
            var obj=new GameObject("Rain streaks");
            rain=obj.AddComponent<ParticleSystem>();
            rain.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=rain.main;main.loop=true;main.startLifetime=1.4f;main.startSpeed=20;
            main.startSize=.035f;main.startColor=new Color(.67f,.76f,.84f,.35f);
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=3500;
            var shape=rain.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(36,2,36);
            var emission=rain.emission;emission.rateOverTime=0;
            var rendererRain=rain.GetComponent<ParticleSystemRenderer>();
            rainMaterial=new Material(Shader.Find("Particles/Standard Unlit"));rainMaterial.SetFloat("_Mode",2);
            rainMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);rainMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            rainMaterial.SetInt("_ZWrite",0);rainMaterial.EnableKeyword("_ALPHABLEND_ON");rainMaterial.renderQueue=3000;
            rendererRain.sharedMaterial=rainMaterial;rendererRain.renderMode=ParticleSystemRenderMode.Stretch;
            rendererRain.lengthScale=7;rendererRain.velocityScale=.025f;rain.Play();
        }
        public void Apply(float elapsed,RouteWeather weather,float dt)
        {
            Elapsed=elapsed;Weather=weather;
            float phase=Mathf.Repeat(elapsed,600)/600f;
            float altitude=Mathf.Sin(phase*2*Mathf.PI);
            Daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.18f,.42f,altitude));
            float dusk=Mathf.Exp(-Mathf.Abs(altitude)*9);
            float target=weather==RouteWeather.Clear?.1f:weather==RouteWeather.Overcast?.73f:1;
            cloud=Mathf.MoveTowards(cloud,target,dt*.06f);
            RainAmount=Mathf.MoveTowards(RainAmount,weather==RouteWeather.Rain?1:0,dt*.08f);
            sky.SetFloat("_Day",Daylight);sky.SetFloat("_Night",1-Daylight);
            sky.SetFloat("_Dusk",dusk);sky.SetFloat("_Cloud",cloud);
            if(sun){
                sun.transform.rotation=Quaternion.Euler(phase*360,phase*35-35,0);
                sun.color=Color.Lerp(new Color(1,.36f,.16f),new Color(1,.94f,.81f),Mathf.Clamp01(altitude*3));
                sun.intensity=Mathf.Lerp(.05f,1.12f,Daylight)*(1-cloud*.62f);
            }
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.055f,.075f,.15f),new Color(.60f,.69f,.80f),Daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.055f,.065f,.10f),new Color(.43f,.47f,.51f),Daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.03f,.035f,.05f),new Color(.30f,.31f,.30f),Daylight);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=Color.Lerp(new Color(.04f,.055f,.10f),new Color(.60f,.69f,.75f),Daylight);
            RenderSettings.fogDensity=Mathf.Lerp(.0028f,.006f,RainAmount);
            int section=System.Array.FindIndex(RouteSchedule.Stops,s=>s>=drive.run.Distance);
            bool covered=section>=0&&RouteSchedule.Tunnel(section)&&drive.cabCamera.transform.position.y<7.4f;
            var emission=rain.emission;emission.rateOverTime=covered||drive.View==2?0:RainAmount*1100;
            rain.transform.position=drive.cabCamera.transform.position+Vector3.up*17+drive.cabCamera.transform.forward*10;
            rain.transform.rotation=Quaternion.Euler(90,0,0);
            cabin.enabled=!drive.Opening && drive.View!=0;
            cabin.intensity=Mathf.Lerp(.32f,1.35f,1-Daylight);
            for(int i=0;i<street.Length;i++){
                float z=Mathf.Floor(drive.run.Distance/25)*25+(i/2-1)*25;
                street[i].transform.position=CurveRoute.Map(new Vector3(i%2==0?-19:24.3f,5,z));
                street[i].gameObject.SetActive(Daylight<.70f);
                street[i].intensity=(1-Daylight)*2.5f;
            }
            foreach(var pair in lamps){
                block.Clear();block.SetColor("_EmissionColor",Color.white*Mathf.Lerp(.5f,1.5f,1-Daylight));
                pair.Item1.SetPropertyBlock(block,pair.Item2);
            }
        }
        void Update()
        {
            if(drive.Opening){Apply(75,RouteWeather.Clear,Time.deltaTime);return;}
            float elapsed=clock+=Time.deltaTime;
            var weather=Weather;
            if(elapsed>=nextWeather){weather=SelectWeather(Random.value);nextWeather=elapsed+Random.Range(120,220);}
            Apply(elapsed,weather,Time.deltaTime);
        }
        void OnDestroy(){if(cabin)Destroy(cabin.gameObject);foreach(var light in street)if(light)Destroy(light.gameObject);if(lamp)Destroy(lamp);if(rain)Destroy(rain.gameObject);if(sky)Destroy(sky);if(rainMaterial)Destroy(rainMaterial);}
    }
}
