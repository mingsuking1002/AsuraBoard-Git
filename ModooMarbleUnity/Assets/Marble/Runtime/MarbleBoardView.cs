using System;
using System.Collections.Generic;
using System.Linq;
using Marble.Core;
using UnityEngine;

namespace Marble.Unity
{
    // Original board art with a user-downloaded Synty floor mesh. No extracted game artwork.
    public sealed class MarbleBoardView : MonoBehaviour
    {
        public static readonly Color[] PlayerColors={Hex("318CAB"),Hex("DC715C"),Hex("6D9850"),Hex("BA8A3D")};
        public static readonly Color[] GroupColors={Hex("A8BA66"),Hex("5EA888"),Hex("64B7BF"),Hex("628EBC"),Hex("BB8BAA"),Hex("9685BA"),Hex("D4B260"),Hex("D88767")};
        public static float TileSize=>DiamondBoardLayout.StepLength(1);
        const float DiamondHalfWidth=DiamondBoardLayout.HalfWidth,DiamondHalfHeight=DiamondBoardLayout.HalfHeight;
        public bool ReducedMotion;
        public bool IsAnimating=>moving || diceTime>0 || diceResultHold>0 || cameraZoom>1.001f;
        public bool DiceInFlight=>diceTime>0 && !ReducedMotion;
        public Vector2 DisplayedPawnPosition(int seat)=>display[seat];
        public Vector2 VisiblePawnPosition(int seat)=>display[seat]-Offset(seat)+pawnOffsets[seat];
        public float DiceMaximumHeight=>models==null?0:Mathf.Max(models.DiceHeight(0),models.DiceHeight(1));
        public float DiceMinimumClearance=>models==null?0:Mathf.Min(models.DiceGroundClearance(0),models.DiceGroundClearance(1));
        public bool DiceFacesMatchResult=>models!=null && models.DiceFacesMatch(resultA,resultB);
        float cameraZoom=1f; Vector2 cameraFocus=new Vector2(720,450); int followSeat=-1;
        public Matrix4x4 ViewTransform
        {
            get
            {
                var center=new Vector2(720,450);var shift=center*(1-cameraZoom)-(cameraFocus-center)*((cameraZoom-1)*1.5f);
                return Matrix4x4.TRS(new Vector3(shift.x,shift.y,0),Quaternion.identity,new Vector3(cameraZoom,cameraZoom,1));
            }
        }
        public event Action StepSound;
        public event Action<int> DiceImpact;
        public RenderTexture DiceTexture=>models==null?null:models.DiceTexture;
        readonly Vector2[] display=new Vector2[4],starts=new Vector2[4],targets=new Vector2[4],pawnOffsets=new Vector2[4];
        readonly struct ViewHop
        {
            public readonly Vector2 Point;public readonly bool Fall,Transfer,Dice;public readonly string Reason;public readonly int From;
            public ViewHop(Vector2 point,bool fall=false,bool transfer=false,bool dice=false,string reason=null,int from=-1){Point=point;Fall=fall;Transfer=transfer;Dice=dice;Reason=reason;From=from;}
        }
        readonly Queue<ViewHop>[] routes=new Queue<ViewHop>[4];
        readonly bool[] targetFalls=new bool[4],targetTransfers=new bool[4],targetDice=new bool[4];
        readonly string[] targetReasons=new string[4];
        readonly int[] completedDiceSteps=new int[4],totalDiceSteps=new int[4];
        float eventPause;string eventPauseCaption;
        public bool EventMovementPaused=>eventPause>0;
        public int CompletedDiceSteps(int seat)=>completedDiceSteps[seat];
        public int TotalDiceSteps(int seat)=>totalDiceSteps[seat];
        long movementRevision;int presentationSeat;
        public string MovementCaption=>diceTime>0?"주사위 던지는 중…":diceResultHold>0?"주사위 결과 확인":eventPause>0?eventPauseCaption:moving && followSeat>=0?(targetDice[followSeat]?"주사위 이동 "+completedDiceSteps[followSeat]+" / "+totalDiceSteps[followSeat]+"칸":!string.IsNullOrEmpty(targetReasons[followSeat])?targetReasons[followSeat]:targetTransfers[followSeat]?"이벤트 위치 이동 중…":"이벤트 추가 이동 중…"):game.Message;
        public int PresentationSeat=>IsAnimating && presentationSeat>=0 && presentationSeat<game.Players.Count?presentationSeat:game.CurrentPlayerIndex;
        readonly float[] hop=new float[4];
        readonly int[] last=new int[4];readonly float[] fallTime=new float[4];
        CircuitModels models;
        BoardFloorLayer floors;
        public bool UsesDownloadedFloor=>floors!=null && floors.Ready;
        public int FloorTileCount=>floors==null?0:floors.TileCount;
        public int FloorSourceVertexCount=>floors==null?0:floors.SourceVertexCount;
        public int SignatureColorPanelCount=>floors==null?0:floors.SignaturePanelCount;
        public bool UsesMiniatureBuildings=>floors!=null && floors.UsesMiniatures;
        public int VisibleMiniatureBuildings=>floors==null?0:floors.VisibleMiniatures;
        public int BuildingSourceVertices=>floors==null?0:floors.BuildingSourceVertices;
        public bool MiniatureVisible(int tile,int level)=>floors!=null && floors.MiniatureVisible(tile,level);
        public bool MiniaturesFaceInward=>floors!=null && floors.MiniaturesFaceInward;
        public Vector2 SignatureColorPoint(int index)=>floors.SignatureCenter(index);
        public bool HasSignaturePanel(int index)=>floors!=null && floors.HasSignaturePanel(index);
        public float FloorTopAlignmentError=>floors==null?float.MaxValue:floors.TopAlignmentError;
        Game game;
        float diceTime,diceDuration,diceResultHold;int diceImpactMask;
        int resultA=1,resultB=1;
        bool moving;
        static readonly Color Ink=PaperTheme.Ink;
        public static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out Color c);return c;}
        public void Initialize(Game model)
        {
            game=model??throw new ArgumentNullException(nameof(model));
            if(game.Tiles.Count!=32)throw new InvalidOperationException("This board requires 32 tiles.");
            for(int i=0;i<game.Players.Count;i++)
            {last[i]=game.Players[i].Position;display[i]=Point(last[i])+Offset(i);targets[i]=display[i];hop[i]=1;routes[i]=new Queue<ViewHop>();}
            UpdatePawnOffsets(1);
            movementRevision=game.ActionMovementRevision;presentationSeat=game.CurrentPlayerIndex;
            resultA=Math.Max(1,game.DiceA);resultB=Math.Max(1,game.DiceB);
            models=gameObject.AddComponent<CircuitModels>();models.Initialize(game.Players.Select(p=>p.Avatar).Concat(Enumerable.Range(game.Players.Count,4-game.Players.Count)).ToArray(),game.Players.Select(p=>p.ColorIndex).Concat(Enumerable.Range(game.Players.Count,4-game.Players.Count)).ToArray(),game.Players.Select(p=>p.Outfit).Concat(Enumerable.Repeat(WardrobeCatalog.DefaultCode,4-game.Players.Count)).ToArray());models.SetDice(resultA,resultB);
            floors=gameObject.AddComponent<BoardFloorLayer>();floors.Initialize(TileFillColor,TileAccentColor,game,PlayerColor);
        }
        public Color PlayerColor(int seat)=>PlayerColors[game.Players[seat].ColorIndex];
        public static readonly Color[] LineColors={Hex("37B879"),Hex("459BDB"),Hex("B777CF"),Hex("EAA03D")};
        Color TileAccentColor(int index)=>LineColors[index/8];
        Color TileFillColor(int index)
        {
            var tile=game.Tiles[index];
            // Resorts carry the full color of their board edge; the plinth shows ownership.
            if(tile.Definition.Kind==TileKind.Resort)return TileAccentColor(index);
            Color surface=tile.Definition.Kind==TileKind.Bonus?Hex("FFF0C9"):PaperTheme.Paper;
            return tile.OwnerIndex>=0?Color.Lerp(surface,PlayerColor(tile.OwnerIndex),.12f):surface;
        }
        public Color TileSurfaceColor(int index)=>TileFillColor(index);
        public Texture CharacterPortrait(int i)=>models==null?null:models.Portrait(i);
        static Vector2 Offset(int i)=>new Vector2(i%2==0?-14:14,i<2?6:21);
        void UpdatePawnOffsets(float blend)
        {
            for(int i=0;i<game.Players.Count;i++)
            {
                var anchor=display[i]-Offset(i);int count=0,rank=0,otherRank=0,active=PresentationSeat;bool activeInGroup=false;
                for(int j=0;j<game.Players.Count;j++)
                {
                    if(game.Players[j].Bankrupt || ((display[j]-Offset(j))-anchor).sqrMagnitude>18*18)continue;
                    if(j==active)activeInGroup=true;
                    if(j<i){rank++;if(j!=active)otherRank++;}count++;
                }
                // Keep the moving player near the middle, so recentering cannot look like a backward step.
                if(activeInGroup && count>1)
                {
                    int activeSlot=(count-1)/2;
                    rank=i==active?activeSlot:otherRank>=activeSlot?otherRank+1:otherRank;
                }
                var target=new Vector2((rank-(Mathf.Max(count,1)-1)*.5f)*26,6);
                pawnOffsets[i]=Vector2.Lerp(pawnOffsets[i],target,blend);
            }
        }
        public static Vector2 CellCenter(int index){DiamondBoardLayout.Center(index,out float x,out float y);return new Vector2(x,y);}
        public static Rect CellRect(int index)
        {
            float minX=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxY=float.MinValue;
            for(int v=0;v<4;v++){var p=TileVertex(index,v);minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
            return Rect.MinMaxRect(minX,minY,maxX,maxY);
        }
        static Vector2 Point(int i)=>CellCenter(i);
        public Vector2 TileGuiPoint(int i)=>Point(i);
        public Vector2 TileLabelPoint(int i)=>UsesDownloadedFloor?floors.SurfaceCenter(i):Point(i);
        public Rect TileGuiRect(int i)=>CellRect(i);
        static Vector2 Inward(int index){var direction=new Vector2(720,440)-Point(index);return direction.sqrMagnitude>.001f?direction.normalized:Vector2.zero;}
        public Rect TileNameRect(int i){var p=TileLabelPoint(i);return new Rect(p.x-49,p.y-20,98,19);}
        public static int HitTestIndex(Vector2 point)=>DiamondBoardLayout.Hit(point.x,point.y);
        public int HitTest(Vector2 point)=>HitTestIndex(point);
        static void DiamondFill(Vector2 p,float width,float height,Color color)
        {
            var matrix=GUI.matrix;var transform=Matrix4x4.identity;
            transform.m00=width;transform.m01=-width;transform.m10=height;transform.m11=height;transform.m03=p.x;transform.m13=p.y;
            GUI.matrix=matrix*transform;CircuitArt.Fill(new Rect(-.5f,-.5f,1,1),color);GUI.matrix=matrix;
        }
        static Vector2 TileVertex(int index,int vertex)
        {DiamondBoardLayout.Vertex(index,vertex,out float x,out float y);return new Vector2(x,y);}
        static void TileSurface(int index,Color color)
        {
            var a=TileVertex(index,0);var b=TileVertex(index,1);var d=TileVertex(index,3);var center=(b+d)*.5f;
            var matrix=GUI.matrix;var transform=Matrix4x4.identity;
            transform.m00=b.x-a.x;transform.m10=b.y-a.y;transform.m01=d.x-a.x;transform.m11=d.y-a.y;transform.m03=center.x;transform.m13=center.y;
            GUI.matrix=matrix*transform;CircuitArt.Fill(new Rect(-.5f,-.5f,1,1),color);GUI.matrix=matrix;
        }
        static void TileFrame(int index,Color color,float width)
        {for(int v=0;v<4;v++)CircuitArt.Line(TileVertex(index,v),TileVertex(index,(v+1)%4),color,width);}
        public void DrawTileOutline(int index,Color color,float width=2)=>TileFrame(index,color,width);
        public void DrawBoard()
        {
            PaperTheme.Surface(new Rect(0,0,1440,900),PaperTheme.Table);
            for(int x=16;x<1440;x+=76)for(int y=79;y<838;y+=71)
            {
                float drift=Mathf.Sin(x*.17f+y*.09f)*11;
                CircuitArt.Line(new Vector2(x+drift,y+3),new Vector2(x+drift+3,y),new Color(.24f,.32f,.15f,.13f),1);
            }
            var middle=new Vector2(720,440);
            DiamondFill(middle+new Vector2(0,8),DiamondBoardLayout.OuterHalfWidth,DiamondBoardLayout.OuterHalfHeight,new Color(.13f,.16f,.10f,.35f));
            DiamondFill(middle,DiamondBoardLayout.OuterHalfWidth,DiamondBoardLayout.OuterHalfHeight,PaperTheme.Wash(PaperTheme.Table,.35f));
            DiamondFill(middle,DiamondBoardLayout.InnerHalfWidth,DiamondBoardLayout.InnerHalfHeight,PaperTheme.Wash(PaperTheme.Table,.52f));
            if(UsesDownloadedFloor)floors.Draw();
            var font=GUI.skin.font;
            // Draw corners last so their enlarged faces remain visible above adjacent cells.
            for(int pass=0;pass<2;pass++)for(int i=0;i<32;i++)
            {
                if((i%8==0)!=(pass==1))continue;
                var t=game.Tiles[i];var p=Point(i);
                Color band=t.Definition.Group>=0?GroupColors[t.Definition.Group%8]:Hex("72978C");
                if(!UsesDownloadedFloor)TileSurface(i,TileFillColor(i));
                // Buildings sit on the inner edge, leaving the land label and toll legible.
                var inward=Inward(i);var inner=UsesDownloadedFloor && i%8!=0?floors.SignatureCenter(i):TileLabelPoint(i)+inward*35;
                var edgeA=TileVertex(i,i/8);var edgeB=TileVertex(i,(i/8+1)%4);var tangent=(edgeB-edgeA).normalized;
                if(!floors.UsesMiniatures && t.Definition.Kind==TileKind.City)
                {
                    if(t.Landmark){DrawBuilding(inner,4,t.OwnerIndex<0?band:PlayerColor(t.OwnerIndex));CircuitArt.Label(new Rect(inner.x-30,inner.y+5,60,12),"랜드마크",GUI.skin.font,9,PaperTheme.Ink,true);}
                    else if(t.Buildings!=0)
                        for(int type=1;type<=3;type++)if(t.HasBuilding(type))
                            DrawBuilding(inner+tangent*((type-2)*16),type,t.OwnerIndex<0?band:PlayerColor(t.OwnerIndex));
                }
                else if(!floors.UsesMiniatures && t.Definition.Kind==TileKind.Resort && t.OwnerIndex>=0)DrawBuilding(inner,1,PlayerColor(t.OwnerIndex));
                if(i%8==0)DrawIcon(t.Definition.Kind,p+new Vector2(0,17),Hex("56756A"));
                else if(t.Definition.Kind==TileKind.Chance || t.Definition.Kind==TileKind.Bonus ||
                    (t.Definition.Kind==TileKind.Resort && t.OwnerIndex<0))
                    DrawSpecialTileBadge(t.Definition.Kind,inner,font);
            }
        }
        static void DrawSpecialTileBadge(TileKind kind,Vector2 p,Font font)
        {
            if(kind==TileKind.Chance)
            {
                // Stacked cards distinguish fortune tiles from a round reward or island.
                var back=new Rect(p.x-11,p.y-9,18,20);var front=new Rect(p.x-8,p.y-12,18,20);
                CircuitArt.Fill(back,Hex("CACACA"));CircuitArt.Outline(back,Hex("696969"),1);
                CircuitArt.Fill(front,Hex("FAFAFA"));CircuitArt.Outline(front,Hex("696969"),1.5f);
                CircuitArt.Label(front,"?",font,16,Hex("454545"),true);
            }
            else if(kind==TileKind.Bonus)
            {
                CircuitArt.Circle(new Rect(p.x-12,p.y-12,24,24),Hex("A97831"));
                CircuitArt.Circle(new Rect(p.x-10,p.y-10,20,20),Hex("F3C75E"));
                CircuitArt.Circle(new Rect(p.x-8,p.y-8,16,16),Hex("FFE8A3"));
                CircuitArt.Label(new Rect(p.x-9,p.y-10,18,20),"★",font,12,Hex("A97831"),true);
            }
            else if(kind==TileKind.Resort)
            {
                CircuitArt.Circle(new Rect(p.x-13,p.y+3,26,8),Hex("97D9D0"));
                CircuitArt.Circle(new Rect(p.x-10,p.y+2,20,7),Hex("F4DFAC"));
                CircuitArt.Line(p+new Vector2(0,5),p+new Vector2(2,-8),Hex("9B704D"),3);
                CircuitArt.Line(p+new Vector2(2,-8),p+new Vector2(-9,-11),Hex("377D5B"),3);
                CircuitArt.Line(p+new Vector2(2,-8),p+new Vector2(-8,-3),Hex("377D5B"),3);
                CircuitArt.Line(p+new Vector2(2,-8),p+new Vector2(11,-11),Hex("377D5B"),3);
                CircuitArt.Line(p+new Vector2(2,-8),p+new Vector2(10,-3),Hex("377D5B"),3);
            }
        }
        static void DrawBuilding(Vector2 p,int level,Color c)
        {
            if(level==1)
            {
                CircuitArt.Circle(new Rect(p.x-13,p.y-3,28,7),new Color(.15f,.3f,.25f,.18f));
                CircuitArt.Fill(new Rect(p.x-9,p.y-14,18,13),Hex("F4E9D2"));
                CircuitArt.Line(p+new Vector2(-12,-14),p+new Vector2(0,-23),c,5);
                CircuitArt.Line(p+new Vector2(0,-23),p+new Vector2(12,-14),c,5);
                CircuitArt.Fill(new Rect(p.x-2,p.y-9,4,8),Hex("795B48"));return;
            }
            if(level==4)
            {
                CircuitArt.Circle(new Rect(p.x-18,p.y-3,38,8),new Color(.15f,.3f,.25f,.2f));
                CircuitArt.Fill(new Rect(p.x-13,p.y-36,26,35),Color.Lerp(c,Color.white,.15f));
                CircuitArt.Fill(new Rect(p.x-17,p.y-28,7,27),Color.Lerp(c,Color.black,.12f));
                CircuitArt.Fill(new Rect(p.x+10,p.y-28,7,27),Color.Lerp(c,Color.black,.2f));
                for(int row=0;row<5;row++)for(int col=0;col<3;col++)CircuitArt.Fill(new Rect(p.x-9+col*7,p.y-31+row*6,4,3),Hex("FFF3C8"));
                CircuitArt.Fill(new Rect(p.x-15,p.y-39,30,4),Hex("E6D9B5"));
                CircuitArt.Line(p+new Vector2(0,-39),p+new Vector2(0,-50),Hex("795B48"),2);
                CircuitArt.Fill(new Rect(p.x+1,p.y-50,9,6),Hex("EDAD63"));return;
            }
            float height=10+level*5;
            CircuitArt.Fill(new Rect(p.x-15,p.y-3,33,7),new Color(.15f,.3f,.25f,.15f));
            CircuitArt.Fill(new Rect(p.x-13,p.y-height,24,height),c);
            CircuitArt.Fill(new Rect(p.x+11,p.y-height+3,6,height-3),Color.Lerp(c,Color.black,.20f));
            CircuitArt.Fill(new Rect(p.x-15,p.y-height-3,30,5),Color.Lerp(c,Color.white,.45f));
            if(level==3){CircuitArt.Fill(new Rect(p.x-8,p.y-height-8,16,5),Hex("E6D9B5"));CircuitArt.Fill(new Rect(p.x-3,p.y-height-13,6,5),c);}
            for(int row=0;row<level;row++)for(int col=0;col<2;col++)CircuitArt.Fill(new Rect(p.x-8+col*11,p.y-height+5+row*5,5,3),Hex("FFF3C8"));
            if(level>=4){CircuitArt.Line(p+new Vector2(0,-height-3),p+new Vector2(0,-height-12),c,2);CircuitArt.Fill(new Rect(p.x+1,p.y-height-13,10,6),Hex("EDAD63"));}
        }
        static void DrawIcon(TileKind kind,Vector2 p,Color c)
        {
            switch(kind)
            {
                case TileKind.Start:
                    CircuitArt.Circle(new Rect(p.x-23,p.y-8,46,21),Hex("DCEDE2"));
                    CircuitArt.Line(p+new Vector2(14,2),p+new Vector2(-14,2),c,5);CircuitArt.Line(p+new Vector2(-14,2),p+new Vector2(-5,-6),c,4);CircuitArt.Line(p+new Vector2(-14,2),p+new Vector2(-5,10),c,4);break;
                case TileKind.Travel:
                    CircuitArt.Circle(new Rect(p.x-23,p.y+5,17,8),Hex("DEEDE9"));CircuitArt.Circle(new Rect(p.x+7,p.y-10,18,9),Hex("DEEDE9"));
                    CircuitArt.Line(p+new Vector2(-16,8),p+new Vector2(17,-8),Hex("648FA2"),5);CircuitArt.Line(p+new Vector2(3,-1),p+new Vector2(-8,-13),Hex("648FA2"),4);CircuitArt.Line(p+new Vector2(3,-1),p+new Vector2(5,12),Hex("648FA2"),4);CircuitArt.Line(p+new Vector2(-11,5),p+new Vector2(-18,-2),Hex("648FA2"),3);break;
                case TileKind.Island:
                case TileKind.Resort:
                    CircuitArt.Circle(new Rect(p.x-23,p.y+5,46,13),Hex("B7DED8"));CircuitArt.Circle(new Rect(p.x-16,p.y+3,32,10),Hex("E9D7A6"));
                    CircuitArt.Line(p+new Vector2(0,8),p+new Vector2(3,-10),Hex("A3754F"),4);
                    CircuitArt.Line(p+new Vector2(3,-10),p+new Vector2(-13,-14),Hex("568A68"),4);CircuitArt.Line(p+new Vector2(3,-10),p+new Vector2(-10,-5),Hex("568A68"),4);
                    CircuitArt.Line(p+new Vector2(3,-10),p+new Vector2(16,-14),Hex("568A68"),4);CircuitArt.Line(p+new Vector2(3,-10),p+new Vector2(15,-4),Hex("568A68"),4);break;
                case TileKind.Olympics:
                    CircuitArt.Circle(new Rect(p.x-18,p.y-12,36,20),Hex("D9AD52"));CircuitArt.Circle(new Rect(p.x-14,p.y-9,28,15),Hex("F3D98C"));
                    CircuitArt.Fill(new Rect(p.x-10,p.y-13,20,13),Hex("E5B85A"));CircuitArt.Fill(new Rect(p.x-7,p.y-11,14,8),Hex("F8DEA0"));CircuitArt.Line(p+new Vector2(0,0),p+new Vector2(0,11),Hex("BD8840"),4);CircuitArt.Fill(new Rect(p.x-10,p.y+9,20,5),Hex("BD8840"));break;
                case TileKind.Bonus:
                    CircuitArt.Circle(new Rect(p.x-12,p.y-14,24,28),Hex("D3B367"));CircuitArt.Circle(new Rect(p.x-8,p.y-10,16,20),Hex("F0DFA1"));CircuitArt.Line(p+new Vector2(0,-7),p+new Vector2(0,7),c,3);break;
                default:
                    PaperTheme.Card(new Rect(p.x-12,p.y-15,24,30),PaperTheme.Paper,false,1.5f);CircuitArt.Label(new Rect(p.x-12,p.y-16,24,30),"?",GUI.skin.font,23,Ink,true);break;
            }
        }
        public void DrawTokens()
        {
            var order=new List<int>();for(int i=0;i<game.Players.Count;i++)if(!game.Players[i].Bankrupt)order.Add(i);
            order.Sort((a,b)=>{if(a==b)return 0;int depth=VisiblePawnPosition(a).y.CompareTo(VisiblePawnPosition(b).y);return depth!=0?depth:a==PresentationSeat?1:b==PresentationSeat?-1:a.CompareTo(b);});
            // Draw all bases first, so another player's base cannot cover a character.
            foreach(int i in order)
            {
                var p=VisiblePawnPosition(i);bool active=i==PresentationSeat && game.Phase!=GamePhase.Finished;
                float scale=active?1.50f:1.24f,alpha=active?1f:.84f;
                var baseColor=PlayerColor(i);baseColor.a=alpha;
                CircuitArt.Circle(new Rect(p.x-18*scale,p.y-2,36*scale,12*scale),new Color(.1f,.17f,.12f,.28f*alpha));
                CircuitArt.Circle(new Rect(p.x-17*scale,p.y-5,34*scale,11*scale),new Color(.12f,.17f,.14f,.94f*alpha));
                CircuitArt.Circle(new Rect(p.x-15*scale,p.y-4,30*scale,9*scale),new Color(1,.98f,.9f,.95f*alpha));
                CircuitArt.Circle(new Rect(p.x-12*scale,p.y-3,24*scale,7*scale),baseColor);
                if(Marble.Core.BodyCatalog.IsSold(game.Players[i],Marble.Core.BodyPart.Kidney) && Mathf.Repeat(Time.time+i*1.7f,12)<3)CircuitArt.Circle(new Rect(p.x-24,p.y+2,48,11),new Color(.83f,.72f,.22f,.58f*alpha));
            }
            foreach(int i in order)
            {
                var p=VisiblePawnPosition(i);bool active=i==PresentationSeat && game.Phase!=GamePhase.Finished;
                float lift=ReducedMotion?0:Mathf.Sin(hop[i]*Mathf.PI)*12,scale=active?1.50f:1.24f,alpha=active?1f:.84f;
                var portrait=CharacterPortrait(i);
                if(portrait!=null)
                {
                    var bounds=new Rect(p.x-24*scale,p.y-57*scale-lift,48*scale,60*scale);
                    var outline=new Color(.05f,.07f,.06f,.92f*alpha);
                    // The camera texture is transparent: offset copies follow the actual silhouette.
                    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)if(x!=0 || y!=0)
                        CircuitArt.Texture(new Rect(bounds.x+x*1.8f,bounds.y+y*1.8f,bounds.width,bounds.height),portrait,outline);
                    CircuitArt.Texture(bounds,portrait,new Color(1,1,1,alpha));
                }
                if(active)
                {
                    float arrowY=p.y-72*scale-lift;
                    var tip=new Vector2(p.x,arrowY+10);var top=new Vector2(p.x,arrowY-3);
                    var left=new Vector2(p.x-6,arrowY+3);var right=new Vector2(p.x+6,arrowY+3);
                    CircuitArt.Line(tip,top,PaperTheme.Paper,7);CircuitArt.Line(tip,left,PaperTheme.Paper,7);CircuitArt.Line(tip,right,PaperTheme.Paper,7);
                    CircuitArt.Line(tip,top,Ink,3);CircuitArt.Line(tip,left,Ink,3);CircuitArt.Line(tip,right,Ink,3);
                }
            }
        }
        public void DrawDice(Rect r)
        {
            if(models==null || DiceTexture==null)return;
            models.SetDiceSkin(game.Players[PresentationSeat].DiceSkin);
            if(diceTime>0 && !ReducedMotion)
            {
                float progress=1-diceTime/diceDuration,settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,progress));
                var spread=new Rect(r.center.x-r.width*.8f,r.center.y-r.height*.8f,r.width*1.6f,r.height*1.6f);
                r=new Rect(Mathf.Lerp(spread.x,r.x,settle),Mathf.Lerp(spread.y,r.y,settle),Mathf.Lerp(spread.width,r.width,settle),Mathf.Lerp(spread.height,r.height,settle));
            }
            float aspect=DiceTexture.width/(float)DiceTexture.height;
            if(r.width/r.height>aspect){float width=r.height*aspect;r.x+=(r.width-width)*.5f;r.width=width;}
            else {float height=r.width/aspect;r.y+=(r.height-height)*.5f;r.height=height;}
            for(int i=0;i<2;i++)
            {
                var p=models.DiceGroundViewport(i);float height=models.DiceHeight(i),width=r.width*.20f/(1+height*.32f);
                float sx=r.x+p.x*r.width,sy=r.y+(1-p.y)*r.height;
                CircuitArt.Circle(new Rect(sx-width*.65f,sy-width*.16f,width*1.3f,width*.34f),new Color(.12f,.17f,.1f,.08f/(1+height)));
                CircuitArt.Circle(new Rect(sx-width*.46f,sy-width*.09f,width*.92f,width*.19f),new Color(.12f,.17f,.1f,.21f/(1+height*1.8f)));
                if(diceTime>0 && !ReducedMotion)
                {
                    float progress=1-diceTime/diceDuration,flash=0;
                    for(int hit=0;hit<3;hit++){float age=progress-DiceThrowMotion.ImpactAt(i,hit);if(age>=0 && age<.08f)flash=Mathf.Max(flash,1-age/.08f);}
                    if(flash>0)
                    {
                        var accent=new Color(.3f,.36f,.22f,.48f*flash);
                        CircuitArt.Line(new Vector2(sx-width*.7f,sy-4),new Vector2(sx-width*.88f,sy-7),accent,1.6f);
                        CircuitArt.Line(new Vector2(sx+width*.7f,sy-4),new Vector2(sx+width*.88f,sy-7),accent,1.6f);
                    }
                }
            }
            GUI.DrawTexture(r,DiceTexture,ScaleMode.ScaleToFit,true);
        }
        public void Sync(bool rolled)
        {
            eventPause=0;
            if(rolled){diceDuration=ReducedMotion?.12f:DiceThrowMotion.Duration;diceTime=diceDuration;diceResultHold=ReducedMotion?0:.18f;diceImpactMask=0;resultA=game.DiceA;resultB=game.DiceB;presentationSeat=game.LastDiceActor;models.ReducedMotion=ReducedMotion;models.AnimateDice(0,resultA,resultB);}
            bool fresh=movementRevision!=game.ActionMovementRevision;movementRevision=game.ActionMovementRevision;
            for(int i=0;i<game.Players.Count;i++)
            {
                int old=last[i],next=game.Players[i].Position;
                var trace=fresh?game.ActionMovement.Where(step=>step.Seat==i).ToArray():Array.Empty<BoardMovementStep>();
                if(old==next && trace.Length==0)continue;
                routes[i].Clear();fallTime[i]=0;
                completedDiceSteps[i]=0;totalDiceSteps[i]=trace.Count(step=>step.Dice);
                if(trace.Length>0 && trace[0].From==old && trace[trace.Length-1].To==next)
                    foreach(var step in trace)routes[i].Enqueue(new ViewHop(Point(step.To)+Offset(i),step.Fall,step.Transfer,step.Dice,step.Reason,step.From));
                else routes[i].Enqueue(new ViewHop(Point(next)+Offset(i),false,true));
                hop[i]=0;starts[i]=display[i];SetTarget(i,routes[i].Dequeue());last[i]=next;moving=true;followSeat=i;
                if(!rolled)presentationSeat=i;
            }
        }
        void SetTarget(int seat,ViewHop target){targets[seat]=target.Point;targetFalls[seat]=target.Fall;targetTransfers[seat]=target.Transfer;targetDice[seat]=target.Dice;targetReasons[seat]=target.Reason;}
        public void RefreshProperties(){}
        void Update()
        {
            if(game==null)return;models.ReducedMotion=ReducedMotion;
            for(int i=0;i<game.Players.Count;i++){models.SetBodyState(i,game.Config.EnableBodyEconomy?game.Players[i].SoldBodyMask:0);models.SetFallPose(i,fallTime[i]>0?Mathf.Sin((.65f-fallTime[i])/.65f*Mathf.PI):0);}
            if(diceTime>0)
            {
                diceTime-=Time.deltaTime;float progress=Mathf.Clamp01(1-diceTime/diceDuration);
                if(!ReducedMotion)for(int bounce=0;bounce<3;bounce++)for(int die=0;die<2;die++)
                {int impact=bounce*2+die,bit=1<<impact;float threshold=DiceThrowMotion.ImpactAt(die,bounce);if(progress>=threshold && (diceImpactMask&bit)==0){diceImpactMask|=bit;DiceImpact?.Invoke(impact);}}
                models.AnimateDice(progress,resultA,resultB);if(diceTime<=0)models.SetDice(resultA,resultB);return;
            }
            if(diceResultHold>0){diceResultHold-=Time.deltaTime;return;}
            if(eventPause>0){eventPause=Mathf.Max(0,eventPause-Time.deltaTime);return;}
            moving=false;
            for(int i=0;i<game.Players.Count;i++)
            {
                if(fallTime[i]>0){fallTime[i]=Mathf.Max(0,fallTime[i]-Time.deltaTime);moving=true;continue;}
                if(hop[i]>=1 && Vector2.Distance(display[i],targets[i])<.1f && routes[i].Count==0)continue;
                moving=true;hop[i]=Mathf.Min(1,hop[i]+Time.deltaTime*(ReducedMotion?25:targetTransfers[i]?5f:7.5f));display[i]=Vector2.Lerp(starts[i],targets[i],Mathf.SmoothStep(0,1,hop[i]));
                if(hop[i]>=1)
                {
                    display[i]=targets[i];if(targetDice[i])completedDiceSteps[i]++;
                    if(!targetTransfers[i])StepSound?.Invoke();if(targetFalls[i])fallTime[i]=.65f;
                    if(routes[i].Count>0)
                    {
                        var nextHop=routes[i].Dequeue();bool diceArrival=targetDice[i] && !nextHop.Dice;
                        if(!ReducedMotion && (diceArrival || !nextHop.Dice && nextHop.Reason!=targetReasons[i]))
                        {
                            eventPause=.85f;
                            eventPauseCaption=diceArrival?"주사위 "+completedDiceSteps[i]+"칸 완료 · "+game.Tiles[nextHop.From].Definition.Name:nextHop.Reason;
                        }
                        starts[i]=display[i];SetTarget(i,nextHop);hop[i]=0;
                    }
                }
            }
            if(ReducedMotion){cameraZoom=1f;return;}
            float blend=Mathf.Clamp01(Time.deltaTime*7f);
            if(moving && followSeat>=0 && followSeat<game.Players.Count)cameraFocus=Vector2.Lerp(cameraFocus,VisiblePawnPosition(followSeat),blend);
            cameraZoom=Mathf.Lerp(cameraZoom,moving?1.12f:1f,blend);
            if(!moving && cameraZoom<1.001f){cameraZoom=1f;followSeat=-1;cameraFocus=new Vector2(720,450);}
        }
        void LateUpdate()
        {
            if(game!=null)UpdatePawnOffsets(ReducedMotion?1:1-Mathf.Exp(-12*Time.deltaTime));
        }
    }
}
