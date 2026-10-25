using UnityEngine;

namespace SporeGobbo.Geometry.Preview
{
    /// <summary>Preview display controls only. Baked meshes remain visible in Edit Mode without entering Play.</summary>
    [ExecuteAlways]
    public sealed class CaveGeometryPreview : MonoBehaviour
    {
        public bool showGrid;
        public bool showContours = true;
        public bool showWallFaces = true;
        public bool showForegroundLips = true;
        public MeshRenderer[] gridRenderers;
        public MeshRenderer[] contourRenderers;
        public MeshRenderer[] wallRenderers;
        public MeshRenderer[] lipRenderers;
        public Camera previewCamera;

        bool visibilityApplied,lastGrid,lastContours,lastWalls,lastLips;
        MeshRenderer[] lastGridArray,lastContourArray,lastWallArray,lastLipArray;
        void OnEnable(){visibilityApplied=false;ApplyVisibility();}
        void OnValidate() => ApplyVisibility();
        public void ApplyVisibility()
        {
            using(GeometryTrace.Measure(GeometryPhase.Visibility))
            {
            if(visibilityApplied&&lastGrid==showGrid&&lastContours==showContours&&lastWalls==showWallFaces&&lastLips==showForegroundLips&&
                ReferenceEquals(lastGridArray,gridRenderers)&&ReferenceEquals(lastContourArray,contourRenderers)&&ReferenceEquals(lastWallArray,wallRenderers)&&ReferenceEquals(lastLipArray,lipRenderers))return;
            visibilityApplied=true;lastGrid=showGrid;lastContours=showContours;lastWalls=showWallFaces;lastLips=showForegroundLips;
            lastGridArray=gridRenderers;lastContourArray=contourRenderers;lastWallArray=wallRenderers;lastLipArray=lipRenderers;
            Set(gridRenderers,showGrid);Set(contourRenderers,showContours);
            Set(wallRenderers,showWallFaces);Set(lipRenderers,showForegroundLips);
        
            }
        }
        static void Set(MeshRenderer[] renderers,bool visible)
        { if(renderers!=null)foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=visible; }
        void StopFollowing(){var live=GetComponent<PlayableCavePreview>();if(live!=null)live.followPlayer=false;}
        public void Overview()
        { StopFollowing();if(previewCamera!=null){previewCamera.transform.position=new Vector3(48,-31,-10);previewCamera.orthographicSize=35;} }
        public void TunnelDetail()
        { StopFollowing();if(previewCamera!=null){previewCamera.transform.position=new Vector3(70,-23,-10);previewCamera.orthographicSize=12;} }
        public void ChamberDetail()
        { StopFollowing();if(previewCamera!=null){previewCamera.transform.position=new Vector3(50,-39,-10);previewCamera.orthographicSize=14;} }
        void OnGUI()
        {
            if(!Application.isPlaying||GetComponent<Stage4CameraPreview>()!=null&&!GetComponent<Stage4CameraPreview>().ShowDebug)return;
            GUILayout.BeginArea(new Rect(12,12,280,285),GUI.skin.box);
            GUILayout.Label("SPORE GOBBO — GEOMETRY PREVIEW");
            GUILayout.Label("Uniform square grid · reference wall height");
            showGrid=GUILayout.Toggle(showGrid,"Logical cell grid");
            showContours=GUILayout.Toggle(showContours,"Rounded contour outlines");
            showWallFaces=GUILayout.Toggle(showWallFaces,"Back walls and side transitions");
            showForegroundLips=GUILayout.Toggle(showForegroundLips,"Foreground lips");
            if(GetComponent<Stage4CameraPreview>()==null){
            if(GUILayout.Button("Whole cave"))Overview();
            if(GUILayout.Button("Three-cell tunnel detail"))TunnelDetail();
            if(GUILayout.Button("Gobbo and pillar detail"))ChamberDetail();}
            GUILayout.Label(GetComponent<PlayableCavePreview>()==null?"Stage 2 reference — static geometry.":"Stage 3 controls on the right. Views disable follow.");
            GUILayout.EndArea();ApplyVisibility();
        }
    }
}
