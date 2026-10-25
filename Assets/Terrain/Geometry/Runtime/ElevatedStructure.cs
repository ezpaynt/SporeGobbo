using System;
using System.Collections.Generic;
namespace SporeGobbo.Geometry
{
 // Height is never added to logical Y. Ground is the only terrain lookup coordinate.
 public readonly struct ElevatedVertex
 {
  public readonly Point2 Ground; public readonly double Height;
  public ElevatedVertex(Point2 ground,double height){if(height<0||double.IsNaN(height)||double.IsInfinity(height))throw new ArgumentOutOfRangeException(nameof(height));Ground=ground;Height=height;}
  public static ElevatedVertex Lerp(ElevatedVertex a,ElevatedVertex b,double t)=>new ElevatedVertex(a.Ground*(1-t)+b.Ground*t,a.Height*(1-t)+b.Height*t);
 }
 public static class ElevatedProjection
 {
  // Isotropic visual scale at the actual ground point. This matches the existing actor
  // billboard scale, but permits a different ground point and height at every root section.
  public static Point2 Project(FocusProjection map,ElevatedVertex v){var p=map.Project(v.Ground);return new Point2(p.X,p.Y-v.Height*Math.Sqrt(map.ScaleX(v.Ground.X)*map.ScaleY(v.Ground.Y)));}
  public static Point2 Legacy(FocusProjection map,ElevatedVertex v)=>map.Project(new Point2(v.Ground.X,v.Ground.Y-v.Height));
 }
 public readonly struct ElevatedSection
 {
  public readonly ElevatedVertex Left,Right;
  public ElevatedSection(Point2 p,double h,double radius,Point2 axis,double thickness=0){Left=new ElevatedVertex(p-axis*radius,h+thickness);Right=new ElevatedVertex(p+axis*radius,h);}
  public ElevatedSection(ElevatedVertex left,ElevatedVertex right){Left=left;Right=right;}
 }
 public sealed class ElevatedPath
 {
  public readonly string Id;public readonly ElevatedSection[] Sections;public readonly bool Overhead;
  public ElevatedPath(string id,ElevatedSection[] sections,bool overhead=false){Id=id;Sections=sections;Overhead=overhead;}
 }
 public enum LandmarkProfile{SmoothReference,BroadShoulder,Segmented,Cluster}
 public static class ElevatedShapes
 {
  public static ElevatedPath Spike(Point2 p,double width,double height,LandmarkProfile profile,int cluster=0)
  {
   double[] t=profile==LandmarkProfile.SmoothReference?new[]{0,.125,.25,.375,.5,.625,.75,.875,1}:new[]{0,.09,.24,.43,.51,.69,.82,1};
   double[] r=profile==LandmarkProfile.BroadShoulder?new[]{.88,1.0,.93,.95,.73,.67,.36,0}:new[]{.92,1,.82,.95,.64,.70,.29,0};
   var s=new ElevatedSection[t.Length];for(int i=0;i<s.Length;i++){double radius=profile==LandmarkProfile.SmoothReference?width*Math.Pow(1-t[i],.8):width*r[i];double lean=profile==LandmarkProfile.SmoothReference?.9*Math.Sin(t[i]*Math.PI):width*(i%3==0?-.10:.08)*Math.Sin(t[i]*Math.PI);s[i]=new ElevatedSection(new Point2(p.X+lean,p.Y),height*t[i],radius,new Point2(1,0));}return new ElevatedPath("spike/"+profile+"/"+cluster,s);
  }
  public static ElevatedPath Path(string id,Point2[] ground,double[] heights,double[] radii,Point2[] axes=null,ElevatedSection? shared=null)
  {
   if(ground.Length!=heights.Length||ground.Length!=radii.Length||ground.Length<2)throw new ArgumentException();
   var s=new ElevatedSection[ground.Length];for(int i=0;i<s.Length;i++){var tangent=ground[Math.Min(i+1,s.Length-1)]-ground[Math.Max(0,i-1)];double len=Math.Sqrt(tangent.X*tangent.X+tangent.Y*tangent.Y);var axis=axes==null?(len>1e-8?new Point2(-tangent.Y/len,tangent.X/len):new Point2(1,0)):axes[i];s[i]=new ElevatedSection(ground[i],heights[i],radii[i],axis,radii[i]*.55);}if(shared.HasValue)s[0]=shared.Value;
   var smooth=new ElevatedSection[(s.Length-1)*4+1];for(int i=0;i<s.Length-1;i++)for(int j=0;j<4;j++){double t=j/4.0;smooth[i*4+j]=j==0?s[i]:new ElevatedSection(Curve(s[Math.Max(0,i-1)].Left,s[i].Left,s[i+1].Left,s[Math.Min(s.Length-1,i+2)].Left,t),Curve(s[Math.Max(0,i-1)].Right,s[i].Right,s[i+1].Right,s[Math.Min(s.Length-1,i+2)].Right,t));}smooth[smooth.Length-1]=s[s.Length-1];return new ElevatedPath(id,smooth,true);
  }
  public static List<ElevatedPath> Roots(ExplorationFixture f)
  {
   // Explicit diagonal ground paths; height, width and ground heading are independent.
   var a=Path("root/ground-rise",new[]{f.P(30,-6),f.P(33,-5),f.P(36,-1),f.P(40,-2),f.P(43,-3)},new[]{0d,.5,2.5,1,0},new[]{3.4,3.1,2.2,2.6,3.5});
   var b=Path("root/high-span",new[]{f.P(43,-3),f.P(47,-7),f.P(54,-10),f.P(60,-6),f.P(65,-7),f.P(68,-9)},new[]{0d,8,16,12,3,0},new[]{3.5,3,2.4,2.1,2.7,3.4},shared:a.Sections[a.Sections.Length-1]);
   var branch=Path("root/diagonal-branch",new[]{f.P(54,-10),f.P(62,-17),f.P(70,-23),f.P(76,-29)},new[]{16d,8,1.5,0},new[]{2.4,2.7,1.8,.3},shared:b.Sections[8]);
   var low=Path("root/low-buried-tail",new[]{f.P(30,-6),f.P(26,-10),f.P(21,-14),f.P(18,-18)},new[]{0d,.7,.2,0},new[]{3.4,2.6,1.3,.15},shared:a.Sections[0]);
   return new List<ElevatedPath>{a,b,branch,low};
  }
  static ElevatedVertex Curve(ElevatedVertex a,ElevatedVertex b,ElevatedVertex c,ElevatedVertex d,double t){double t2=t*t,t3=t2*t;var p=(b.Ground*2+(c.Ground-a.Ground)*t+(a.Ground*2-b.Ground*5+c.Ground*4-d.Ground)*t2+(a.Ground*(-1)+b.Ground*3-c.Ground*3+d.Ground)*t3)*.5;double h=.5*(2*b.Height+(c.Height-a.Height)*t+(2*a.Height-5*b.Height+4*c.Height-d.Height)*t2+(-a.Height+3*b.Height-3*c.Height+d.Height)*t3);return new ElevatedVertex(p,Math.Max(0,h));}
 }
}
