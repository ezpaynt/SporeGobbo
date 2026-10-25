using System;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
 [CreateAssetMenu(menuName="Spore Gobbo/Temporary environment palette V2")]
 public sealed class Stage55Palette:ScriptableObject
 {
  [Serializable]public class Entry{public string name,hex;public Entry(string n,string h){name=n;hex=h;}}
  public Entry[] entries=Baseline();
  public static Entry[] Baseline()=>new[]{
   new Entry("InkBlack","#050907"),new Entry("VoidBlackGreen","#08120F"),new Entry("PermanentMossBlack","#111B17"),new Entry("PermanentWallFace","#1B2722"),new Entry("PermanentDeepShadow","#0C1411"),new Entry("UndugDirtMass","#242A26"),new Entry("UndugDirtVariation","#30322F"),new Entry("FreshFractureEdge","#676255"),new Entry("DigWallUpper","#504D46"),new Entry("DigWallLower","#282F2B"),new Entry("WallFootOcclusion","#151A19"),new Entry("FloorSlateBlue","#465963"),new Entry("FloorDust","#566469"),new Entry("FloorDeepBlueShadow","#172735"),new Entry("FloorPlumShadow","#453B51"),new Entry("StoneLight","#697476"),new Entry("StoneBase","#465156"),new Entry("StoneShadow","#293338"),new Entry("StoneDeep","#171D20"),new Entry("RootLight","#604A3D"),new Entry("RootBase","#49362F"),new Entry("RootShadow","#241B1B"),new Entry("BoneLimestone","#C8C1AA"),new Entry("BoneShadow","#817D70"),new Entry("MossStain","#536047"),new Entry("SicklyLichen","#7E904B"),new Entry("DeepCerulean","#176B8B"),new Entry("CeruleanGlow","#35A4C5"),new Entry("BlueViolet","#51445F")};
  // SetColor accepts sRGB authored values; Unity converts material properties for Linear rendering.
  public Color Color(string name){foreach(var e in entries)if(e.name==name&&ColorUtility.TryParseHtmlString(e.hex,out var c))return c;throw new ArgumentException(name);}
  public string Matter(PreviewMatter m)=>m==PreviewMatter.Root?"RootBase":m==PreviewMatter.Stone?"StoneBase":m==PreviewMatter.Bone?"BoneLimestone":m==PreviewMatter.Permanent?"PermanentMossBlack":"UndugDirtMass";
 }
}
