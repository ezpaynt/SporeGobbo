namespace SporeGobbo.Geometry.Preview
{
    // Disposable derived read cache, never an editable authority. Refilled before local invalidation.
    public sealed class ChunkReadWindow:IOccupancySource,ISmoothedOccupancy,IFrontierPresentation
    {
        readonly IOccupancySource source;readonly bool[] values=new bool[25*25];readonly int x0,y0;readonly double[] smooth=new double[19*19];
        public int Width=>source.Width;public int Height=>source.Height;
        public ChunkReadWindow(IOccupancySource source,DualTile key){this.source=source;x0=key.X*16-4;y0=key.Y*16-4;Refresh();}
        public void Refresh(){for(int y=0;y<25;y++)for(int x=0;x<25;x++)values[y*25+x]=source.IsSolid(System.Math.Max(0,System.Math.Min(Width-1,x0+x)),System.Math.Max(0,System.Math.Min(Height-1,y0+y)));
            for(int y=0;y<19;y++)for(int x=0;x<19;x++){double sum=0;for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)sum+=WallOrientationCache.FilterWeight(dx,dy)*(IsSolid(x0+3+x+dx,y0+3+y+dy)?1:0);smooth[y*19+x]=sum/WallOrientationCache.FilterWeightSum;}
        }
        public bool SoftTile(int x,int y)=>source is IFrontierPresentation f&&f.SoftTile(x,y);
        public bool TryField(double x,double y,out double value)
        {double sx=x-.5,sy=y-.5;int ix=(int)System.Math.Floor(sx),iy=(int)System.Math.Floor(sy);double tx=sx-ix,ty=sy-iy;int xx=ix-(x0+3),yy=iy-(y0+3);value=0;if(xx<0||yy<0||xx>=18||yy>=18)return false;int i=yy*19+xx;value=smooth[i]*(1-tx)*(1-ty)+smooth[i+1]*tx*(1-ty)+smooth[i+20]*tx*ty+smooth[i+19]*(1-tx)*ty;return true;}
        public bool IsSolid(int x,int y){int xx=x-x0,yy=y-y0;return xx>=0&&xx<25&&yy>=0&&yy<25?values[yy*25+xx]:source.IsSolid(x,y);}
    }
}
