using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
namespace SporeGobbo.Safety
{
    // Normal player saves retain their original location and format. Editor tests fail closed.
    public static class Persistence
    {
        static readonly Dictionary<string, object> prefs = new Dictionary<string, object>();
        static bool RequiresIsolation
        {
            get
            {
                if (Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-runTests") >= 0) return true;
#if UNITY_INCLUDE_TESTS && !UNITY_EDITOR
                return true;
#endif
#if UNITY_EDITOR
                if (!Application.isPlaying) return true;
                // Pinned test-framework API; fail closed if a package upgrade removes it.
                var api = Type.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner", false);
                if (api == null) throw new InvalidOperationException("Cannot verify Editor test-run state");
                var active = api.GetMethod("IsRunActive", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (active == null) throw new InvalidOperationException("Unsupported test-framework: cannot verify isolation");
                if ((bool)active.Invoke(null, null)) return true;
#endif
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                string first = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(0);
                return Preview(scene) || Preview(first);
            }
        }
        static bool Preview(string path) => path.StartsWith("Assets/Stage", StringComparison.Ordinal) || path.StartsWith("Assets/Terrain/", StringComparison.Ordinal);
        public static string ExperimentRoot => Root();
        public static string ExperimentPath(params string[] parts) => Within(Root(), Path.Combine(parts));
        public static bool Isolated => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SPORE_TEST_ROOT"));
        static string Root()
        {
            string root = Environment.GetEnvironmentVariable("SPORE_TEST_ROOT");
            string token = Environment.GetEnvironmentVariable("SPORE_TEST_TOKEN");
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(token)) throw new InvalidOperationException("Persistence isolation missing. Use safety_checkpoint/run.py; real storage is forbidden in tests/previews.");
            string full = Path.GetFullPath(root);
            if (!Path.IsPathRooted(root) || Path.GetFileName(full) != token || token.Length != 32) throw new InvalidOperationException("Invalid test root identity");
            NoLinks(full);
            if (!File.Exists(Path.Combine(full, ".spore-owned")) || File.ReadAllText(Path.Combine(full, ".spore-owned")) != token) throw new InvalidOperationException("Unowned test storage");
            return full;
        }
        public static void NoLinks(string path)
        {
            for (var p = new DirectoryInfo(Path.GetFullPath(path)); p != null; p = p.Parent)
                try { if ((File.GetAttributes(p.FullName) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse points forbidden: " + p.FullName); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
        }
        public static string Within(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) throw new IOException("Relative child path required");
            string canonical = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(canonical, relative));
            if (!path.StartsWith(canonical + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Path escapes owned root");
            NoLinks(path);
            return path;
        }
        public static string CampaignFolder
        {
            get
            {
                if (Isolated || RequiresIsolation) return Within(Root(), "Saves");
                return Path.Combine(Application.persistentDataPath, "Saves");
            }
        }
        public static string SlotPath(string folder, string file) => Isolated || RequiresIsolation ? Within(folder, file) : Path.Combine(folder, file);
        static bool MemoryPrefs { get { if (Isolated || RequiresIsolation) { Root(); return true; } return false; } }
        public static int GetInt(string key, int fallback = 0) => MemoryPrefs ? (prefs.TryGetValue(key,out var value) && value is int i ? i : fallback) : PlayerPrefs.GetInt(key,fallback);
        public static string GetString(string key,string fallback = "") => MemoryPrefs ? (prefs.TryGetValue(key,out var value) && value is string s ? s : fallback) : PlayerPrefs.GetString(key,fallback);
        public static bool HasKey(string key) => MemoryPrefs ? prefs.ContainsKey(key) : PlayerPrefs.HasKey(key);
        public static void SetInt(string key,int value) { if (MemoryPrefs) prefs[key]=value; else PlayerPrefs.SetInt(key,value); }
        public static void SetString(string key,string value) { if (MemoryPrefs) prefs[key]=value; else PlayerPrefs.SetString(key,value); }
        public static void DeleteKey(string key) { if (MemoryPrefs) prefs.Remove(key); else PlayerPrefs.DeleteKey(key); }
        public static void Save() { if (!MemoryPrefs) PlayerPrefs.Save(); }
        public static void ClearTestPrefs() { Root(); prefs.Clear(); }
    }
}
