using System;
using UnityEngine;
namespace SporeGobbo.Geometry.Preview
{
 [CreateAssetMenu(menuName="Spore Gobbo/Temporary preview palette")]
 public sealed class Stage54Palette:ScriptableObject
 {
  [Serializable]public class Entry{public string name,hex;public Entry(string n,string h){name=n;hex=h;}}
  public Entry[] entries=Baseline();
  public static Entry[] Baseline()=>new[]{new Entry("VoidBlackGreen","#08120F"),new Entry("PermanentMossBlack","#1E2A25"),new Entry("FloorDustySlate","#697780"),new Entry("FloorPlumShadow","#4D4C5B"),new Entry("DirtLevel1Umber","#987052"),new Entry("DirtLevel2Burnt","#704D3B"),new Entry("DirtLevel3Bruised","#49343B"),new Entry("ExposedWallMoss","#2D3930"),new Entry("RootSepia","#46362B"),new Entry("StoneColdGrey","#737D80"),new Entry("BoneLimestone","#D5CBB2"),new Entry("GrowthGrossGreen","#83964F"),new Entry("GrowthYellowMoss","#A0A35A"),new Entry("MineralDeepCerulean","#176B8B"),new Entry("GlowCerulean","#35A4C5"),new Entry("MineralTeal","#417E78"),new Entry("StrangeDustyPlum","#705F78"),new Entry("AccentOrangeRed","#C45A3D"),new Entry("AccentAlizarin","#8B2944"),new Entry("LightDirtyAmber","#D89A42")};
  // Unity Color shader properties / MPB SetColor use sRGB-authored Color values. Validate through rendered swatches in the active color space.
  public Color Color(string name){foreach(var e in entries)if(e.name==name){if(ColorUtility.TryParseHtmlString(e.hex,out var c))return c;throw new ArgumentException(e.hex);}throw new ArgumentException(name);}
  public string Matter(PreviewMatter m)=>m==PreviewMatter.Root?"RootSepia":m==PreviewMatter.Stone?"StoneColdGrey":m==PreviewMatter.Bone?"BoneLimestone":m==PreviewMatter.Permanent?"PermanentMossBlack":"DirtLevel1Umber";
 }
}
