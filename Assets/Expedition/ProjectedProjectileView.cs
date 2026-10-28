using UnityEngine;
/// <summary>Project actual rotated logical sprite vertices; projectile collider remains logical.</summary>
[DefaultExecutionOrder(2500)]public sealed class ProjectedProjectileView:MonoBehaviour
{
 IProjectedActorContext context;SpriteRenderer source;GameObject display;Mesh mesh;Material material;Sprite lastSprite;Vector2[] vertices;Vector3[] projected;
 void Start(){context=RegionQueries.Projected;if(context==null)return;source=GetComponentInChildren<SpriteRenderer>();if(source==null)return;display=new GameObject("Projected real projectile");display.transform.SetParent(context.PresentationRoot,false);mesh=new Mesh();display.AddComponent<MeshFilter>().sharedMesh=mesh;material=new Material(context.ActorShader);var r=display.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.sortingOrder=90;source.forceRenderingOff=true;}
 void LateUpdate(){if(display==null||source==null||context.Projection==null)return;display.SetActive(context.PresentationReady);var s=source.sprite;if(s==null)return;if(s!=lastSprite){lastSprite=s;vertices=s.vertices;projected=new Vector3[vertices.Length];mesh.Clear();mesh.vertices=projected;mesh.uv=s.uv;var tri=s.triangles;var indices=new int[tri.Length];for(int i=0;i<tri.Length;i++)indices[i]=tri[i];mesh.triangles=indices;material.mainTexture=s.texture;}for(int i=0;i<vertices.Length;i++)projected[i]=context.Render(source.transform.TransformPoint(vertices[i]));mesh.vertices=projected;material.color=source.color;mesh.RecalculateBounds();}
 void OnDestroy(){if(source!=null)source.forceRenderingOff=false;if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);if(display!=null)Destroy(display);}
}
