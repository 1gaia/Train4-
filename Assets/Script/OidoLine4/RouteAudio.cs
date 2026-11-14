using System.Collections.Generic;
using UnityEngine;

namespace LineFour.Oido
{
    public sealed class RouteAudio : MonoBehaviour
    {
        AudioSource ride,voice,rain;
        Line4Drive drive;
        RouteAtmosphere atmosphere;
        readonly Queue<AudioClip> announcements=new Queue<AudioClip>();
        readonly AudioClip[] stations=new AudioClip[51];
        AudioClip closing,welcome,terminal;
        int next=-1;
        bool announced,started;
        DoorPhase previous;
        public int SpokenStations {get;private set;}
        AudioSource Source(bool loop,float volume)
        {
            var source=gameObject.AddComponent<AudioSource>();
            source.loop=loop;source.volume=volume;source.spatialBlend=0;
            return source;
        }
        void Start()
        {
            drive=GetComponent<Line4Drive>();atmosphere=GetComponent<RouteAtmosphere>();
            for(int i=0;i<stations.Length;i++)stations[i]=Resources.Load<AudioClip>("RouteAudio/Next"+i.ToString("00"));
            closing=Resources.Load<AudioClip>("RouteAudio/Closing");
            welcome=Resources.Load<AudioClip>("RouteAudio/Welcome");
            terminal=Resources.Load<AudioClip>("RouteAudio/Terminal");
            ride=Source(true,0);ride.clip=Resources.Load<AudioClip>("RouteAudio/MetroLoop");ride.Play();
            voice=Source(false,.85f);voice.priority=20;
            rain=Source(true,0);rain.clip=Resources.Load<AudioClip>("RouteAudio/Rain");rain.Play();
        }
        void Update()
        {
            if(drive.Opening)return;
            if(!started){announcements.Enqueue(welcome);started=true;}
            var r=drive.run;
            if(next!=r.Target){next=r.Target;announced=false;}
            float remaining=RunState.Stops[r.Target]-r.Distance;
            if(!announced && remaining<210 && remaining>=-RunState.Tolerance){
                announcements.Enqueue(r.Target==50?terminal:stations[r.Target]);announced=true;SpokenStations++;
            }
            if(r.Phase==DoorPhase.Closing && previous!=DoorPhase.Closing)announcements.Enqueue(closing);
            previous=r.Phase;
            if(!voice.isPlaying && announcements.Count>0){voice.clip=announcements.Dequeue();if(voice.clip)voice.Play();}
            float speed=r.Speed/18;
            float duck=voice.isPlaying?.38f:1;
            ride.volume=Mathf.Lerp(ride.volume,Mathf.Clamp01(speed*3)*(.11f+.18f*speed)*duck,Time.deltaTime*3);
            ride.pitch=Mathf.Lerp(.75f,1.12f,speed);
            rain.volume=atmosphere.RainAmount*(drive.View==0?.22f:.065f)*duck;
        }
    }
}
