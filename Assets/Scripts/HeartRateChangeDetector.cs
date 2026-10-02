using System.Collections.Generic;

// Game rule only. Each confirmation must be a separate, spaced watch reading.
public sealed class HeartRateChangeDetector
{
    public float WindowSeconds = 10f;
    public int ThresholdBpm = 25;
    public int RequiredConfirmations = 3;
    struct Sample { public float time; public int bpm; public Sample(float t,int b){time=t;bpm=b;} }
    readonly Queue<Sample> history = new Queue<Sample>();
    readonly Queue<int> recent = new Queue<int>();
    float lastTime = -1, candidateTime;
    int direction, confirmations;
    public int Confirmations => confirmations;
    public int LastChange { get; private set; }
    public void Reset(){history.Clear();recent.Clear();lastTime=-1;direction=confirmations=0;LastChange=0;}
    public bool Add(int bpm,float time)
    {
        if(bpm<=0 || bpm>300)return false;
        if(lastTime>=0 && time-lastTime<.5f)return false;
        if(lastTime>=0 && time-lastTime>5f)Reset();
        lastTime=time;
        recent.Enqueue(bpm);
        if(recent.Count<3)return false;
        if(recent.Count>3)recent.Dequeue();
        var filtered=recent.ToArray();System.Array.Sort(filtered);bpm=filtered[1];
        while(history.Count>0 && time-history.Peek().time>WindowSeconds)history.Dequeue();
        int change=0;
        foreach(var sample in history)
        {
            int delta=bpm-sample.bpm;
            if(System.Math.Abs(delta)>System.Math.Abs(change))change=delta;
        }
        bool exceeds=System.Math.Abs(change)>=ThresholdBpm;
        int sign=System.Math.Sign(change);
        if(exceeds)
        {
            if(direction!=sign || time-candidateTime>WindowSeconds){confirmations=0;candidateTime=time;}
            direction=sign;confirmations++;LastChange=change;
        }
        else {direction=confirmations=0;LastChange=0;}
        history.Enqueue(new Sample(time,bpm));
        return confirmations>=RequiredConfirmations;
    }
}
