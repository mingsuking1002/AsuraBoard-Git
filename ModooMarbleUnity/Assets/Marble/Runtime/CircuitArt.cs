using UnityEngine;
namespace Marble.Unity
{
    // Code-native drawing helpers. Every shape is generated locally.
    public static class CircuitArt
    {
        static Texture2D disc;
        public static void Texture(Rect r,Texture texture,Color tint){var old=GUI.color;GUI.color=tint;GUI.DrawTexture(r,texture);GUI.color=old;}
        public static void Fill(Rect r,Color c)=>Texture(r,Texture2D.whiteTexture,c);
        public static void Circle(Rect r,Color c)
        {
            if(disc==null)
            {
                disc=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Generated antialiased disc",hideFlags=HideFlags.DontSave};var pixels=new Color[4096];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32))));
                disc.SetPixels(pixels);disc.Apply();
            }
            Texture(r,disc,c);
        }
        public static void Outline(Rect r,Color c,float w){Fill(new Rect(r.x,r.y,r.width,w),c);Fill(new Rect(r.x,r.yMax-w,r.width,w),c);Fill(new Rect(r.x,r.y,w,r.height),c);Fill(new Rect(r.xMax-w,r.y,w,r.height),c);}
        public static void Line(Vector2 a,Vector2 b,Color color,float width)
        {
            var matrix=GUI.matrix;var delta=b-a;var pivot=new Vector3(a.x,a.y,0);
            GUI.matrix=matrix*Matrix4x4.Translate(pivot)*Matrix4x4.Rotate(Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg));Fill(new Rect(0,-width/2,delta.magnitude,width),color);GUI.matrix=matrix;
        }
        public static void Label(Rect r,string text,Font font,int size,Color color,bool bold)
        {
            var style=new GUIStyle(GUI.skin.label){font=font,fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,alignment=TextAnchor.MiddleCenter,wordWrap=false,padding=new RectOffset(0,0,0,0)};style.normal.textColor=color;GUI.Label(r,text,style);
        }
        public static void Dispose(){if(disc!=null)Object.Destroy(disc);disc=null;}
    }
}
