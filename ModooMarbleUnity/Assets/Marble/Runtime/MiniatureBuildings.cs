using System;
using System.Collections.Generic;
using Marble.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Marble.Unity
{
    // Three-dimensional board miniatures use the user's Simple Town apartment model.
    public sealed class MiniatureBuildings : MonoBehaviour
    {
        const float ProjectionScale=1.41421356237f;
        readonly GameObject[,] buildings=new GameObject[32,4];
        readonly Renderer[,] flags=new Renderer[32,4];
        readonly List<Material> materials=new List<Material>();
        Game game;Transform studio;Func<int,Vector2> center;Func<int,Color> playerColor;
        GameObject apartment;Material house,roof,window,trim,apartmentMaterial,ownerMarker,sand,palm,trunk;
        MaterialPropertyBlock markerBlock;
        public bool Ready{get;private set;}
        public int VisibleCount{get;private set;}
        public int SourceVertices{get;private set;}
        public bool IsVisible(int tile,int level)=>tile>=0 && tile<32 && level>=1 && level<=4 && buildings[tile,level-1]!=null && buildings[tile,level-1].activeSelf;
        public bool VisibleFaceInward()
        {
            for(int tile=0;tile<32;tile++)for(int level=0;level<4;level++)
            {
                var obj=buildings[tile,level];if(obj==null || !obj.activeSelf)continue;
                var target=studio.TransformPoint(Plane(new Vector2(DiamondBoardLayout.CenterX,DiamondBoardLayout.CenterY),0))-obj.transform.position;
                target.y=0;if(Vector3.Dot(obj.transform.forward,target)<=0)return false;
            }
            return true;
        }
        public void Initialize(Game model,Transform boardStudio,Func<int,Vector2> panelCenter,Func<int,Color> ownerColor)
        {
            game=model;studio=boardStudio;center=panelCenter;playerColor=ownerColor;
            apartment=Resources.Load<GameObject>("Downloaded/SimpleTown/apartment_large_thin_short");
            var palette=Resources.Load<Texture2D>("Downloaded/SimpleTown/STL_Building_Apartment_02");
            var filter=apartment==null?null:apartment.GetComponentInChildren<MeshFilter>();
            var surface=Resources.Load<Shader>("BoardFloor");var textured=Resources.Load<Shader>("SidekickPalette");
            if(filter==null || filter.sharedMesh==null || palette==null || surface==null || textured==null){Debug.LogError("Downloaded building model or material unavailable.");return;}
            SourceVertices=filter.sharedMesh.vertexCount;markerBlock=new MaterialPropertyBlock();
            house=Solid(surface,"F3EBD9");roof=Solid(surface,"B76852");window=Solid(surface,"466E79");trim=Solid(surface,"F1D194");ownerMarker=Solid(surface,"FFFFFF");
            sand=Solid(surface,"EBD6A3");palm=Solid(surface,"4C9569");trunk=Solid(surface,"9C704E");
            apartmentMaterial=new Material(textured){name="Simple Town apartment palette",mainTexture=palette};materials.Add(apartmentMaterial);
            Ready=true;
        }
        Material Solid(Shader shader,string color)
        {var m=new Material(shader){name="Miniature "+color,color=MarbleBoardView.Hex(color)};materials.Add(m);return m;}
        static Vector3 Plane(Vector2 p,float y)=>new Vector3(p.x-720,y,(450-p.y)*ProjectionScale);
        static Vector2 Vertex(int i,int v){DiamondBoardLayout.Vertex(i,v,out float x,out float y);return new Vector2(x,y);}
        Transform Cube(Transform parent,string name,Vector3 pos,Vector3 size,Material mat,Vector3 rotation=default)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.layer=27;Destroy(obj.GetComponent<Collider>());
            var t=obj.transform;t.SetParent(parent,false);t.localPosition=pos;t.localScale=size;t.localRotation=Quaternion.Euler(rotation);
            var r=obj.GetComponent<Renderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;
        }
        void Create(int index,int level)
        {
            var root=new GameObject("Miniature "+index+" / "+level);root.layer=27;root.transform.SetParent(studio,false);
            int edge=index/8;var a=Vertex(index,edge);var b=Vertex(index,(edge+1)%4);var tangent=(b-a).normalized;
            bool resort=game.Tiles[index].Definition.Kind==TileKind.Resort;
            var anchor=center(index)+(level==4 || resort?Vector2.zero:tangent*((level-2)*16));
            var inward=(a+b-Vertex(index,(edge+2)%4)-Vertex(index,(edge+3)%4))*.5f;
            var platform=Cube(root.transform,"Owner color plinth",new Vector3(0,1.1f,0),new Vector3(19,2,16),ownerMarker);
            flags[index,level-1]=platform.GetComponent<Renderer>();
            if(resort)
            {
                Cube(root.transform,"Resort sand",new Vector3(0,2.8f,0),new Vector3(16,1.5f,12),sand);
                Cube(root.transform,"Palm trunk",new Vector3(0,9,0),new Vector3(2.5f,12,2.5f),trunk,new Vector3(0,0,-8));
                Cube(root.transform,"Palm leaf left",new Vector3(-4,15.5f,0),new Vector3(9,1.3f,3.5f),palm,new Vector3(0,0,14));
                Cube(root.transform,"Palm leaf right",new Vector3(4,15.5f,0),new Vector3(9,1.3f,3.5f),palm,new Vector3(0,0,-14));
                Cube(root.transform,"Palm leaf front",new Vector3(0,15.5f,4),new Vector3(3.5f,1.3f,9),palm,new Vector3(14,0,0));
                Cube(root.transform,"Palm leaf back",new Vector3(0,15.5f,-4),new Vector3(3.5f,1.3f,9),palm,new Vector3(-14,0,0));
            }
            else if(level==1)
            {
                Cube(root.transform,"Villa walls",new Vector3(0,9,0),new Vector3(14,15,12),house);
                Cube(root.transform,"Villa roof left",new Vector3(-4.1f,18,0),new Vector3(11,2,16),roof,new Vector3(0,0,35));
                Cube(root.transform,"Villa roof right",new Vector3(4.1f,18,0),new Vector3(11,2,16),roof,new Vector3(0,0,-35));
                Cube(root.transform,"Door",new Vector3(0,6,6.1f),new Vector3(3.5f,7.5f,.5f),roof);
                Cube(root.transform,"Left window",new Vector3(-4.8f,11,6.1f),new Vector3(3,4,.5f),window);
                Cube(root.transform,"Right window",new Vector3(4.8f,11,6.1f),new Vector3(3,4,.5f),window);
            }
            else
            {
                var model=Instantiate(apartment,root.transform,false);model.name="Simple Town building";
                var renderers=model.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;
                foreach(var r in renderers)
                {
                    bounds.Encapsulate(r.bounds);r.gameObject.layer=27;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                    var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=apartmentMaterial;r.sharedMaterials=slots;
                }
                var localCenter=bounds.center-root.transform.position;var size=bounds.size;
                float desiredHeight=level==2?29:level==3?39:47;
                float horizontal=18/Mathf.Max(.01f,Mathf.Max(size.x,size.z)),vertical=desiredHeight/Mathf.Max(.01f,size.y);
                model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(horizontal,vertical,horizontal));
                model.transform.localPosition=new Vector3(-localCenter.x*horizontal,-(bounds.min.y-root.transform.position.y)*vertical+2,-localCenter.z*horizontal);
                float actualHeight=size.y*vertical;
                if(level>=3)Cube(root.transform,"Hotel roof accent",new Vector3(0,actualHeight+2,0),new Vector3(15,2,13),trim);
                if(level==4)
                {
                    Cube(root.transform,"Landmark crown",new Vector3(0,actualHeight+4,0),new Vector3(11,4,9),trim);
                    Cube(root.transform,"Landmark flag pole",new Vector3(0,actualHeight+11,0),new Vector3(1,12,1),roof);
                    Cube(root.transform,"Landmark flag",new Vector3(4,actualHeight+15,0),new Vector3(8,4,1),trim);
                }
            }
            root.transform.localPosition=Plane(anchor,1.5f*ProjectionScale);
            root.transform.localRotation=Quaternion.LookRotation(new Vector3(inward.x,0,-inward.y*ProjectionScale).normalized,Vector3.up);
            buildings[index,level-1]=root;
        }
        public void Refresh()
        {
            if(!Ready)return;VisibleCount=0;
            for(int i=0;i<32;i++)
            {
                var t=game.Tiles[i];if(i%8==0)continue;
                for(int level=1;level<=4;level++)
                {
                    bool show=t.OwnerIndex>=0 && (t.Landmark?level==4:t.Definition.Kind==TileKind.City?t.HasBuilding(level):t.Definition.Kind==TileKind.Resort && level==1);
                    if(show && buildings[i,level-1]==null)Create(i,level);
                    var obj=buildings[i,level-1];if(obj==null)continue;
                    if(obj.activeSelf!=show)obj.SetActive(show);
                    if(show){VisibleCount++;markerBlock.Clear();markerBlock.SetColor("_Color",playerColor(t.OwnerIndex));flags[i,level-1].SetPropertyBlock(markerBlock);}
                }
            }
        }
        void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
    }
}
