using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;

/// <summary>Copies the real animated Sprite's topology/UVs. Physics root never enters render coordinates.</summary>
[DefaultExecutionOrder(2500)]
public sealed class Stage59ActorView:MonoBehaviour
{
    public Stage59Preview Owner;
    public bool Outline=true;
    public static int OwnedMeshes,OwnedMaterials;
    SpriteRenderer source;Collider2D body;GameObject rendered;Mesh mesh,shadow;
    Material material,shadowMaterial,lineMaterial;MeshRenderer renderer;LineRenderer outline;
    Sprite lastSprite;Vector2[] spriteVertices;Vector3[] projected;Color[] colors;
    readonly int[] shadowTriangles=new int[48];
    readonly Vector3[] corners=new Vector3[5];readonly List<Vector3> shadowVertices=new List<Vector3>(18);
    public Vector2 ProjectedFoot {get;private set;}
    public float RenderedBottomY {get;private set;}
    public double UpdateMs {get;private set;}
    public int SpriteChanges {get;private set;}
    public Vector2 PhysicalFoot=>new Vector2(transform.position.x,body.bounds.min.y);
    void Start()=>Initialize();
    public void Initialize()
    {
        if(rendered!=null)return;
        source=GetComponentInChildren<SpriteRenderer>();body=GetComponent<Collider2D>();
        rendered=new GameObject(name+" projected animated sprite");rendered.transform.SetParent(Owner.transform,false);
        var filter=rendered.AddComponent<MeshFilter>();mesh=new Mesh{name=name+" animated projection"};mesh.MarkDynamic();filter.sharedMesh=mesh;
        renderer=rendered.AddComponent<MeshRenderer>();material=new Material(Owner.SpriteShader);renderer.sharedMaterial=material;
        var shadowGo=new GameObject(name+" diagnostic ground shadow");shadowGo.transform.SetParent(rendered.transform,false);
        shadow= new Mesh{name="Projected foot shadow"};shadowGo.AddComponent<MeshFilter>().sharedMesh=shadow;
        var sr=shadowGo.AddComponent<MeshRenderer>();shadowMaterial=new Material(Owner.SpriteShader);shadowMaterial.color=new Color(0,0,0,.28f);sr.sharedMaterial=shadowMaterial;sr.sortingOrder=9;
        var lineGo=new GameObject(name+" projected collider bounds");lineGo.transform.SetParent(rendered.transform,false);
        outline=lineGo.AddComponent<LineRenderer>();lineMaterial=new Material(Owner.SpriteShader);outline.sharedMaterial=lineMaterial;outline.startColor=outline.endColor=new Color(.4f,1,.3f,.8f);outline.startWidth=outline.endWidth=.035f;outline.positionCount=5;outline.useWorldSpace=true;outline.sortingOrder=80;
        for(int i=0;i<16;i++){shadowTriangles[i*3]=0;shadowTriangles[i*3+1]=i+1;shadowTriangles[i*3+2]=i+2;}
        OwnedMeshes+=2;OwnedMaterials+=3;source.forceRenderingOff=true;
    }
    public void Draw(int sortingOrder)
    {
        if(source==null||Owner.Live.Snapshot==null||body==null)return;
        using(new ActorTerrainBinding.Scope("SG59.ActorPresentation"))
        {
            long t=System.Diagnostics.Stopwatch.GetTimestamp();var map=Owner.Live.Snapshot;
            var foot=Owner.Authority.ToLogical(PhysicalFoot);var anchor=map.Project(foot);ProjectedFoot=new Vector2((float)anchor.X,(float)-anchor.Y);
            var scale=Stage4CameraPreview.ActorScale(map,foot,Owner.Live.settings.actorMode);
            var sprite=source.sprite;
            if(sprite==null)return;
            if(sprite!=lastSprite)
            {
                lastSprite=sprite;SpriteChanges++;spriteVertices=sprite.vertices;projected=new Vector3[spriteVertices.Length];colors=new Color[spriteVertices.Length];
                mesh.Clear();mesh.vertices=new Vector3[spriteVertices.Length];mesh.uv=sprite.uv;var tri=sprite.triangles;int[] indices=new int[tri.Length];for(int i=0;i<indices.Length;i++)indices[i]=tri[i];mesh.triangles=indices;material.mainTexture=sprite.texture;
            }
            float bottom=float.PositiveInfinity;foreach(var v in spriteVertices)bottom=Mathf.Min(bottom,v.y);RenderedBottomY=float.PositiveInfinity;
            for(int i=0;i<projected.Length;i++)
            {
                var v=spriteVertices[i];float x=v.x*(source.flipX?-1:1)*transform.lossyScale.x/.6f;
                float y=(v.y-bottom)*transform.lossyScale.y/.6f;
                projected[i]=new Vector3(ProjectedFoot.x+x*scale.x,ProjectedFoot.y+y*scale.y,0);colors[i]=source.color;RenderedBottomY=Mathf.Min(RenderedBottomY,projected[i].y);
            }
            mesh.vertices=projected;mesh.colors=colors;mesh.RecalculateBounds();renderer.sortingOrder=sortingOrder;renderer.enabled=Owner.Live.Ready;
            var bounds=body.bounds;var lo=bounds.min;var hi=bounds.max;
            corners[0]=Owner.Render(new Vector2(lo.x,lo.y));corners[1]=Owner.Render(new Vector2(hi.x,lo.y));corners[2]=Owner.Render(new Vector2(hi.x,hi.y));corners[3]=Owner.Render(new Vector2(lo.x,hi.y));corners[4]=corners[0];outline.SetPositions(corners);outline.enabled=Outline&&Owner.Live.Ready;
            shadowVertices.Clear();shadowVertices.Add(new Vector3(ProjectedFoot.x,ProjectedFoot.y));
            for(int i=0;i<=16;i++){float a=i*Mathf.PI/8;shadowVertices.Add(Owner.Render(PhysicalFoot+new Vector2(Mathf.Cos(a)*bounds.extents.x,Mathf.Sin(a)*bounds.extents.y*.25f)));}
            shadow.Clear();shadow.SetVertices(shadowVertices);shadow.triangles=shadowTriangles;
            UpdateMs=StreamingGeometry.Elapsed(t);
        }
    }
    void OnDestroy()=>Release();
    public void Release()
    {
        if(source!=null)source.forceRenderingOff=false;
        if(mesh!=null)OwnedMeshes--;if(shadow!=null)OwnedMeshes--;if(material!=null)OwnedMaterials--;if(shadowMaterial!=null)OwnedMaterials--;if(lineMaterial!=null)OwnedMaterials--;
        foreach(var o in new UnityEngine.Object[]{mesh,shadow,material,shadowMaterial,lineMaterial,rendered})if(o!=null){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        mesh=null;shadow=null;material=null;shadowMaterial=null;lineMaterial=null;rendered=null;source=null;body=null;
    }
}
