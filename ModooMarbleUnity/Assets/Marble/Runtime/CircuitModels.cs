using System.Collections.Generic;
using UnityEngine;
using Marble.Core;
using UnityEngine.Rendering;
namespace Marble.Unity
{
    // Original toy robots and rounded dice, assembled from locally generated geometry.
    public sealed class CircuitModels : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Camera> cameras=new List<Camera>();
        readonly List<RenderTexture> renders=new List<RenderTexture>();
        readonly Transform[] robots=new Transform[4],dice=new Transform[2];
        readonly float[] diceAirHeight=new float[2];
        const float DiceGroundY=-.62f;
        readonly SidekickAvatar[] people=new SidekickAvatar[4];
        bool previewFixed;float previewYaw=-18;
        public bool UsesSidekick=>people[0]!=null;
        public int VisibleCharacterParts(int seat)=>people[seat]==null?0:people[seat].PartCount;
        public void SetPreviewYaw(float yaw){previewFixed=true;previewYaw=yaw;if(robots[0]!=null)robots[0].localRotation=Quaternion.Euler(0,yaw+(people[0]!=null?180:0),0);}
        public bool ReducedMotion;
        Mesh roundedBox;
        readonly int[] bodyMasks={-1,-1,-1,-1};readonly float[] fallPose=new float[4];MaterialPropertyBlock bodyTint;
        public RenderTexture DiceTexture{get;private set;}
        Material shell,navy,ivory,gold;
        Material[] diceShells;int activeDiceSkin=-1;Camera diceCamera;
        static int nextStudio;readonly Vector3 studioOffset=new Vector3(0,0,32*(++nextStudio));
        public Texture Portrait(int i)=>i>=0 && i<4 && i<renders.Count?renders[i]:null;
        // Native Unity allocations must not run in MonoBehaviour field initializers.
        void Awake(){bodyTint=new MaterialPropertyBlock();}
        public void Initialize(int[] avatars=null,int[] colors=null,int[] outfits=null)
        {
            if(bodyTint==null)bodyTint=new MaterialPropertyBlock();
            roundedBox=RoundedBox();shell=Paint("F2EEE1");navy=Paint("203E46");ivory=Paint("FFF6DE");gold=Paint("EDBF67");
            for(int i=0;i<4;i++)
            {
                var root=Stage("Explorer robot "+(i+1),new Vector3(i*12,-500,0));
                var robot=new GameObject("Hand assembled explorer").transform;robot.SetParent(root,false);robots[i]=robot;
                if(SidekickAvatar.Available){people[i]=robot.gameObject.AddComponent<SidekickAvatar>();people[i].Initialize(outfits==null?WardrobeCatalog.DefaultCode:outfits[i]);}
                else Robot(robot,avatars==null?i:avatars[i],MarbleBoardView.PlayerColors[colors==null?i:colors[i]]);
                SetBodyState(i,0);robot.localRotation=Quaternion.Euler(0,people[i]!=null?162:-18,0);
                CameraFor(root,new Vector3(0,1.55f,-4.6f),new Vector3(0,1.08f,0),1.30f,192,240);
            }
            var stage=Stage("Generated dice studio",new Vector3(60,-500,0));var dark=Paint("274C51");
            for(int i=0;i<2;i++)
            {
                var root=new GameObject("Rounded die "+(i+1)).transform;root.SetParent(stage,false);root.localPosition=new Vector3(i==0?-.72f:.72f,0,0);dice[i]=root;
                Part("Rounded cube",root,PrimitiveType.Cube,Vector3.zero,Vector3.one,ivory);
                for(int face=1;face<=6;face++)
                {
                    Vector3 normal=Normal(face),v=Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up,u=Vector3.Cross(v,normal);
                    foreach(var pip in Pips(face))
                    {
                        var dot=Part("Face "+face+" pip",root,PrimitiveType.Sphere,normal*.503f+u*pip.x+v*pip.y,new Vector3(.13f,.13f,.028f),dark);
                        dot.localRotation=Quaternion.FromToRotation(Vector3.forward,normal);
                    }
                }
            }
            diceShells=new[]{ivory,Paint("C68751"),Paint("3B9AB8"),Paint("E9BD4D"),Paint("BDA3ED")};SetDiceSkin(0);
            diceCamera=CameraFor(stage,new Vector3(0,6,-4.5f),Vector3.zero,1.22f,512,256);DiceTexture=diceCamera.targetTexture;
        }
        public void SetDiceSkin(int skin)
        {
            skin=Mathf.Clamp(skin,0,4);if(activeDiceSkin==skin || diceShells==null)return;activeDiceSkin=skin;
            for(int i=0;i<2;i++)foreach(var renderer in dice[i].GetComponentsInChildren<Renderer>())
                if(renderer.gameObject.name=="Rounded cube")renderer.sharedMaterial=diceShells[skin];
                else renderer.sharedMaterial=skin==4?diceShells[1+(int)((uint)renderer.gameObject.name.GetHashCode()%3)]:navy;
        }
        Transform Stage(string name,Vector3 position){var root=new GameObject(name).transform;root.SetParent(transform,false);root.position=position+studioOffset;return root;}
        Material Paint(string hex)
        {
            var m=new Material(Shader.Find("Standard")){name="Circuit solid "+hex,color=MarbleBoardView.Hex(hex)};m.SetFloat("_Glossiness",.12f);materials.Add(m);return m;
        }
        Transform Part(string name,Transform root,PrimitiveType shape,Vector3 p,Vector3 size,Material m)
        {
            GameObject go;
            if(shape==PrimitiveType.Cube){go=new GameObject(name);go.AddComponent<MeshFilter>().sharedMesh=roundedBox;go.AddComponent<MeshRenderer>();}
            else{go=GameObject.CreatePrimitive(shape);go.name=name;Destroy(go.GetComponent<Collider>());}
            go.layer=28;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=size;
            var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=m;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return go.transform;
        }
        void Robot(Transform root,int variant,Color bodyColor)
        {
            var accent=Paint(ColorUtility.ToHtmlStringRGB(bodyColor));
            Part("Backpack",root,PrimitiveType.Cube,new Vector3(0,.87f,.28f),new Vector3(.55f,.60f,.28f),accent);
            Part("Body",root,PrimitiveType.Cube,new Vector3(0,.85f,0),new Vector3(.69f,.65f,.46f),shell);
            Part("Chest plate",root,PrimitiveType.Cube,new Vector3(0,.87f,-.25f),new Vector3(.44f,.34f,.08f),accent);
            Part("Compass badge",root,PrimitiveType.Sphere,new Vector3(0,.90f,-.30f),new Vector3(.13f,.13f,.05f),gold);
            for(int side=-1;side<=1;side+=2)
            {
                Part(side<0?"Left boot":"Right boot",root,PrimitiveType.Cube,new Vector3(side*.23f,.25f,-.045f),new Vector3(.30f,.23f,.48f),navy);
                Part(side<0?"Left leg":"Right leg",root,PrimitiveType.Capsule,new Vector3(side*.23f,.43f,0),new Vector3(.15f,.17f,.15f),accent);
                Part("Shoulder",root,PrimitiveType.Sphere,new Vector3(side*.44f,1.04f,0),Vector3.one*.23f,accent);
                var arm=Part("Arm",root,PrimitiveType.Capsule,new Vector3(side*.47f,.80f,0),new Vector3(.18f,.22f,.18f),shell);arm.localRotation=Quaternion.Euler(0,0,side*12);
                Part("Hand",root,PrimitiveType.Sphere,new Vector3(side*.51f,.60f,-.02f),Vector3.one*.20f,navy);
            }
            if(variant==4)
            {
                Part("Crown band",root,PrimitiveType.Cube,new Vector3(0,1.93f,0),new Vector3(.76f,.12f,.59f),gold);
                for(int n=0;n<5;n++)Part("Crown point",root,PrimitiveType.Cube,new Vector3(-.31f+n*.155f,2.06f,-.22f),new Vector3(.09f,.20f,.1f),gold);
            }
            if(variant==5)
            {
                var prism=Paint("B89CEB");var cyan=Paint("7EE7E0");
                for(int side=-1;side<=1;side+=2)for(int n=0;n<3;n++)
                {var wing=Part("Prism wing",root,PrimitiveType.Cube,new Vector3(side*(.58f+n*.12f),1.15f-n*.16f,.24f),new Vector3(.45f,.12f,.12f),n%2==0?prism:cyan);wing.localRotation=Quaternion.Euler(0,0,side*35);}
            }
            PrimitiveType head=variant==0?PrimitiveType.Sphere:variant==2?PrimitiveType.Capsule:PrimitiveType.Cube;
            Vector3 headSize=variant==2?new Vector3(.77f,.38f,.65f):new Vector3(.87f,.67f,.63f);
            Part("Explorer helmet",root,head,new Vector3(0,1.52f,0),headSize,accent);
            Part("Face visor",root,PrimitiveType.Cube,new Vector3(0,1.51f,-.30f),new Vector3(.65f,.34f,.13f),navy);
            for(int side=-1;side<=1;side+=2)Part(side<0?"Left eye":"Right eye",root,PrimitiveType.Sphere,new Vector3(side*.16f,1.53f,-.38f),new Vector3(.085f,.11f,.04f),ivory);
            Part("Mouth vent",root,PrimitiveType.Cube,new Vector3(0,1.39f,-.38f),new Vector3(.22f,.035f,.03f),ivory);
            for(int side=-1;side<=1;side+=2)Part(side<0?"Artificial left leg":"Artificial right leg",root,PrimitiveType.Cylinder,new Vector3(side*.23f,.34f,0),new Vector3(.07f,.23f,.07f),gold);
            Part("Artificial brain",root,PrimitiveType.Cube,new Vector3(0,1.87f,-.12f),new Vector3(.25f,.12f,.25f),gold);
            Part("Artificial kidney",root,PrimitiveType.Sphere,new Vector3(.38f,.53f,.16f),new Vector3(.19f,.24f,.15f),gold);
            Part("Artificial lung",root,PrimitiveType.Cylinder,new Vector3(-.34f,.9f,.33f),new Vector3(.16f,.25f,.16f),ivory);
            Part("Artificial heart",root,PrimitiveType.Cube,new Vector3(0,.86f,-.33f),new Vector3(.22f,.2f,.12f),gold);
            Part("Antenna stem",root,PrimitiveType.Cylinder,new Vector3(variant==1?.24f:0,1.96f,0),new Vector3(.035f,.14f,.035f),navy);
            Part("Signal tip",root,variant==3?PrimitiveType.Cube:PrimitiveType.Sphere,new Vector3(variant==1?.24f:0,2.11f,0),Vector3.one*.14f,gold);
            if(variant==1)Part("Helmet brim",root,PrimitiveType.Cube,new Vector3(0,1.77f,-.13f),new Vector3(1.03f,.11f,.87f),ivory);
            if(variant==2)for(int side=-1;side<=1;side+=2)Part("Receiver",root,PrimitiveType.Sphere,new Vector3(side*.48f,1.55f,0),new Vector3(.20f,.27f,.28f),gold);
            if(variant==3)Part("Survey flag",root,PrimitiveType.Cube,new Vector3(.15f,2.02f,0),new Vector3(.28f,.13f,.04f),accent);
        }
        public void SetBodyState(int seat,int mask)
        {
            if(seat<0 || seat>3 || robots[seat]==null || bodyMasks[seat]==mask)return;bodyMasks[seat]=mask;
            if(people[seat]!=null){people[seat].SetBodyState(mask);return;}
            foreach(var renderer in robots[seat].GetComponentsInChildren<Renderer>(true))
            {
                string name=renderer.gameObject.name;bool active=true;
                if(name=="Left eye")active=(mask&1)==0;if(name=="Right eye")active=(mask&2)==0;
                if(name=="Left boot" || name=="Left leg")active=(mask&4)==0;
                if(name=="Right boot" || name=="Right leg")active=(mask&8)==0;
                if(name=="Mouth vent")active=(mask&16)==0;
                if(name=="Artificial left leg")active=(mask&4)!=0;if(name=="Artificial right leg")active=(mask&8)!=0;
                if(name=="Artificial brain")active=(mask&32)!=0;if(name=="Artificial kidney")active=(mask&64)!=0;
                if(name=="Artificial lung")active=(mask&256)!=0;if(name=="Artificial heart")active=(mask&512)!=0;
                renderer.gameObject.SetActive(active);
                if(name=="Body" || name=="Chest plate" || name=="Explorer helmet" || name=="Backpack")
                {bodyTint.Clear();if((mask&128)!=0)bodyTint.SetColor("_Color",MarbleBoardView.Hex("89755B"));renderer.SetPropertyBlock(bodyTint);}
            }
        }
        public void SetFallPose(int seat,float value){fallPose[seat]=value;}
        Camera CameraFor(Transform root,Vector3 eye,Vector3 target,float size,int width,int height)
        {
            var go=new GameObject("Isolated studio camera");go.transform.SetParent(root,false);go.transform.localPosition=eye;go.transform.LookAt(root.TransformPoint(target));
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=size;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<28;camera.nearClipPlane=.1f;camera.farClipPlane=10;
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){name="Circuit generated studio",antiAliasing=4};rt.Create();camera.targetTexture=rt;renders.Add(rt);cameras.Add(camera);
            var lamp=new GameObject("Studio softbox");lamp.transform.SetParent(root,false);lamp.transform.localPosition=new Vector3(-2,4,-3);
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=9;light.intensity=3.4f;light.cullingMask=1<<28;light.shadows=LightShadows.None;return camera;
        }
        static Vector3 Normal(int face)=>face==1?Vector3.back:face==2?Vector3.left:face==3?Vector3.down:face==4?Vector3.up:face==5?Vector3.right:Vector3.forward;
        static IEnumerable<Vector2> Pips(int face)
        {
            const float d=.245f;
            if(face%2==1)yield return Vector2.zero;
            if(face>=2){yield return new Vector2(-d,-d);yield return new Vector2(d,d);}
            if(face>=4){yield return new Vector2(-d,d);yield return new Vector2(d,-d);}
            if(face==6){yield return new Vector2(-d,0);yield return new Vector2(d,0);}
        }
        static Quaternion DiceResultRotation(int value,int die)
        {
            return Quaternion.AngleAxis(die==0?-12:15,Vector3.up)*Quaternion.FromToRotation(Normal(value),Vector3.up);
        }
        static float DiceSupport(Quaternion rotation)
        {
            // The cube has a .4-unit inner core and rounded .1-unit corners.
            // Keep its lowest point, including the pips, above the tabletop at every rotation.
            return .4f*(Mathf.Abs((rotation*Vector3.right).y)+Mathf.Abs((rotation*Vector3.up).y)+Mathf.Abs((rotation*Vector3.forward).y))+.115f;
        }
        public void SetDice(int a,int b)
        {
            for(int i=0;i<2;i++){var rotation=DiceResultRotation(i==0?a:b,i);dice[i].localRotation=rotation;diceAirHeight[i]=0;dice[i].localPosition=new Vector3(i==0?-.72f:.72f,DiceGroundY+DiceSupport(rotation),0);}
            if(diceCamera!=null)diceCamera.orthographicSize=1.22f;
        }
        public Vector3 DiceGroundViewport(int die)
        {
            if(diceCamera==null || dice[die]==null)return new Vector3(.5f,.5f,0);
            var p=dice[die].localPosition;p.y=DiceGroundY;return diceCamera.WorldToViewportPoint(dice[die].parent.TransformPoint(p));
        }
        public float DiceHeight(int die)=>dice[die]==null?0:diceAirHeight[die];
        public float DiceGroundClearance(int die)=>dice[die]==null?0:dice[die].localPosition.y-DiceSupport(dice[die].localRotation)-DiceGroundY;
        public bool DiceFacesMatch(int a,int b)
        {
            return Vector3.Dot(dice[0].localRotation*Normal(a),Vector3.up)>.999f && Vector3.Dot(dice[1].localRotation*Normal(b),Vector3.up)>.999f;
        }
        public void AnimateDice(float progress,int a,int b)
        {
            for(int i=0;i<2;i++)
            {
                var p=DiceThrowMotion.Sample(progress,i,ReducedMotion);
                var result=DiceResultRotation(i==0?a:b,i);
                var spin=Quaternion.Euler(p.SpinX,p.SpinY,p.SpinZ)*result;
                var rotation=Quaternion.Slerp(spin,result,p.Settle);dice[i].localRotation=rotation;diceAirHeight[i]=p.Y;
                dice[i].localPosition=new Vector3(p.X,DiceGroundY+DiceSupport(rotation)+p.Y,p.Z);
                if(diceCamera!=null)diceCamera.orthographicSize=p.CameraSize;
            }
        }
        void LateUpdate()
        {
            for(int i=0;i<4;i++)if(robots[i]!=null)robots[i].localRotation=i==0 && previewFixed?Quaternion.Euler(0,previewYaw+(people[i]!=null?180:0),0):Quaternion.Euler(0,(people[i]!=null?162:-18)+(ReducedMotion?0:Mathf.Sin(Time.time*1.4f+i)*6),(ReducedMotion?0:Mathf.Sin(Time.time*1.8f+i)*1.5f)+fallPose[i]*(ReducedMotion?15:75));
            foreach(var camera in cameras)camera.Render();
        }
        static Mesh RoundedBox()
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();const int resolution=8;
            for(int face=1;face<=6;face++)
            {
                Vector3 n=Normal(face),v=Mathf.Abs(n.y)>.5f?Vector3.forward:Vector3.up,u=Vector3.Cross(v,n);int start=vertices.Count;
                for(int y=0;y<=resolution;y++)for(int x=0;x<=resolution;x++)
                {
                    Vector3 point=n*.5f+u*(x/(float)resolution-.5f)+v*(y/(float)resolution-.5f);
                    Vector3 inner=new Vector3(Mathf.Clamp(point.x,-.40f,.40f),Mathf.Clamp(point.y,-.40f,.40f),Mathf.Clamp(point.z,-.40f,.40f));
                    var normal=(point-inner).normalized;vertices.Add(inner+normal*.10f);normals.Add(normal);
                }
                for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++){int a=start+y*(resolution+1)+x,b=a+1,c=a+resolution+1,d=c+1;triangles.AddRange(new[]{a,b,d,a,d,c});}
            }
            var mesh=new Mesh{name="Hand-built rounded box"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy()
        {
            foreach(var camera in cameras)if(camera!=null)camera.targetTexture=null;
            foreach(var rt in renders){rt.Release();Destroy(rt);}foreach(var material in materials)Destroy(material);if(roundedBox!=null)Destroy(roundedBox);
        }
    }
}
