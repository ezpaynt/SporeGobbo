using System;
using UnityEngine;

[Serializable]
public class SporeSaveSlotData
{
    public int slotIndex = 1;
    public bool hasSave = false;

    public string saveName = "New Gobbo Camp";
    public string createdAt = "";
    public string lastPlayedAt = "";

    public int runNumber = 1;
    public int ownedBuddyCount = 0;
    public int deadBuddyCount = 0;

    public string lastSceneName = "";
    public string nextSceneName = "SampleScene";

    public static SporeSaveSlotData CreateNew(int slotIndex, string firstSceneName)
    {
        string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        return new SporeSaveSlotData
        {
            slotIndex = Mathf.Clamp(slotIndex, 1, 3),
            hasSave = true,
            saveName = "Gobbo Camp " + slotIndex,
            createdAt = now,
            lastPlayedAt = now,
            runNumber = 1,
            ownedBuddyCount = 0,
            deadBuddyCount = 0,
            lastSceneName = "",
            nextSceneName = firstSceneName
        };
    }

    public string GetButtonLabel()
    {
        if (!hasSave)
            return "Slot " + slotIndex + " — Empty";

        return "Slot " + slotIndex +
               " — Run " + runNumber +
               " — " + ownedBuddyCount + " Gobbos" +
               " — " + deadBuddyCount + " Bones";
    }
}
