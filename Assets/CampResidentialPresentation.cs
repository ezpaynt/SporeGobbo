using System.Collections.Generic;
using UnityEngine;
using SporeGobbo.CampLifecycle;

public sealed class CampResidentialPresentation : MonoBehaviour
{
    readonly Dictionary<int, Transform> anchorsByGlobalSlot = new Dictionary<int, Transform>();
    readonly Dictionary<int, GameObject> markersByGlobalSlot = new Dictionary<int, GameObject>();
    readonly List<int> orderedGlobalSlots = new List<int>();
    bool initialized;

    public void Initialize(HandcraftedCampTerrain terrain)
    {
        if (terrain == null || initialized) return;
        CampResidentialCatalog catalog = terrain.GetResidentialCatalog();
        if (catalog == null) return;
        initialized = true;
        foreach (CampResidentialRoomDefinition room in catalog.Rooms)
        foreach (CampResidentialSlotDefinition slot in room.Slots)
        {
            int slotId = slot.GlobalSlotId;
            GameObject anchor = new GameObject("ResidentialRoom_" + room.RoomId + "_Slot_" + slotId);
            anchor.transform.SetParent(transform, false);
            anchor.transform.position = terrain.CellToWorld(new Vector2Int(slot.RestCell.x, slot.RestCell.y));
            CampActivityPoint activity = anchor.AddComponent<CampActivityPoint>();
            activity.kind = CampActivityKind.ResidentialRest;
            activity.available = false;
            activity.residentialSlot = slotId;
            anchorsByGlobalSlot[slotId] = anchor.transform;
            orderedGlobalSlots.Add(slotId);

            GameObject marker = new GameObject("ResidentialRoom_" + room.RoomId + "_Marker_" + slotId);
            marker.transform.SetParent(anchor.transform, false);
            marker.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            Texture2D texture = new Texture2D(1, 1) { name = "ResidentialSlotDebugTexture_" + slotId };
            texture.SetPixel(0, 0, new Color(0.2f, 0.9f, 1f, 0.9f));
            texture.Apply();
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            renderer.sortingOrder = 28;
            markersByGlobalSlot[slotId] = marker;
        }
        orderedGlobalSlots.Sort();
    }

    public void ApplyProgress(int establishedSlots, ISet<int> occupiedSlots = null)
    {
        if (!initialized) return;
        foreach (int slotId in orderedGlobalSlots)
        {
            bool available = slotId >= 1 && slotId <= establishedSlots;
            markersByGlobalSlot.TryGetValue(slotId, out GameObject marker);
            if (marker != null)
            {
                marker.SetActive(available);
                SpriteRenderer renderer = marker.GetComponent<SpriteRenderer>();
                if (renderer != null) renderer.color = occupiedSlots != null && occupiedSlots.Contains(slotId)
                    ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
            anchorsByGlobalSlot.TryGetValue(slotId, out Transform anchor);
            CampActivityPoint point = anchor != null ? anchor.GetComponent<CampActivityPoint>() : null;
            if (point != null) point.available = available;
        }
    }

    public Transform GetRestPoint(int slotId)
    {
        if (!anchorsByGlobalSlot.TryGetValue(slotId, out Transform anchor) || anchor == null) return null;
        CampActivityPoint point = anchor.GetComponent<CampActivityPoint>();
        return point != null && point.available ? anchor : null;
    }

    public Transform[] GetEstablishedRestPoints(int establishedSlots)
    {
        if (!initialized) return null;
        List<Transform> result = new List<Transform>();
        foreach (int slotId in orderedGlobalSlots)
            if (slotId <= establishedSlots && anchorsByGlobalSlot.TryGetValue(slotId, out Transform anchor))
                result.Add(anchor);
        return result.ToArray();
    }
}
