using UnityEngine;

public static class ClearWaterMaterial
{
    public static Material Create(bool pool)
    {
        var material=new Material(Shader.Find("GP1/Level2/Kelkaya Water"));
        material.name=pool?"Clear Pool Water":"Clear Cave Water";
        material.SetColor("_ShallowColor",new Color(.08f,.45f,.5f,.16f));
        material.SetColor("_DeepColor",new Color(.015f,.12f,.18f,.42f));
        material.SetColor("_FresnelColor",new Color(.55f,.74f,.8f,1));
        material.SetFloat("_WaveHeight",pool?.018f:.035f);
        material.SetFloat("_WaveScale",.65f);
        material.SetFloat("_WaveSpeed",.45f);
        return material;
    }
}
