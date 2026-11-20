using System;
using System.Collections.Generic;
using System.Linq;
using Marble.Core;
using UnityEngine;
namespace Marble.Unity
{
    // Assembles only meshes from the user's Sidekick pack; no package tools are executed.
    public sealed class SidekickAvatar : MonoBehaviour
    {
        public static readonly string[] SkinHex={"D5A57B","F3D4B4","B78258","8C563B","603C30","E6BA95"};
        public static readonly string[] HairNames={"없음","숏컷 1","숏컷 2","스타일 3","스타일 4","스타일 5","스타일 6","스타일 7","스타일 8","스타일 9","스타일 10"};
        public static readonly string[] HatNames={"없음","캐주얼 모자","기사 투구","장비 모자","후드"};
        public static readonly string[] ClothingNames={"베이스","캐주얼","기사"};
        public static readonly string[] AccessoryNames={"없음","페이스 장비","백팩","기사 등 장비","캐주얼 장비 세트","기사 어깨 장비"};
        static readonly Dictionary<string,GameObject> models=new Dictionary<string,GameObject>();
        readonly List<SkinnedMeshRenderer> parts=new List<SkinnedMeshRenderer>();
        readonly List<int> partTypes=new List<int>();
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
        readonly List<GameObject> prosthetics=new List<GameObject>();
        readonly Dictionary<string,Material> clothingMaterials=new Dictionary<string,Material>();
        Material material,metal;Texture2D palette;int outfit;
        public int PartCount=>parts.Count;
        public int Outfit=>outfit;
        public static bool Available=>Model("SK_HUMN_BASE_01_01HEAD_HU01")!=null;
        static GameObject Model(string name)
        {
            if(!models.TryGetValue(name,out var model)){model=Resources.Load<GameObject>("Downloaded/Sidekick/"+name);models[name]=model;}
            return model;
        }
        static string Prefix(int style)=>style==2?"SK_FANT_KNGT_17_":style==1?"SK_SCFI_CIVL_09_":"SK_HUMN_BASE_01_";
        public void Initialize(int code)
        {
            outfit=WardrobeCatalog.IsValid(code)?code:WardrobeCatalog.DefaultCode;
            var sourcePalette=Resources.Load<Texture2D>("Downloaded/Sidekick/T_Starter_01ColorMap");
            palette=new Texture2D(sourcePalette.width,sourcePalette.height,TextureFormat.RGBA32,false){name="Wardrobe skin palette",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=sourcePalette.GetPixels32();var skin=MarbleBoardView.Hex(SkinHex[WardrobeCatalog.Get(outfit,1)]);
            for(int i=0;i<pixels.Length;i++)
            {
                if(pixels[i].r==213 && pixels[i].g==165 && pixels[i].b==123)pixels[i]=skin;
                else if(pixels[i].r==187 && pixels[i].g==139 && pixels[i].b==103)pixels[i]=Color.Lerp(skin,Color.black,.16f);
            }
            palette.SetPixels32(pixels);palette.Apply();
            material=new Material(Resources.Load<Shader>("SidekickPalette")){name="Sidekick wardrobe fabric",mainTexture=palette};
            metal=new Material(Shader.Find("Standard")){name="Artificial body parts",color=MarbleBoardView.Hex("A5B1B7")};metal.SetFloat("_Glossiness",.3f);
            var head=Instantiate(Model("SK_HUMN_BASE_01_01HEAD_HU01"),transform,false);head.name="Sidekick shared skeleton";
            foreach(var bone in head.GetComponentsInChildren<Transform>(true))if(!bones.ContainsKey(bone.name))bones.Add(bone.name,bone);
            foreach(var renderer in head.GetComponentsInChildren<SkinnedMeshRenderer>(true))Register(renderer,1);
            AddBase("03EBRL",3);AddBase("04EBRR",4);AddBase("05EYEL",5);AddBase("06EYER",6);AddBase("07EARL",7);AddBase("08EARR",8);AddBase("35NOSE",35);AddBase("36TETH",36);AddBase("37TONG",37);
            int hair=WardrobeCatalog.Get(outfit,2),hat=WardrobeCatalog.Get(outfit,3);
            if(hair>0 && hat==0)Add("SK_HUMN_BASE_"+hair.ToString("D2")+"_02HAIR_HU01",2);
            int top=WardrobeCatalog.Get(outfit,4),pants=WardrobeCatalog.Get(outfit,5),shoes=WardrobeCatalog.Get(outfit,6);
            foreach(var pair in new[]{("10TORS",10),("11AUPL",11),("12AUPR",12),("13ALWL",13),("14ALWR",14),("15HNDL",15),("16HNDR",16)})Add(Prefix(top)+pair.Item1+"_HU01",pair.Item2);
            foreach(var pair in new[]{("17HIPS",17),("18LEGL",18),("19LEGR",19)})Add(Prefix(pants)+pair.Item1+"_HU01",pair.Item2);
            Add(Prefix(shoes)+"20FOTL_HU01",20);Add(Prefix(shoes)+"21FOTR_HU01",21);
            if(pants==0)AddBase("38WRAP",38);
            if(hat>0)Add(new[]{"","SK_SCFI_CIVL_09_22AHED_HU01","SK_FANT_KNGT_17_22AHED_HU01","SK_SCFI_CIVL_10_22AHED_HU01","SK_HORR_VILN_01_22AHED_HU01"}[hat],22);
            int accessory=WardrobeCatalog.Get(outfit,7);
            if(accessory==1)Add("SK_SCFI_CIVL_09_23AFAC_HU01",23);
            if(accessory==2)Add("SK_SCFI_CIVL_09_24ABAC_HU01",24);
            if(accessory==3)Add("SK_FANT_KNGT_17_24ABAC_HU01",24);
            if(accessory>=4){string p=accessory==4?"SK_SCFI_CIVL_09_":"SK_FANT_KNGT_17_";Add(p+"29ASHL_HU01",29);Add(p+"30ASHR_HU01",30);}
            RelaxArm("upperarm_l","lowerarm_l");RelaxArm("upperarm_r","lowerarm_r");
            transform.localScale=Vector3.one*1.12f;
            Prosthetic("Artificial left leg",new Vector3(.095f,.3f,0),new Vector3(.065f,.25f,.065f));
            Prosthetic("Artificial right leg",new Vector3(-.095f,.3f,0),new Vector3(.065f,.25f,.065f));
            Prosthetic("Artificial brain",new Vector3(0,1.73f,.01f),new Vector3(.1f,.04f,.1f));
            Prosthetic("Artificial kidney",new Vector3(.18f,.82f,.1f),Vector3.one*.08f);
            Prosthetic("Artificial lung",new Vector3(-.15f,1.13f,.19f),new Vector3(.1f,.17f,.09f));
            Prosthetic("Artificial heart",new Vector3(.12f,1.13f,-.18f),Vector3.one*.085f);
            SetBodyState(0);
        }
        void AddBase(string suffix,int type)=>Add("SK_HUMN_BASE_01_"+suffix+"_HU01",type);
        void Add(string name,int type)
        {
            var source=Model(name);if(source==null){Debug.LogError("Wardrobe mesh missing: "+name);return;}
            var instance=Instantiate(source,transform,false);
            foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.bones=renderer.bones.Select(SharedBone).ToArray();
                if(renderer.rootBone!=null)renderer.rootBone=SharedBone(renderer.rootBone);
                renderer.transform.SetParent(transform,true);Register(renderer,type);
            }
            if(!parts.Any(p=>p.gameObject==instance))Destroy(instance);
        }
        Transform SharedBone(Transform source)
        {
            if(source==null)return null;
            if(bones.TryGetValue(source.name,out var found))return found;
            var parent=source.parent==null?transform:SharedBone(source.parent);
            var clone=new GameObject(source.name).transform;clone.SetParent(parent,false);
            clone.localPosition=source.localPosition;clone.localRotation=source.localRotation;clone.localScale=source.localScale;
            bones.Add(source.name,clone);return clone;
        }
        void Register(SkinnedMeshRenderer renderer,int type)
        {
            renderer.gameObject.layer=28;renderer.sharedMaterials=Enumerable.Repeat(PartMaterial(type),Math.Max(1,renderer.sharedMesh.subMeshCount)).ToArray();renderer.updateWhenOffscreen=true;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            var mesh=renderer.sharedMesh;
            for(int i=0;i<mesh.blendShapeCount;i++)if(mesh.GetBlendShapeName(i).EndsWith("masculineFeminine",StringComparison.OrdinalIgnoreCase))renderer.SetBlendShapeWeight(i,WardrobeCatalog.Get(outfit,0)==0?-100:100);
            parts.Add(renderer);partTypes.Add(type);
        }
        Material Flat(string hex)
        {
            if(!clothingMaterials.TryGetValue(hex,out var result))
            {result=new Material(Resources.Load<Shader>("SidekickPalette")){name="Wardrobe fabric "+hex,mainTexture=Texture2D.whiteTexture,color=MarbleBoardView.Hex(hex)};clothingMaterials.Add(hex,result);}
            return result;
        }
        Material PartMaterial(int type)
        {
            // Keep detailed skin/eye palette entries, but give separate clothing pieces
            // clean fabric colors instead of unrelated starter-set palette regions.
            if(type==2 || type==3 || type==4)return Flat("3E3027");
            int top=WardrobeCatalog.Get(outfit,4),pants=WardrobeCatalog.Get(outfit,5),shoes=WardrobeCatalog.Get(outfit,6);
            if(type>=10 && type<=14)return top==0?material:Flat(top==1?"537E99":"8C9DA8");
            if(type==15 || type==16)return top==2?Flat("6E7F8A"):material;
            if(type>=17 && type<=19)return pants==0?(type==17?Flat("344B58"):material):Flat(pants==1?"344B58":"778995");
            if(type==20 || type==21)return shoes==0?material:Flat(shoes==1?"705947":"8C9DA8");
            if(type==22)return Flat(WardrobeCatalog.Get(outfit,3)==2?"8C9DA8":"4B6278");
            if(type==23)return Flat("BD9A58");
            if(type==24)return Flat(WardrobeCatalog.Get(outfit,7)==3?"864F48":"705947");
            if(type==29 || type==30)return Flat("8C9DA8");
            if(type==38)return Flat("344B58");
            return material;
        }
        void RelaxArm(string upperName,string lowerName)
        {
            if(!bones.TryGetValue(upperName,out var upper) || !bones.TryGetValue(lowerName,out var lower))return;
            var direction=lower.position-upper.position;
            upper.rotation=Quaternion.FromToRotation(direction.normalized,new Vector3(Mathf.Sign(direction.x)*.15f,-1,0).normalized)*upper.rotation;
        }
        void Prosthetic(string name,Vector3 position,Vector3 scale)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cylinder);part.name=name;part.layer=28;part.transform.SetParent(transform,false);part.transform.localPosition=position;part.transform.localScale=scale;
            Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=metal;prosthetics.Add(part);
        }
        public void SetBodyState(int mask)
        {
            var block=new MaterialPropertyBlock();if((mask&128)!=0)block.SetColor("_Color",MarbleBoardView.Hex("89755B"));
            for(int i=0;i<parts.Count;i++)
            {
                int type=partTypes[i];bool hidden=type==5 && (mask&1)!=0 || type==6 && (mask&2)!=0 || (type==18 || type==20) && (mask&4)!=0 || (type==19 || type==21) && (mask&8)!=0 || (type==36 || type==37) && (mask&16)!=0;
                parts[i].enabled=!hidden;parts[i].SetPropertyBlock(block);
            }
            int[] flags={4,8,32,64,256,512};for(int i=0;i<flags.Length;i++)prosthetics[i].SetActive((mask&flags[i])!=0);
        }
        void OnDestroy(){if(material!=null)Destroy(material);if(metal!=null)Destroy(metal);if(palette!=null)Destroy(palette);foreach(var value in clothingMaterials.Values)if(value!=null)Destroy(value);}
    }
}
