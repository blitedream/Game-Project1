using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class BreathingAudioAudit
{
    static BreathingAudioAudit() { EditorApplication.delayCall += Audit; }
    static void Audit()
    {
        string report="";
        foreach(string name in new[]{"Unequipped","Equipped"})
        {
            var clip=Resources.Load<AudioClip>("Audio/Breathing/"+name);
            if(clip==null)continue;
            clip.LoadAudioData();
            var data=new float[clip.samples*clip.channels];
            bool read=clip.GetData(data,0);double energy=0;float peak=0;int first=-1;
            for(int i=0;i<data.Length;i++){energy+=data[i]*data[i];peak=Mathf.Max(peak,Mathf.Abs(data[i]));if(first<0 && Mathf.Abs(data[i])>.02f)first=i;}
            report+=$"{name}: read={read}, length={clip.length}, rms={System.Math.Sqrt(energy/data.Length):F5}, peak={peak:F5}, firstSound={first/(float)(clip.frequency*clip.channels):F2}s\n";
        }
        File.WriteAllText("Artifacts/breathing_audio_audit.txt",report);
    }
}
