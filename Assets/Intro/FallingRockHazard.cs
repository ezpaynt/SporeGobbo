using System;using System.Collections.Generic;using UnityEngine;using SporeGobbo.Geometry;using SporeGobbo.Geometry.Preview;
/// <summary>Reusable logical ground warning/fall/contact. No airborne collider and no implicit terrain mutation.</summary>
public sealed class FallingRockHazard:MonoBehaviour
{
 public static int ActiveCount{get;private set;}public static int ImpactCount{get;private set;}public static int DamageCount{get;private set;}
 public float Age{get;private set;}public bool Impacted{get;private set;}public Vector2 Ground{get;private set;}public float WarningSeconds=1.8f,FallSeconds=.8f;
 IWorldRegion region;Func<bool> clock;Action impact;Action<string,Vector2> cue;int damage;bool cancelled,counted;Vector2 footprint=new Vector2(.38f,.247f);bool collapse;readonly Vector3[] vertices=new Vector3[10];readonly Color[] colors=new Color[10];readonly Vector2[] uv=new Vector2[10];readonly int[] triangles=new int[24];Mesh mesh;Material material;MeshRenderer renderer;readonly Collider2D[] hits=new Collider2D[16];readonly HashSet<int> hitActors=new HashSet<int>();
 public static FallingRockHazard Create(IWorldRegion region,Vector2 ground,int damage,Func<bool> active,Action impact=null,Action<string,Vector2> cue=null)
 {var cell=region.WorldToCell(ground);if(!region.LogicalReady||region.IsBlocked(cell))return null;var go=new GameObject("Logical falling rock — warning then contact");go.transform.SetParent(region.PresentationRoot,false);var rock=go.AddComponent<FallingRockHazard>();rock.region=region;rock.Ground=ground;rock.damage=Math.Max(0,damage);rock.clock=active;rock.impact=impact;rock.cue=cue;rock.mesh=new Mesh{name="Projected rock/warning/dust"};go.AddComponent<MeshFilter>().sharedMesh=rock.mesh;rock.renderer=go.AddComponent<MeshRenderer>();rock.material=new Material(region.ActorShader);rock.material.mainTexture=Texture2D.whiteTexture;rock.renderer.sharedMaterial=rock.material;rock.renderer.sortingOrder=115;rock.counted=true;ActiveCount++;cue?.Invoke("rock.warning",ground);return rock;}
 public void SetCollapseFootprint(IReadOnlyList<DualTile> cells){if(cells==null||cells.Count==0)return;int loX=int.MaxValue,loY=int.MaxValue,hiX=int.MinValue,hiY=int.MinValue;foreach(var c in cells){loX=Math.Min(loX,c.X);loY=Math.Min(loY,c.Y);hiX=Math.Max(hiX,c.X);hiY=Math.Max(hiY,c.Y);}footprint=new Vector2((hiX-loX+1)*region.CellSize*.5f,(hiY-loY+1)*region.CellSize*.5f);collapse=true;WarningSeconds=2.1f;FallSeconds=1.1f;}
 void Update()
 {
  using var profile=new Unity.Profiling.ProfilerMarker("Spore.Intro.FallingRock").Auto();if(cancelled||region==null)return;
  if(clock?.Invoke()==true)Age+=Time.deltaTime;
  if(!Impacted&&Age>=WarningSeconds+FallSeconds){Impacted=true;ImpactCount++;var cell=region.WorldToCell(Ground);if(region.LogicalReady&&!region.IsBlocked(cell)){int n=Physics2D.OverlapCircle(Ground,.48f,new ContactFilter2D{useTriggers=true},hits);for(int i=0;i<n;i++){var actor=hits[i]!=null?hits[i].GetComponentInParent<GobboController>():null;if(actor!=null&&damage>0&&hitActors.Add(actor.GetInstanceID())){actor.TakeDamage(damage);DamageCount++;}}cue?.Invoke("rock.impact",Ground);impact?.Invoke();}}
  if(Age>WarningSeconds+FallSeconds+.7f)Destroy(gameObject);
 }
 void LateUpdate()
 {
  if(cancelled||region?.Projection==null||mesh==null)return;renderer.enabled=region.PresentationReady;
  float height=Age<WarningSeconds?0:10*(1-Mathf.Clamp01((Age-WarningSeconds)/FallSeconds)*Mathf.Clamp01((Age-WarningSeconds)/FallSeconds));
  float radius=Age<WarningSeconds?1.35f:Impacted?1.5f:1;Color color=Age<WarningSeconds?new Color(.64f,.55f,.36f,.55f):Impacted?new Color(.41f,.41f,.32f,.4f*(1-Mathf.Clamp01((Age-WarningSeconds-FallSeconds)/.7f))):collapse?new Color(.18f,.22f,.17f,1):new Color(.39f,.43f,.40f,1);
  var logical=region.LogicalPosition(Ground);var scale=Stage4CameraPreview.ActorScale(region.Projection,logical,region.PresentationSettings.actorMode);var center=region.Render(Ground);center.y+=height*scale.y;
  vertices[0]=center;colors[0]=color;for(int i=0;i<=8;i++){float a=i*Mathf.PI/4;var v=region.Render(Ground+new Vector2(Mathf.Cos(a)*footprint.x*radius,Mathf.Sin(a)*footprint.y*radius));v.y+=height*scale.y;vertices[i+1]=v;colors[i+1]=color;uv[i+1]=new Vector2(.5f,.5f);if(i<8){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}}
  mesh.Clear();mesh.vertices=vertices;mesh.colors=colors;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
 }
 public void Cancel(){cancelled=true;impact=null;cue=null;clock=null;if(renderer!=null)renderer.enabled=false;Destroy(gameObject);}
 void OnDestroy(){if(counted){ActiveCount--;counted=false;}if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);impact=null;clock=null;region=null;}
}
