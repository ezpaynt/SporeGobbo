using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temporary debug helper for figuring out why player death is not opening the death screen.
/// Put this on the runtime player Gobbo next to GobboController + PlayerDeathWatcher.
/// Remove it after the death flow is fixed.
/// </summary>
public class PlayerDeathDebugProbe : MonoBehaviour
{
    [Header("Debug")]
    public bool logEverySecond = true;
    public bool logLifecycle = true;
    public bool logWhenHealthLooksDead = true;
    public bool inspectPlayerDeathWatcher = true;

    private float nextLogTime;
    private bool alreadyLoggedDeadLike;

    private void Awake()
    {
        if (logLifecycle) Log("Awake");
    }

    private void OnEnable()
    {
        if (logLifecycle) Log("OnEnable");
    }

    private void Start()
    {
        if (logLifecycle) Log("Start");
        DumpWatcherState("Start");
    }

    private void Update()
    {
        if (logEverySecond && Time.unscaledTime >= nextLogTime)
        {
            nextLogTime = Time.unscaledTime + 1f;
            Log($"Heartbeat activeSelf={gameObject.activeSelf}, activeInHierarchy={gameObject.activeInHierarchy}, enabled={enabled}, timeScale={Time.timeScale}");
            DumpWatcherState("Heartbeat");
        }

        if (logWhenHealthLooksDead && !alreadyLoggedDeadLike)
        {
            if (TryReadLikelyHealth(out float hp, out string source) && hp <= 0f)
            {
                alreadyLoggedDeadLike = true;
                Log($"Health looks dead: {source}={hp}. If no PlayerDeathWatcher handled log appears after this, watcher is missing/suppressed/not receiving death.");
                DumpWatcherState("HealthDead");
            }
        }
    }

    private void OnDisable()
    {
        if (logLifecycle) Log("OnDisable");
        DumpWatcherState("OnDisable");
    }

    private void OnDestroy()
    {
        if (logLifecycle) Log("OnDestroy");
        DumpWatcherState("OnDestroy");
    }

    private void Log(string msg)
    {
        Debug.Log($"[PlayerDeathDebugProbe] {msg} | scene={SceneManager.GetActiveScene().name} | object={name}", this);
    }

    private void DumpWatcherState(string where)
    {
        if (!inspectPlayerDeathWatcher) return;

        Component watcher = GetComponent("PlayerDeathWatcher");
        if (watcher == null)
        {
            Debug.LogWarning($"[PlayerDeathDebugProbe] {where}: PlayerDeathWatcher component NOT FOUND on {name}.", this);
            return;
        }

        Type t = watcher.GetType();
        bool behaviourEnabled = watcher is Behaviour b && b.enabled;
        string summary = $"{where}: PlayerDeathWatcher found, enabled={behaviourEnabled}";

        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            string lower = f.Name.ToLowerInvariant();
            if (!lower.Contains("suppress") && !lower.Contains("handled") && !lower.Contains("death") && !lower.Contains("scene"))
                continue;

            try
            {
                object target = f.IsStatic ? null : watcher;
                object value = f.GetValue(target);
                summary += $", {f.Name}={FormatValue(value)}";
            }
            catch { }
        }

        Debug.Log($"[PlayerDeathDebugProbe] {summary}", this);
    }

    private bool TryReadLikelyHealth(out float value, out string source)
    {
        value = 0f;
        source = string.Empty;

        string[] names =
        {
            "currentHealth", "health", "hp", "hitPoints", "currentHp", "CurrentHealth", "Health", "HP"
        };

        Component[] components = GetComponents<Component>();
        foreach (Component c in components)
        {
            if (c == null) continue;
            Type t = c.GetType();

            foreach (string n in names)
            {
                FieldInfo f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null && TryConvertNumber(f.GetValue(c), out value))
                {
                    source = $"{t.Name}.{f.Name}";
                    return true;
                }

                PropertyInfo p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.GetIndexParameters().Length == 0 && TryConvertNumber(p.GetValue(c, null), out value))
                {
                    source = $"{t.Name}.{p.Name}";
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryConvertNumber(object raw, out float value)
    {
        value = 0f;
        if (raw == null) return false;

        try
        {
            if (raw is int i) { value = i; return true; }
            if (raw is float f) { value = f; return true; }
            if (raw is double d) { value = (float)d; return true; }
            if (raw is long l) { value = l; return true; }
        }
        catch { }

        return false;
    }

    private string FormatValue(object value)
    {
        if (value == null) return "null";
        return value.ToString();
    }
}
