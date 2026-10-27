using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using SporeGobbo.Safety;

/// <summary>File mechanics for SporeSaveManager. No campaign selection, roster or lifecycle ownership.</summary>
public static class CampaignSnapshotStorage
{
    const int MaximumBytes = 16 * 1024 * 1024;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

    public static string ValidatePath(string path)
    {
        string full = Path.GetFullPath(path);
        string root = Path.GetFullPath(Persistence.Isolated ? Persistence.ExperimentRoot : Persistence.CampaignFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Campaign storage escapes its approved root");
        return Persistence.Within(root, full.Substring(root.Length + 1));
    }
    public static string Read(string path)
    {
        path = ValidatePath(path);
        if (new FileInfo(path).Length > MaximumBytes) throw new InvalidDataException("Campaign snapshot exceeds bounded size");
        return File.ReadAllText(path, Utf8);
    }
    public static string Hash(string text)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
    public static void Commit(string path, string text, Action<string> validate, Action<string> fault = null)
    {
        path = ValidatePath(path);
        validate(text);
        byte[] bytes = Utf8.GetBytes(text);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Campaign snapshot exceeds bounded size");
        // Never rotate an unreadable primary over the last known-good backup.
        if (File.Exists(path)) validate(Read(path));
        string backup = ValidatePath(path + ".bak");
        if (!File.Exists(path) && File.Exists(backup)) throw new IOException("Explicit backup recovery required");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string pending = ValidatePath(path + ".pending-" + Guid.NewGuid().ToString("N"));
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        fault?.Invoke("flushed");
        validate(Read(pending));
        fault?.Invoke("validated");
        if (File.Exists(path)) File.Replace(pending, path, backup);
        else File.Move(pending, path);
        // No post-promotion fault injection: a visible successful promotion is a committed generation.
    }
    public static void Recover(string path, Action<string> validate)
    {
        path = ValidatePath(path); string backup = ValidatePath(path + ".bak");
        string text = Read(backup); validate(text);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string pending = ValidatePath(path + ".recovery-" + Guid.NewGuid().ToString("N"));
        byte[] bytes = Utf8.GetBytes(text);
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        validate(Read(pending));
        if (File.Exists(path))
        {
            // Preserve the failed primary separately; never replace the good backup with corrupt bytes.
            string evidence = ValidatePath(path + ".failed-" + Guid.NewGuid().ToString("N"));
            File.Copy(path, evidence, false);
            File.Replace(pending, path, null);
        }
        else File.Move(pending, path);
    }
}
