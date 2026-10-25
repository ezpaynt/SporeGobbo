using UnityEngine;

public sealed class EnemyDamageRequest
{
    public int amount;
    public string gobboSourceId;

    public EnemyDamageRequest(int amount, string gobboSourceId = "")
    {
        this.amount = Mathf.Max(0, amount);
        this.gobboSourceId = string.IsNullOrWhiteSpace(gobboSourceId) ? "" : gobboSourceId.Trim();
    }
}

public interface IEnemyDamageReceiver
{
    void ReceiveEnemyDamage(EnemyDamageRequest request);
}

public static class EnemyDamage
{
    public static bool Apply(GameObject target, int amount, string gobboSourceId = "")
    {
        if (target == null || amount <= 0) return false;

        Transform cursor = target.transform;
        while (cursor != null)
        {
            MonoBehaviour[] behaviours = cursor.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is not IEnemyDamageReceiver receiver) continue;
                receiver.ReceiveEnemyDamage(new EnemyDamageRequest(amount, gobboSourceId));
                return true;
            }
            cursor = cursor.parent;
        }

        return false;
    }
}
