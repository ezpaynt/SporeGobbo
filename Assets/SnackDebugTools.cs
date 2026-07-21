using System.Text;
using UnityEngine;

public class SnackDebugTools : MonoBehaviour
{
    [Header("Prototype IDs")]
    public string healSnackId = "test_snack_heal_01";
    public string maxHpSnackId = "test_snack_max_hp_01";
    public string attackSnackId = "test_snack_attack_01";

    [Header("Test Target")]
    public string buddyTargetId = "";
    public float retreatLossPercent = 0.75f;

    [ContextMenu("Grant Persistent Heal Snack")]
    public void GrantPersistentHealSnack()
    {
        GrantPersistent(healSnackId);
    }

    [ContextMenu("Add Run-Held Heal Snack")]
    public void AddRunHeldHealSnack()
    {
        AddRunHeld(healSnackId);
    }

    [ContextMenu("Print Persistent Stacks")]
    public void PrintPersistentStacks()
    {
        Debug.Log("[SnackDebugTools] Persistent stacks:\n" + FormatStacks(InventoryService.GetStacks(GameState.Instance)));
    }

    [ContextMenu("Print Run-Held Stacks")]
    public void PrintRunHeldStacks()
    {
        Debug.Log("[SnackDebugTools] Run-held stacks:\n" + FormatStacks(RunSnackLootService.GetRunStacks(GameState.Instance)));
    }

    [ContextMenu("Print Retreat Calculations 1-5")]
    public void PrintRetreatCalculations()
    {
        StringBuilder builder = new StringBuilder();
        float percent = Mathf.Clamp01(retreatLossPercent);
        for (int i = 1; i <= 5; i++)
        {
            int lost = RunSnackLootService.CalculateRetreatLoss(i, percent);
            builder.AppendLine(i + " collected -> lose " + lost + ", keep " + Mathf.Max(0, i - lost));
        }
        Debug.Log("[SnackDebugTools] Retreat calculations at " + percent + ":\n" + builder);
    }

    [ContextMenu("Test Duplicate Success Finalization")]
    public void TestDuplicateSuccessFinalization()
    {
        GameState state = GameState.Instance;
        bool first = RunSnackLootService.FinalizeSuccess(state);
        bool second = RunSnackLootService.FinalizeSuccess(state);
        Debug.Log("[SnackDebugTools] Duplicate success finalization first=" + first + ", second=" + second +
                  ", campCycle=" + (state != null ? state.campCycleNumber : -1));
    }

    [ContextMenu("Feed Heal Snack To Leader")]
    public void FeedHealSnackToLeader()
    {
        CampfireSnackResult result = CampfireSnackService.CommitFeeding(GameState.Instance, healSnackId, ItemTargetType.Leader, "");
        Debug.Log("[SnackDebugTools] Feed leader result: " + result.success + " | " + result.message);
    }

    [ContextMenu("Feed Heal Snack To Buddy Target")]
    public void FeedHealSnackToBuddy()
    {
        CampfireSnackResult result = CampfireSnackService.CommitFeeding(GameState.Instance, healSnackId, ItemTargetType.Buddy, buddyTargetId);
        Debug.Log("[SnackDebugTools] Feed buddy result: " + result.success + " | " + result.message);
    }

    public void GrantPersistent(string itemId)
    {
        ItemDefinition item = ItemDatabase.Get(itemId);
        bool ok = InventoryService.TryAdd(GameState.Instance, item, 1, true);
        Debug.Log("[SnackDebugTools] Grant persistent " + itemId + ": " + ok);
    }

    public void AddRunHeld(string itemId)
    {
        ItemDefinition item = ItemDatabase.Get(itemId);
        bool ok = RunSnackLootService.AddRunSnack(GameState.Instance, item, 1);
        Debug.Log("[SnackDebugTools] Add run-held " + itemId + ": " + ok);
    }

    string FormatStacks(System.Collections.Generic.IEnumerable<InventoryStackSaveData> stacks)
    {
        StringBuilder builder = new StringBuilder();
        if (stacks != null)
        {
            foreach (InventoryStackSaveData stack in stacks)
            {
                if (stack == null) continue;
                builder.AppendLine(stack.itemId + ": " + stack.quantity);
            }
        }
        return builder.Length > 0 ? builder.ToString().TrimEnd() : "(empty)";
    }
}
