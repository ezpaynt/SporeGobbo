namespace SporeGobbo.Geometry.Preview
{
 /// <summary>Accepted immutable composition recipe. Returns a fresh editable instance; no preview orchestration.</summary>
 public static class ReferenceCameraSettings
 {
  public static Stage4Settings Create()=>new Stage4Settings{coverage=52f/(48f/.55f),side=.55f,top=.12f,foreground=1.9f,centerWidth=52,centerHeight=28,distance=30,anchor=.62f};
 }
}
