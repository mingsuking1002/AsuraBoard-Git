using UnityEngine;
namespace Marble.Unity
{
    // Screen-space vision loss, drawn above the world and below all decision UI.
    public static class BodyWorldLayer
    {
        static readonly Texture2D[] masks=new Texture2D[4];
        public const float NearVisionRadius=110;
        const float FarVisionRadius=600;
        public static void DrawVignette(int missingEyesMask,Vector2 localPawnCenter,float radius,float comfortScale=1)
        {
            int state=missingEyesMask&3;if(state==0 || Event.current.type!=EventType.Repaint)return;
            if(masks[state]==null)masks[state]=CreateMask(state);
            var color=GUI.color;
            GUI.color=new Color(1,1,1,Mathf.Clamp(comfortScale,.5f,1));
            if(state==3)
            {
                // Keep the clear center, with a long fade into a faintly visible world.
                // Clamp UVs beyond the texture to the same darkest border value.
                float diameter=Mathf.Max(1,radius)*(FarVisionRadius/NearVisionRadius)*2;
                var uv=new Rect(.5f-localPawnCenter.x/diameter,.5f+(localPawnCenter.y-900)/diameter,1440/diameter,900/diameter);
                GUI.DrawTextureWithTexCoords(new Rect(0,0,1440,900),masks[state],uv,true);
            }
            else GUI.DrawTexture(new Rect(0,0,1440,900),masks[state],ScaleMode.StretchToFill,true);
            GUI.color=color;
        }
        static Texture2D CreateMask(int state)
        {
            const int width=512,height=320;
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Generated eye vignette "+state,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
            // One eye shifts the remaining field of view away from the missing side.
            var center=state==3?new Vector2(.5f,.5f):new Vector2(state==1?.75f:.25f,.5f);
            var pixels=new Color32[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float dx=((x+.5f)/width-center.x)*2,dy=((y+.5f)/height-center.y)*(state==3?2:.8f);
                float distance=Mathf.Sqrt(dx*dx+dy*dy),opacity;
                if(state==3)
                {
                    float fade=Mathf.InverseLerp(NearVisionRadius*.6f,FarVisionRadius,distance*FarVisionRadius);
                    // Four percent of the scene remains at the darkest point.
                    opacity=.96f*(1-Mathf.Pow(1-fade,6));
                }
                else
                {
                    // A wider transition leaves more of the missing side faintly visible.
                    float fade=Mathf.InverseLerp(.45f,1.45f,distance);
                    opacity=fade*fade*(3-2*fade);
                }
                pixels[y*width+x]=new Color32(0,0,0,(byte)Mathf.RoundToInt(opacity*255));
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
        public static void Dispose()
        {for(int i=0;i<masks.Length;i++){if(masks[i]!=null)Object.Destroy(masks[i]);masks[i]=null;}}
    }
}
