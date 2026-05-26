using System;
using System.IO;
using UnityEngine;

/// <summary>
/// First-pass save-slot manager for Spore Gobbo.
/// This currently stores slot metadata and remembers last played slot.
/// Later we will expand this to serialize full GameState: player, buddies, camp unlocks,
/// collection book, achievements, shop/meta unlocks, and dead buddy history.
/// </summary>
public static class SporeSaveManager
{
    public const int SlotCount = 3;
    private const string LastSlotKey = "SporeGobbo_LastPlayedSlot";

    public static string SaveFolder
    {
        get
        {
            string folder = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return folder;
        }
    }

    public static string GetSlotPath(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        return Path.Combine(SaveFolder, "slot_" + slotIndex + ".json");
    }

    public static bool HasSave(int slotIndex)
    {
        SporeSaveSlotData data = LoadSlot(slotIndex);
        return data != null && data.hasSave;
    }

    public static SporeSaveSlotData LoadSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        string path = GetSlotPath(slotIndex);

        if (!File.Exists(path))
        {
            return new SporeSaveSlotData
            {
                slotIndex = slotIndex,
                hasSave = false
            };
        }

        try
        {
            string json = File.ReadAllText(path);
            SporeSaveSlotData data = JsonUtility.FromJson<SporeSaveSlotData>(json);

            if (data == null)
            {
                data = new SporeSaveSlotData();
                data.slotIndex = slotIndex;
                data.hasSave = false;
            }

            data.slotIndex = slotIndex;
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Failed to load save slot " + slotIndex + ": " + ex.Message);
            return new SporeSaveSlotData
            {
                slotIndex = slotIndex,
                hasSave = false
            };
        }
    }

    public static void SaveSlot(SporeSaveSlotData data)
    {
        if (data == null)
            return;

        data.slotIndex = Mathf.Clamp(data.slotIndex, 1, SlotCount);
        data.hasSave = true;
        data.lastPlayedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(GetSlotPath(data.slotIndex), json);

        SetLastPlayedSlot(data.slotIndex);
        Debug.Log("Saved slot " + data.slotIndex + " to " + GetSlotPath(data.slotIndex));
    }

    public static SporeSaveSlotData CreateNewGame(int slotIndex, string firstSceneName)
    {
        SporeSaveSlotData data = SporeSaveSlotData.CreateNew(slotIndex, firstSceneName);
        SaveSlot(data);
        return data;
    }

    public static void DeleteSlot(int slotIndex)
    {
        string path = GetSlotPath(slotIndex);
        if (File.Exists(path))
            File.Delete(path);

        if (GetLastPlayedSlot() == slotIndex)
            PlayerPrefs.DeleteKey(LastSlotKey);

        PlayerPrefs.Save();
    }

    public static int GetLastPlayedSlot()
    {
        return PlayerPrefs.GetInt(LastSlotKey, 0);
    }

    public static void SetLastPlayedSlot(int slotIndex)
    {
        slotIndex = Mathf.Clamp(slotIndex, 1, SlotCount);
        PlayerPrefs.SetInt(LastSlotKey, slotIndex);
        PlayerPrefs.Save();
    }

    public static SporeSaveSlotData LoadLastPlayedSlot()
    {
        int slot = GetLastPlayedSlot();

        if (slot <= 0)
            return null;

        SporeSaveSlotData data = LoadSlot(slot);
        return data != null && data.hasSave ? data : null;
    }

    public static bool HasAnySave()
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            if (HasSave(i))
                return true;
        }

        return false;
    }
}
