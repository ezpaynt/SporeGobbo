using System;
using UnityEngine;

[Serializable]
public class RunSnackSummaryEntry
{
    public string itemId = "";
    public int collectedQuantity = 0;
    public int lostQuantity = 0;
    public int retainedQuantity = 0;

    public RunSnackSummaryEntry() { }

    public RunSnackSummaryEntry(string itemId, int collectedQuantity, int lostQuantity, int retainedQuantity)
    {
        this.itemId = ItemIdUtility.Normalize(itemId);
        this.collectedQuantity = Mathf.Max(0, collectedQuantity);
        this.lostQuantity = Mathf.Max(0, lostQuantity);
        this.retainedQuantity = Mathf.Max(0, retainedQuantity);
    }

    public RunSnackSummaryEntry Clone()
    {
        return new RunSnackSummaryEntry(itemId, collectedQuantity, lostQuantity, retainedQuantity);
    }
}
