using System;
using System.Collections.Generic;
using Marble.Core;
using UnityEngine;
using UnityEngine.Rendering;
namespace Marble.Unity
{
    // Uses the user's downloaded Synty slab mesh, fitted to the existing tile footprints.
    public sealed class BoardFloorLayer : MonoBehaviour
    {
        const string Resource="Downloaded/Synty/Floor1x1";
        const float Thickness=6,ProjectionScale=1.41421356237f;
        static int nextStudio;
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Mesh> caps=new List<Mesh>();
        readonly List<Mesh> panels=new List<Mesh>();
        readonly Vector2[] surfaceCenters=new Vector2[32];
        readonly Vector2[] signatureCenters=new Vector2[32];
        readonly List<Material> materials=new List<Material>();
        readonly Material[] panelMaterials=new Material[32];
        Func<int,Color> tileColor,tileAccent;Camera renderCamera;RenderTexture target;Transform studio;MiniatureBuildings miniatures;
        public bool Ready=>target!=null && meshes.Count==32;
        public int TileCount=>meshes.Count;
        public int SignaturePanelCount=>panels.Count;
        public bool HasSignaturePanel(int index)=>index>=0 && index<panelMaterials.Length && panelMaterials[index]!=null;
        public int SourceVertexCount{get;private set;}
        public float TopAlignmentError{get;private set;}
        public bool UsesMiniatures=>miniatures!=null && miniatures.Ready;
        public int VisibleMiniatures=>miniatures==null?0:miniatures.VisibleCount;
        public int BuildingSourceVertices=>miniatures==null?0:miniatures.SourceVertices;
        public bool MiniatureVisible(int tile,int level)=>miniatures!=null && miniatures.IsVisible(tile,level);
        public bool MiniaturesFaceInward=>miniatures!=null && miniatures.Ready && miniatures.VisibleFaceInward();
        public Vector2 SurfaceCenter(int index)=>surfaceCenters[index];
        public Vector2 SignatureCenter(int index)=>signatureCenters[index];
        public void Initialize(Func<int,Color> color,Func<int,Color> accent,Game game,Func<int,Color> playerColor)
        {
            tileColor=color;tileAccent=accent;
            var model=Resources.Load<GameObject>(Resource);
            var filter=model==null?null:model.GetComponentInChildren<MeshFilter>();
            var shader=Resources.Load<Shader>("BoardFloor");
            if(filter==null || filter.sharedMesh==null || !filter.sharedMesh.isReadable || shader==null)
            {Debug.LogError("Downloaded board floor mesh/shader unavailable.");return;}
            var source=filter.sharedMesh;SourceVertexCount=source.vertexCount;
            if(source.bounds.size.x<=0 || source.bounds.size.y<=0 || source.bounds.size.z<=0)
            {Debug.LogError("Downloaded board floor must have a solid volume.");return;}
            studio=new GameObject("Synty floor studio").transform;studio.SetParent(transform,false);studio.localPosition=new Vector3(10000*(++nextStudio),-3000,0);
            for(int index=0;index<32;index++)
            {
                var mesh=Instantiate(source);mesh.name="Synty floor tile "+index;
                var vertices=source.vertices;for(int v=0;v<vertices.Length;v++)vertices[v]=Fit(index,vertices[v],source.bounds);
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                var material=new Material(shader){name="Floor color "+index,color=tileColor(index)};materials.Add(material);
                var slab=new GameObject("Floor 1x1 · "+index);slab.layer=27;slab.transform.SetParent(studio,false);
                slab.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=slab.AddComponent<MeshRenderer>();
                var slots=new Material[mesh.subMeshCount];for(int s=0;s<slots.Length;s++)slots[s]=material;
                renderer.sharedMaterials=slots;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                var cap=CreateTileCap(index,out surfaceCenters[index]);caps.Add(cap);
                var surface=new GameObject("Flat tile surface · "+index);surface.layer=27;surface.transform.SetParent(studio,false);
                surface.AddComponent<MeshFilter>().sharedMesh=cap;
                var topRenderer=surface.AddComponent<MeshRenderer>();topRenderer.sharedMaterial=material;
                topRenderer.shadowCastingMode=ShadowCastingMode.Off;topRenderer.receiveShadows=false;
                if(index%8!=0)
                {
                    var kind=game.Tiles[index].Definition.Kind;
                    bool showPanel=kind!=TileKind.Chance && kind!=TileKind.Resort;
                    // Still compute the inner anchor for icons, text and resort miniatures.
                    var panel=CreateColorPanel(index,out signatureCenters[index],showPanel);
                    if(showPanel)
                    {
                        panels.Add(panel);
                        var panelMaterial=new Material(shader){name="Line color tile "+index,color=tileAccent(index)};panelMaterials[index]=panelMaterial;
                        var coloredTile=new GameObject("Raised signature color tile · "+index);coloredTile.layer=27;coloredTile.transform.SetParent(studio,false);
                        coloredTile.AddComponent<MeshFilter>().sharedMesh=panel;
                        var panelRenderer=coloredTile.AddComponent<MeshRenderer>();panelRenderer.sharedMaterial=panelMaterial;
                        panelRenderer.shadowCastingMode=ShadowCastingMode.Off;panelRenderer.receiveShadows=false;
                    }
                }
            }
            miniatures=gameObject.AddComponent<MiniatureBuildings>();miniatures.Initialize(game,studio,SignatureCenter,playerColor);miniatures.Refresh();
            target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32){name="Synty board slabs",antiAliasing=4,filterMode=FilterMode.Bilinear};target.Create();
            var cameraRoot=new GameObject("Floor orthographic camera");cameraRoot.transform.SetParent(studio,false);
            cameraRoot.transform.localPosition=new Vector3(0,1400,-1400);cameraRoot.transform.localRotation=Quaternion.Euler(45,0,0);
            renderCamera=cameraRoot.AddComponent<Camera>();renderCamera.orthographic=true;renderCamera.orthographicSize=450;
            renderCamera.aspect=1440f/900;renderCamera.nearClipPlane=1;renderCamera.farClipPlane=4000;
            renderCamera.clearFlags=CameraClearFlags.SolidColor;renderCamera.backgroundColor=Color.clear;
            renderCamera.cullingMask=1<<27;renderCamera.targetTexture=target;renderCamera.allowHDR=false;renderCamera.allowMSAA=true;
            renderCamera.enabled=false;
            // Validate camera-space agreement with the rule engine's tile footprints.
            for(int i=0;i<32;i++)for(int v=0;v<4;v++)
            {
                var point=Vertex(i,v);var projected=renderCamera.WorldToViewportPoint(studio.TransformPoint(Plane(point,0)));
                TopAlignmentError=Mathf.Max(TopAlignmentError,Vector2.Distance(point,new Vector2(projected.x*1440,(1-projected.y)*900)));
            }
            renderCamera.Render();
        }
        static Vector2 Vertex(int index,int vertex){DiamondBoardLayout.Vertex(index,vertex,out float x,out float y);return new Vector2(x,y);}
        static Vector3 Plane(Vector2 gui,float height)=>new Vector3(gui.x-720,height,(450-gui.y)*ProjectionScale);
        static Vector3 Fit(int index,Vector3 source,Bounds bounds)
        {
            float u=(source.x-bounds.min.x)/bounds.size.x,v=(source.z-bounds.min.z)/bounds.size.z;
            var a=Vertex(index,0);var point=a+(Vertex(index,1)-a)*u+(Vertex(index,3)-a)*v;
            // Recess the original raised rim beneath the flat playing surface.
            float height=((source.y-bounds.max.y)/bounds.size.y*Thickness-2)*ProjectionScale;
            return Plane(point,height);
        }
        static List<Vector2> TilePolygon(int index)
        {
            var polygon=new List<Vector2>();for(int i=0;i<4;i++)polygon.Add(Vertex(index,i));
            return polygon;
        }
        static Mesh CreateTileCap(int index,out Vector2 center)
        {
            var polygon=TilePolygon(index);center=Vector2.zero;foreach(var p in polygon)center+=p;center/=polygon.Count;
            return BeveledSurface(polygon,"Flat beveled board tile "+index,0,-.8f,-8,1.8f,.8f);
        }
        static Mesh CreateColorPanel(int index,out Vector2 panelCenter,bool createMesh)
        {
            var polygon=TilePolygon(index);var center=Vector2.zero;foreach(var p in polygon)center+=p;center/=4;
            for(int i=0;i<4;i++)polygon[i]+=(center-polygon[i]).normalized*3;
            int edge=index/8;
            var a=polygon[edge];var b=polygon[(edge+1)%4];var innerA=polygon[(edge+3)%4];var innerB=polygon[(edge+2)%4];
            var strip=new List<Vector2>{Vector2.Lerp(a,innerA,.025f),Vector2.Lerp(b,innerB,.025f),Vector2.Lerp(b,innerB,.28f),Vector2.Lerp(a,innerA,.28f)};
            panelCenter=Vector2.zero;foreach(var p in strip)panelCenter+=p;panelCenter/=4;
            return createMesh?BeveledSurface(strip,"Signature color tile "+index,.9f,.5f,.05f,.7f,.25f):null;
        }
        static Mesh BeveledSurface(List<Vector2> polygon,string name,float top,float bevel,float bottom,float innerInset,float outerInset)
        {
            var center=Vector2.zero;foreach(var p in polygon)center+=p;center/=polygon.Count;
            int count=polygon.Count;var vertices=new List<Vector3>();var triangles=new List<int>();
            // A broad flat top, a small bevel, and a shallow solid side replace the standing rim.
            for(int ring=0;ring<3;ring++)for(int i=0;i<count;i++)
            {
                var inward=(center-polygon[i]).normalized;
                var point=polygon[i]+inward*(ring==0?innerInset:outerInset);
                vertices.Add(Plane(point,(ring==0?top:ring==1?bevel:bottom)*ProjectionScale));
            }
            for(int i=1;i<count-1;i++){triangles.Add(0);triangles.Add(i);triangles.Add(i+1);}
            for(int ring=0;ring<2;ring++)for(int i=0;i<count;i++)
            {
                int next=(i+1)%count,a=ring*count+i,b=(ring+1)*count+i,c=(ring+1)*count+next,d=ring*count+next;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);triangles.Add(a);triangles.Add(c);triangles.Add(d);
            }
            // Separate faces keep the center flat instead of smoothing it into a shallow bowl.
            var flat=new Vector3[triangles.Count];var indices=new int[triangles.Count];
            for(int i=0;i<indices.Length;i++){flat[i]=vertices[triangles[i]];indices[i]=i;}
            var mesh=new Mesh{name=name};mesh.vertices=flat;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void Draw()
        {if(Ready && Event.current.type==EventType.Repaint)GUI.DrawTexture(new Rect(0,0,1440,900),target,ScaleMode.StretchToFill,true);}
        void LateUpdate()
        {
            if(!Ready)return;
            for(int i=0;i<materials.Count;i++)materials[i].color=tileColor(i);
            for(int i=0;i<panelMaterials.Length;i++)if(panelMaterials[i]!=null)panelMaterials[i].color=tileAccent(i);
            miniatures.Refresh();
            renderCamera.Render();
        }
        void OnDestroy()
        {
            if(renderCamera!=null)renderCamera.targetTexture=null;
            if(target!=null){target.Release();Destroy(target);}
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            foreach(var mesh in caps)if(mesh!=null)Destroy(mesh);
            foreach(var mesh in panels)if(mesh!=null)Destroy(mesh);
            foreach(var material in materials)if(material!=null)Destroy(material);
            foreach(var material in panelMaterials)if(material!=null)Destroy(material);
        }
    }
}
