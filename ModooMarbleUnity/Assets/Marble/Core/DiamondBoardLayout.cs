using System;
namespace Marble.Core
{
    // Coordinates shared by painting, movement, and hit testing in the 1440x900 canvas.
    public static class DiamondBoardLayout
    {
        public const float HalfWidth=60f,HalfHeight=64.5f/1.7f,CenterX=720f,CenterY=440f;
        public const float CornerUnits=1.7f,BoardUnits=CornerUnits*2+7;
        public const float CornerHalfWidth=HalfWidth*CornerUnits,CornerHalfHeight=HalfHeight*CornerUnits;
        public const float OuterHalfWidth=BoardUnits*HalfWidth,OuterHalfHeight=BoardUnits*HalfHeight;
        public const float InnerHalfWidth=7*HalfWidth,InnerHalfHeight=7*HalfHeight;
        static void Cell(int index,out int col,out int row)
        {
            if(index<0 || index>=32)throw new ArgumentOutOfRangeException(nameof(index));
            if(index<=8){col=0;row=index;}
            else if(index<=16){col=index-8;row=8;}
            else if(index<=24){col=8;row=24-index;}
            else{col=32-index;row=0;}
        }
        static float AxisCenter(int cell)=>cell==0?CornerUnits*.5f:cell==8?BoardUnits-CornerUnits*.5f:CornerUnits+cell-.5f;
        public static void Center(int index,out float x,out float y)
        {
            Cell(index,out int col,out int row);float u=AxisCenter(col),v=AxisCenter(row);
            x=CenterX+(u-v)*HalfWidth;y=CenterY-(u+v-BoardUnits)*HalfHeight;
        }
        public static float StepLength(int index)
        {
            float factor=index%8==0 || index%8==7?(CornerUnits+1)*.5f:1;
            return factor*(float)Math.Sqrt(HalfWidth*HalfWidth+HalfHeight*HalfHeight);
        }
        public static void Vertex(int index,int vertex,out float x,out float y)
        {
            if(vertex<0 || vertex>3)throw new ArgumentOutOfRangeException(nameof(vertex));
            Cell(index,out int col,out int row);
            float width=col==0 || col==8?CornerUnits:1,depth=row==0 || row==8?CornerUnits:1;
            float u=AxisCenter(col)+(vertex<2?width*.5f:-width*.5f);
            float v=AxisCenter(row)+(vertex==0 || vertex==3?depth*.5f:-depth*.5f);
            x=CenterX+(u-v)*HalfWidth;y=CenterY-(u+v-BoardUnits)*HalfHeight;
        }
        static bool Contains(int index,float x,float y)
        {
            bool positive=false,negative=false;
            for(int v=0;v<4;v++)
            {
                Vertex(index,v,out float ax,out float ay);Vertex(index,(v+1)%4,out float bx,out float by);
                float cross=(bx-ax)*(y-ay)-(by-ay)*(x-ax);
                if(cross>.001f)positive=true;else if(cross<-.001f)negative=true;
                if(positive && negative)return false;
            }
            return true;
        }
        public static int Hit(float x,float y)
        {
            // Shared boundaries belong to corner tiles; faces no longer overlap.
            for(int corner=0;corner<32;corner+=8)if(Contains(corner,x,y))return corner;
            int hit=-1;float best=float.MaxValue;
            for(int i=0;i<32;i++)
            {
                Center(i,out float cx,out float cy);
                float distance=Math.Abs(x-cx)/HalfWidth+Math.Abs(y-cy)/HalfHeight;
                if(Contains(i,x,y) && distance<best){best=distance;hit=i;}
            }
            return hit;
        }
    }
    public static class DiamondBoardLayoutSelfTests
    {
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Diamond board: "+message);}
        public static int Run()
        {
            int count=0;
            for(int i=0;i<32;i++)
            {
                DiamondBoardLayout.Center(i,out float x,out float y);DiamondBoardLayout.Center((i+1)%32,out float nx,out float ny);
                float factor=i%8==0 || i%8==7?1.35f:1;
                Check(Math.Abs(Math.Abs(nx-x)-60*factor)<.01f && Math.Abs(Math.Abs(ny-y)-DiamondBoardLayout.HalfHeight*factor)<.01f,"adjacent "+i);
                float sample=i%8==0?25:8;
                foreach(var d in new[]{(0f,0f),(sample,0f),(-sample,0f),(0f,sample),(0f,-sample)})
                    Check(DiamondBoardLayout.Hit(x+d.Item1,y+d.Item2)==i,"interior hit "+i);
                count++;
            }
            DiamondBoardLayout.Center(0,out float sx,out float sy);DiamondBoardLayout.Center(8,out float ix,out float iy);
            DiamondBoardLayout.Center(16,out float ox,out float oy);DiamondBoardLayout.Center(24,out float tx,out float ty);
            Check(Math.Abs(sx-720)<.01f && Math.Abs(sy-770.088f)<.01f && Math.Abs(ix-198)<.01f && Math.Abs(iy-440)<.01f && Math.Abs(ox-720)<.01f && Math.Abs(oy-109.912f)<.01f && Math.Abs(tx-1242)<.01f && Math.Abs(ty-440)<.01f,"corner orientation");count++;
            DiamondBoardLayout.Center(1,out float firstX,out float firstY);Check(firstX<sx && firstY<sy,"leave start toward the left");count++;
            float cornerArea=0,normalArea=0;
            for(int j=0;j<4;j++)
            {
                DiamondBoardLayout.Vertex(0,j,out float ax,out float ay);DiamondBoardLayout.Vertex(0,(j+1)%4,out float bx,out float by);cornerArea+=ax*by-ay*bx;
                DiamondBoardLayout.Vertex(1,j,out ax,out ay);DiamondBoardLayout.Vertex(1,(j+1)%4,out bx,out by);normalArea+=ax*by-ay*bx;
            }
            Check(Math.Abs(cornerArea)>Math.Abs(normalArea) && DiamondBoardLayout.Hit(sx-30,sy)==0,"larger corner area and corner hit");count++;
            for(int i=0;i<32;i++)
            {
                if(i%8==0)continue;
                DiamondBoardLayout.Center(i,out float x,out float y);float vx=0,vy=0;
                for(int j=0;j<4;j++){DiamondBoardLayout.Vertex(i,j,out float px,out float py);vx+=px*.25f;vy+=py*.25f;}
                double area=0;
                for(int j=0;j<4;j++){DiamondBoardLayout.Vertex(i,j,out float ax,out float ay);DiamondBoardLayout.Vertex(i,(j+1)%4,out float bx,out float by);area+=(double)ax*by-(double)ay*bx;}
                Check(Math.Abs(Math.Abs(area)-Math.Abs(normalArea))<1,"equal ordinary tile area "+i);
                Check(Math.Abs(vx-x)<.01f && Math.Abs(vy-y)<.01f && DiamondBoardLayout.Hit(vx,vy)==i,"uncut tile center hit "+i);count++;
            }
            Check(DiamondBoardLayout.Hit(720,440)==-1 && DiamondBoardLayout.Hit(0,0)==-1,"empty board and exterior");count++;
            bool rejected=false;try{DiamondBoardLayout.Center(32,out _,out _);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"invalid index");count++;
            return count;
        }
    }
}
