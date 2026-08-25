using UnityEngine;
using SporeGobbo.CampLifecycle;

public enum CampActivityKind
{
    ResidentialRest,
    FireSocial,
    GeneralWander
}

public sealed class CampActivityPoint : MonoBehaviour
{
    public CampActivityKind kind;
    public bool available;
    public int residentialStage;
    public int residentialSlot;

    public static bool CanUse(CampActivityKind pointKind, bool pointAvailable,
        int pointResidentialSlot, int gobboResidentialSlot)
    {
        return CampArrivalPolicy.CanUseActivityPoint(pointKind == CampActivityKind.ResidentialRest,
            pointAvailable, pointResidentialSlot, gobboResidentialSlot);
    }

    public bool IsValidFor(GobboUnitSaveData gobbo, HandcraftedCampTerrain terrain)
    {
        if (!CanUse(kind, available, residentialSlot, gobbo != null ? gobbo.campResidentialSlotId : 0)) return false;
        return terrain == null || !terrain.IsBlocked(terrain.WorldToCell(transform.position));
    }

    public static CampActivityPoint[] GetValidFor(GobboUnitSaveData gobbo)
    {
        HandcraftedCampTerrain terrain = Object.FindAnyObjectByType<HandcraftedCampTerrain>();
        CampActivityPoint[] all = Object.FindObjectsByType<CampActivityPoint>(FindObjectsInactive.Exclude);
        System.Collections.Generic.List<CampActivityPoint> valid = new System.Collections.Generic.List<CampActivityPoint>();
        foreach (CampActivityPoint point in all)
            if (point != null && point.IsValidFor(gobbo, terrain)) valid.Add(point);
        return valid.ToArray();
    }

    public static Transform ChooseSnapshotPoint(GobboUnitSaveData gobbo, int stableIndex)
    {
        CampActivityPoint[] valid = GetValidFor(gobbo);
        if (valid.Length == 0) return null;
        int hash = gobbo != null && !string.IsNullOrWhiteSpace(gobbo.uniqueId)
            ? gobbo.uniqueId.GetHashCode() : stableIndex;
        return valid[Mathf.Abs(hash == int.MinValue ? 0 : hash) % valid.Length].transform;
    }
}
