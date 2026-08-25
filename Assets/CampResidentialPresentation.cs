using System.Collections.Generic;
using UnityEngine;

public sealed class CampResidentialPresentation : MonoBehaviour
{
    Transform[] stageOneAnchors;
    GameObject[] stageOneMarkers;

    public void Initialize(HandcraftedCampTerrain terrain)
    {
        if (terrain == null || stageOneAnchors != null) return;
        List<Vector2Int> cells = terrain.GetResidentialPresentationCells(1);
        stageOneAnchors = new Transform[cells.Count];
        stageOneMarkers = new GameObject[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            GameObject anchor = new GameObject("ResidentialStage1Slot_" + (i + 1));
            anchor.transform.SetParent(transform, false);
            anchor.transform.position = terrain.CellToWorld(cells[i]);
            CampActivityPoint activity = anchor.AddComponent<CampActivityPoint>();
            activity.kind = CampActivityKind.ResidentialRest;
            activity.available = false;
            activity.residentialStage = 1;
            activity.residentialSlot = i + 1;
            stageOneAnchors[i] = anchor.transform;

            GameObject marker = new GameObject("ResidentialStage1Marker_" + (i + 1));
            marker.transform.SetParent(anchor.transform, false);
            marker.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            Texture2D texture = new Texture2D(1, 1) { name = "ResidentialSlotDebugTexture_" + (i + 1) };
            texture.SetPixel(0, 0, new Color(0.2f, 0.9f, 1f, 0.9f));
            texture.Apply();
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            renderer.sortingOrder = 28;
            stageOneMarkers[i] = marker;
        }
    }

    public void ApplyProgress(int residentialStage, int establishedSlots, ISet<int> occupiedSlots = null)
    {
        if (stageOneMarkers == null) return;
        for (int i = 0; i < stageOneMarkers.Length; i++)
        {
            bool available = SporeGobbo.CampLifecycle.CampSpatialPolicy
                .ShouldExposeResidentialSlot(i + 1, residentialStage, establishedSlots);
            if (stageOneMarkers[i] != null)
            {
                stageOneMarkers[i].SetActive(SporeGobbo.CampLifecycle.CampSpatialPolicy
                    .ShouldExposeResidentialSlot(i + 1, residentialStage, establishedSlots));
                SpriteRenderer renderer = stageOneMarkers[i].GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.color = occupiedSlots != null && occupiedSlots.Contains(i + 1)
                    ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            CampActivityPoint point = stageOneAnchors[i] != null
                ? stageOneAnchors[i].GetComponent<CampActivityPoint>() : null;
            if (point != null) point.available = available;
        }
    }

    public Transform GetRestPoint(int slotId)
    {
        int index = slotId - 1;
        if (stageOneAnchors == null || index < 0 || index >= stageOneAnchors.Length) return null;
        CampActivityPoint point = stageOneAnchors[index] != null
            ? stageOneAnchors[index].GetComponent<CampActivityPoint>() : null;
        return point != null && point.available ? stageOneAnchors[index] : null;
    }

    public Transform[] GetEstablishedRestPoints(int residentialStage, int establishedSlots)
    {
        if (residentialStage < 1 || stageOneAnchors == null) return null;
        int count = Mathf.Clamp(establishedSlots, 0, stageOneAnchors.Length);
        Transform[] result = new Transform[count];
        System.Array.Copy(stageOneAnchors, result, count);
        return result;
    }
}
