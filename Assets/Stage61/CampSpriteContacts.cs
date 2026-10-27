using UnityEngine;
using System.Collections.Generic;
/// <summary>Unchanged placeholder sprite contact measured from existing alpha bounds. No gameplay scale.</summary>
public static class CampSpriteContacts
{
 static readonly Dictionary<string,Vector2> frames=new Dictionary<string,Vector2>(System.StringComparer.OrdinalIgnoreCase)
 {
  {"logs_0",new Vector2(1.80000000f,0.30468750f)},
  {"fire1_0",new Vector2(1.85000000f,0.15384615f)},
  {"Fire2_0",new Vector2(1.85000000f,0.15164221f)},
  {"Fire2_1",new Vector2(0.04007687f,0.12903226f)},
  {"fire3_0",new Vector2(1.85000000f,0.10397767f)},
  {"fire3_1",new Vector2(0.02065597f,0.12500000f)},
  {"fire3_2",new Vector2(0.01290998f,0.20000000f)},
  {"fire3_3",new Vector2(0.03485694f,0.11111111f)},
  {"fire3_4",new Vector2(0.04260293f,0.03030303f)},
  {"fire3_5",new Vector2(0.00903699f,0.28571429f)},
  {"fire3_6",new Vector2(0.01032798f,0.12500000f)},
  {"fire3_7",new Vector2(0.01290998f,0.30000000f)},
  {"fire3_8",new Vector2(0.02194696f,0.23529412f)},
  {"singleMush_0",new Vector2(0.93081797f,0.22396188f)},
  {"2mush_0",new Vector2(1.00867665f,0.23621511f)},
  {"3mush_0",new Vector2(1.13476124f,0.25663717f)},
  {"shiny1_0",new Vector2(1.16328452f,0.27794263f)},
  {"shiny2_0",new Vector2(0.96250000f,0.26876513f)},
  {"shiny3_0",new Vector2(1.05398406f,0.19958420f)},
 };
 public static Vector2 Frame(Sprite sprite)=>sprite!=null&&frames.TryGetValue(sprite.name,out var value)?value:new Vector2(1,0);
 public static bool HasCalibration(Sprite sprite)=>sprite!=null&&frames.ContainsKey(sprite.name);
}
