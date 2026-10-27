#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using SporeGobbo.Safety;
/// <summary>Read-only real-service fixtures. Import uses the existing save/campaign authority.</summary>
public static class DevelopmentCampaignFixtures
{
    public static void Import(int slot)
    {
        if(!V1RuntimeProfile.AllowsTestActions||!Persistence.Isolated||SceneManager.GetActiveScene().name!="MainMenu"||slot<1||slot>3)
            throw new InvalidOperationException("Fixture import requires the protected Main Menu");
        if(SporeSaveManager.InspectSlot(slot)!=SporeSaveManager.SlotStatus.Missing)throw new InvalidOperationException("Development slot is occupied; no implicit overwrite");
        string name=slot==1?"FreshA":slot==2?"EstablishedB":"RecoveryC";
        var fixture=Resources.Load<TextAsset>("Stage61Fixtures/"+name);
        if(fixture==null)throw new InvalidOperationException("Missing reviewed fixture "+name);
        DevelopmentCampaignSnapshot.Decode(fixture.text,slot,out var data);
        SporeSaveManager.SaveSlot(data);SporeSaveManager.ApplySlotToGameState(data);
        UnityEngine.Object.FindAnyObjectByType<MainMenuController>()?.ShowMainMenu();
    }
}
#endif
