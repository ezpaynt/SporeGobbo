using System;
using UnityEngine;
using SporeGobbo.Safety;

/// <summary>Immutable storage choice and optional tool capabilities. Never selects gameplay destinations or correctness.</summary>
public static class V1RuntimeProfile
{
    static readonly string selectedStorage = ParseStorageProfile(Environment.GetEnvironmentVariable("SPORE_V1_PROFILE"));
    static bool legacyTools;
    public static string StorageProfile => selectedStorage;
    public static bool DevelopmentStorage => selectedStorage == "development";
    public static string CampaignRelativeFolder => DevelopmentStorage ? "Development/V1/Campaigns" : "V1/Campaigns";
    public static string PreferencePrefix => DevelopmentStorage ? "SporeGobbo_DevelopmentV1_" : "SporeGobbo_V1_";
    public static string CampaignIdPrefix => DevelopmentStorage ? "development-v1-" : "v1-";
    public static bool DevelopmentToolsEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Persistence.Isolated && (legacyTools || Environment.GetEnvironmentVariable("SPORE_V1_DEVTOOLS") == "1");
#else
            return false;
#endif
        }
    }
    public static bool EvidenceEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Persistence.Isolated && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SPORE_STAGE61_CAPTURE"));
#else
            return false;
#endif
        }
    }
    public static bool AllowsTestActions => DevelopmentToolsEnabled || EvidenceEnabled;
    public static string ParseStorageProfile(string value)
    {
        if (string.IsNullOrEmpty(value) || value == "normal") return "normal";
        if (value == "development") return value;
        throw new InvalidOperationException("Unknown V1 storage profile. Expected normal or development.");
    }
    // Retained historical drivers may request tools, but cannot change immutable storage or normal routes.
    public static void EnableLegacyDevelopmentTools()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Persistence.Isolated) throw new InvalidOperationException("Development tools require owned isolated storage");
        _ = Persistence.ExperimentRoot;
        legacyTools = true;
#else
        throw new InvalidOperationException("Development tools are unavailable in this build");
#endif
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTools() => legacyTools = false;
}
