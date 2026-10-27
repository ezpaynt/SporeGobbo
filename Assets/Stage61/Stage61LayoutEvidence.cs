using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.Geometry;
using SporeGobbo.Geometry.Preview;
using SporeGobbo.CampCandidate;
using SporeGobbo.Safety;
/// <summary>Automated geometry acceptance fixture only. Never creates a campaign or substitutes gameplay.</summary>
public sealed class Stage61LayoutEvidence:MonoBehaviour
{
 public Shader terrainShader,spriteShader;WorldRegionPresentation view;RegionTerrainState state;AuthoredCamp definition;
 readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();readonly List<string> checks=new List<string>();string directory;
 Mesh markerMesh;MeshRenderer markerRenderer;Point2 worker;bool showWorker;
 IEnumerator Start()
 {
  directory=Persistence.ExperimentPath("Evidence");Directory.CreateDirectory(directory);
  definition=SettlementCampLayout.Create();state=new RegionTerrainState(definition.Id,192,192,definition);
  Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
  var cameraObject=new GameObject("Geometry proof camera");var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.07f,.055f);camera.transform.position=new Vector3(0,0,-10);
  var palette=ScriptableObject.CreateInstance<Stage55Palette>();owned.Add(palette);var surface=new Texture2D(192,192,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};owned.Add(surface);
  var pixels=new Color[192*192];for(int y=0;y<192;y++)for(int x=0;x<192;x++)pixels[y*192+x]=definition.Protected(x,y)?Color.white:Color.black;surface.SetPixels(pixels);surface.Apply();
  string[] colors={"FloorSlateBlue","UndugDirtMass","DigWallUpper","FreshFractureEdge","PermanentMossBlack","FloorPlumShadow","SicklyLichen"};var materials=new Material[colors.Length];
  for(int i=0;i<materials.Length;i++){var m=new Material(terrainShader);owned.Add(m);m.SetColor("_Color",palette.Color(colors[i]));m.SetFloat("_Layer",i);m.SetTexture("_CampSurface",surface);m.SetColor("_Permanent",palette.Color("PermanentMossBlack"));m.SetColor("_PermanentFace",palette.Color("PermanentWallFace"));m.SetColor("_WallUpper",palette.Color("DigWallUpper"));m.SetColor("_WallLower",palette.Color("DigWallLower"));m.SetColor("_Foot",palette.Color("WallFootOcclusion"));materials[i]=m;}
  view=gameObject.AddComponent<WorldRegionPresentation>();view.Initialize(state.World,materials,terrainShader,camera,new Vector2(90.5f,113.5f));state.Committed+=(cells,revision)=>view.Stream.RefreshPhysicalCells(cells);
  var marker=new GameObject("Geometry positions only, no gameplay actors");markerMesh=new Mesh();owned.Add(markerMesh);marker.AddComponent<MeshFilter>().sharedMesh=markerMesh;markerRenderer=marker.AddComponent<MeshRenderer>();var markerMaterial=new Material(spriteShader);owned.Add(markerMaterial);markerRenderer.sharedMaterial=markerMaterial;markerRenderer.sortingOrder=30;
  var csv=new System.Text.StringBuilder("x,y,category,protected\n");for(int y=0;y<192;y++)for(int x=0;x<192;x++)csv.Append(x).Append(',').Append(y).Append(',').Append(definition.Category(x,y)).Append(',').Append(definition.Protected(x,y)?1:0).Append('\n');File.WriteAllText(Path.Combine(directory,"camp-logical-grid.csv"),csv.ToString());
  int guard=900;while(!view.Ready&&guard-->0)yield return null;if(!view.Ready){Finish("Terrain presentation did not become ready");yield break;}
  checks.Add("PASS reusable streaming renderer ready without preview campaign hosts");yield return Capture("fire-before");
  state.Commit(SettlementCampLayout.RootCells(),state.Revision,c=>true);var catalog=SettlementCampLayout.Catalog(definition);var footprint=catalog.GetSlot(1).ExcavationFootprint;
  worker=new Point2(catalog.GetSlot(1).Approach.x+.5,catalog.GetSlot(1).Approach.y+.5);showWorker=true;
  var partial=footprint.Take(footprint.Count/2).Select(p=>new DualTile(p.x,p.y));state.Commit(partial,state.Revision,c=>true);for(int i=0;i<90;i++)yield return null;yield return Capture("fire-partial-geometry");
  state.Commit(footprint.Select(p=>new DualTile(p.x,p.y)),state.Revision,c=>true);for(int i=0;i<90;i++)yield return null;yield return Capture("fire-first-footprint-open");
  var p=view.Snapshot.Project(worker);double width=view.settings.centerWidth/view.settings.coverage,height=width/(16.0/9);bool onScreen=Math.Abs(p.X)<width*.5&&Math.Abs(p.Y)<height*.5;
  checks.Add((onScreen?"PASS ":"FAIL ")+"first construction staging point inside reference Fire-centered viewport");
  File.WriteAllText(Path.Combine(directory,"layout-metrics.json"),JsonUtility.ToJson(new Metrics{layout=definition.Id,fingerprint=definition.Fingerprint(),worker=new Vector2((float)worker.X,(float)worker.Y),projectedWorker=new Vector2((float)p.X,(float)-p.Y),width=(float)width,height=(float)height},true));Finish("");
 }
 void LateUpdate()
 {
  if(view==null)return;view.Refresh(new Vector2(90.5f,113.5f),Time.deltaTime);if(view.Snapshot==null||markerMesh==null)return;
  var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
  void Box(Point2 p,double w,double h,Color color){int n=vertices.Count;foreach(var q in new[]{new Point2(p.X-w,p.Y-h),new Point2(p.X+w,p.Y-h),new Point2(p.X+w,p.Y+h),new Point2(p.X-w,p.Y+h)}){vertices.Add(view.Project(q));colors.Add(color);}indices.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});}
  Box(definition.Marker("fire").Anchor,1.2,1.3,new Color(1,.55f,.15f));Box(definition.Marker("squad").Anchor,1.3,.6,new Color(.7f,.7f,.5f));if(showWorker)Box(worker,.4,.7,Color.green);
  markerMesh.Clear();markerMesh.SetVertices(vertices);markerMesh.SetTriangles(indices,0);markerMesh.SetColors(colors);markerMesh.RecalculateBounds();
 }
 IEnumerator Capture(string name){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(directory,name+".png"),t.EncodeToPNG());}finally{Destroy(t);}}
 [Serializable]class Result{public bool finished;public string error;public string[] checks;}
 [Serializable]class Metrics{public string layout,fingerprint;public Vector2 worker,projectedWorker;public float width,height;}
 void Finish(string error){File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(new Result{finished=true,error=error,checks=checks.ToArray()},true));Application.Quit(string.IsNullOrEmpty(error)&&checks.All(s=>s.StartsWith("PASS "))?0:1);}
 void OnDestroy(){foreach(var o in owned)if(o!=null)Destroy(o);}
}
