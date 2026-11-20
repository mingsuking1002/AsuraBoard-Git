using System.Collections.Generic;
using UnityEngine;
namespace Marble.Unity
{
    // Locally authored pen strokes and paper surfaces. No reference image is embedded.
    public static class PaperTheme
    {
        public static readonly Color Ink=Hex("20221D");
        public static readonly Color Paper=Hex("FFFDF5");
        public static readonly Color Muted=Hex("707260");
        public static readonly Color Table=Hex("98A779");
        public static readonly Color Selected=Hex("DDE8C5");
        public static readonly Color Forest=Hex("365D4B");
        public static readonly Color FrontTable=Hex("344E46");
        public static readonly Color Sun=Hex("F2C76C");
        public static readonly Color Coral=Hex("E67D61");
        static readonly Dictionary<string,Texture2D> frames=new Dictionary<string,Texture2D>();
        static Texture2D grain,inkPixel;
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out Color color);return color;}
        public static void ApplySkin(GUISkin skin)
        {
            if(inkPixel==null){inkPixel=new Texture2D(1,1){name="Ink scrollbar",hideFlags=HideFlags.DontSave};inkPixel.SetPixel(0,0,Ink);inkPixel.Apply();}
            skin.settings.cursorColor=Ink;skin.settings.selectionColor=Selected;
            skin.verticalScrollbar.fixedWidth=11;skin.verticalScrollbar.normal.background=null;
            skin.verticalScrollbarThumb.normal.background=inkPixel;skin.verticalScrollbarThumb.hover.background=inkPixel;skin.verticalScrollbarThumb.active.background=inkPixel;
            skin.verticalScrollbarThumb.fixedWidth=6;skin.verticalScrollbarUpButton.fixedHeight=0;skin.verticalScrollbarDownButton.fixedHeight=0;
            skin.horizontalScrollbar.fixedHeight=11;skin.horizontalScrollbar.normal.background=null;
            skin.horizontalScrollbarThumb.normal.background=inkPixel;skin.horizontalScrollbarThumb.hover.background=inkPixel;skin.horizontalScrollbarThumb.active.background=inkPixel;
            skin.horizontalScrollbarThumb.fixedHeight=6;skin.horizontalScrollbarLeftButton.fixedWidth=0;skin.horizontalScrollbarRightButton.fixedWidth=0;
        }
        static float Wave(float t,float seed)=>Mathf.Sin(t*13.1f+seed)*.68f+Mathf.Sin(t*27.3f+seed*1.7f)*.27f+Mathf.Sin(t*6.1f-seed)*.38f;
        public static Color Wash(Color color,float strength=.16f)
        {
            var c=Color.Lerp(Paper,color,strength);c.a=1;return c;
        }
        public static void Surface(Rect r,Color color)
        {
            CircuitArt.Fill(r,color);
            if(grain==null)
            {
                grain=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Handmade paper grain",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
                var pixels=new Color[4096];
                for(int i=0;i<pixels.Length;i++){uint n=unchecked((uint)(i*747796405+2891336453));n=(n^(n>>16))*2246822519u;pixels[i]=new Color(.27f,.25f,.16f,((n&255)/255f)*.034f);}
                grain.SetPixels(pixels);grain.Apply();
            }
            GUI.DrawTextureWithTexCoords(r,grain,new Rect(0,0,r.width/64,r.height/64),true);
        }
        public static void Frame(Rect r,Color ink,float stroke=2.5f,int variation=0)
        {
            if(r.width<6 || r.height<6)return;
            int w=Mathf.Clamp(Mathf.RoundToInt(r.width),6,1600),h=Mathf.Clamp(Mathf.RoundToInt(r.height),6,1100);
            int weight=Mathf.RoundToInt(stroke*4);string key=w+":"+h+":"+weight+":"+(variation%7);
            if(!frames.TryGetValue(key,out var frame))
            {
                frame=new Texture2D(w,h,TextureFormat.RGBA32,false){name="Generated ink frame "+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave};
                var data=new Color[w*h];float amplitude=Mathf.Clamp(Mathf.Min(w,h)*.007f,.3f,1.8f),seed=variation*.73f+w*.011f+h*.017f;
                float margin=Mathf.Max(2,stroke*.7f+amplitude);
                var left=new float[h];var right=new float[h];var top=new float[w];var bottom=new float[w];
                for(int y=0;y<h;y++){float t=y/(float)h;left[y]=margin+Wave(t,seed)*amplitude;right[y]=w-1-margin+Wave(t,seed+3)*amplitude;}
                for(int x=0;x<w;x++){float t=x/(float)w;top[x]=margin+Wave(t,seed+7)*amplitude;bottom[x]=h-1-margin+Wave(t,seed+11)*amplitude;}
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    float d=Mathf.Min(Mathf.Min(Mathf.Abs(x-left[y]),Mathf.Abs(x-right[y])),Mathf.Min(Mathf.Abs(y-top[x]),Mathf.Abs(y-bottom[x])));
                    float alpha=Mathf.Clamp01(stroke*.50f+.60f-d);
                    // A second loose pass crosses the first stroke like the user's ink frame.
                    float d2=Mathf.Min(Mathf.Min(Mathf.Abs(x-left[y]-amplitude*1.1f),Mathf.Abs(x-right[y]+amplitude*.8f)),Mathf.Min(Mathf.Abs(y-top[x]-amplitude*.9f),Mathf.Abs(y-bottom[x]+amplitude)));
                    alpha=Mathf.Max(alpha,Mathf.Clamp01(stroke*.20f+.35f-d2)*.55f);
                    data[y*w+x]=new Color(1,1,1,alpha);
                }
                frame.SetPixels(data);frame.Apply();frames[key]=frame;
            }
            CircuitArt.Texture(r,frame,ink);
        }
        public static void Card(Rect r,Color paper,bool stacked=false,float stroke=2.6f,int variation=0)
        {
            CircuitArt.Fill(new Rect(r.x+4,r.y+5,r.width,r.height),new Color(.13f,.16f,.10f,.16f));
            if(stacked)
            {
                var underneath=new Rect(r.x+2,r.y+2,r.width,r.height);Surface(underneath,Wash(Table,.08f));Frame(underneath,Ink,1.1f,variation+2);
            }
            Surface(r,paper);Frame(r,Ink,Mathf.Max(.9f,stroke*.72f),variation);
        }
        public static void Chip(Rect r,Color fill)
        {CircuitArt.Fill(r,fill);Frame(r,Ink,1.2f);}
        public static void Underline(Rect r,Color color)
        {
            CircuitArt.Line(new Vector2(r.x,r.y+1),new Vector2(r.x+r.width*.48f,r.y),color,1.7f);
            CircuitArt.Line(new Vector2(r.x+r.width*.48f,r.y),new Vector2(r.xMax,r.y+1.5f),color,1.7f);
        }
        public static void Dispose()
        {foreach(var texture in frames.Values)Object.Destroy(texture);frames.Clear();if(grain!=null)Object.Destroy(grain);grain=null;if(inkPixel!=null)Object.Destroy(inkPixel);inkPixel=null;}
    }
}
