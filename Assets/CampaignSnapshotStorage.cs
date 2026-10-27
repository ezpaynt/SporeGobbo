using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using SporeGobbo.Safety;
using Unity.Profiling;

/// <summary>File mechanics for SporeSaveManager. No campaign selection, roster or lifecycle ownership.</summary>
public static class CampaignSnapshotStorage
{
    const int MaximumBytes = 16 * 1024 * 1024;
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    public static bool TimingEnabled { get; set; }
    public static void BeginTimingCapture() { timingCount=0; droppedTimings=0; TimingEnabled=true; }
    const int TimingCapacity = 4096;
    static readonly Timing[] timings = new Timing[TimingCapacity];
    static int timingCount, droppedTimings;
    static long nextRequest;
    [ThreadStatic] static long request;
    enum Operation { Read, Hash, Validate, Write, Flush, Replace, Recovery, Commit }
    struct Timing { public long request, ticks, bytes; public int frame; public Operation operation; }
    static readonly ProfilerMarker readProfile = new ProfilerMarker("Spore.V1.StorageRead");
    static readonly ProfilerMarker hashProfile = new ProfilerMarker("Spore.V1.StorageHash");
    static readonly ProfilerMarker validateProfile = new ProfilerMarker("Spore.V1.StorageValidate");
    static readonly ProfilerMarker writeProfile = new ProfilerMarker("Spore.V1.StorageWrite");
    static readonly ProfilerMarker flushProfile = new ProfilerMarker("Spore.V1.StorageFlush");
    static readonly ProfilerMarker replaceProfile = new ProfilerMarker("Spore.V1.StorageReplace");
    static readonly ProfilerMarker recoveryProfile = new ProfilerMarker("Spore.V1.StorageRecovery");
    static readonly ProfilerMarker commitProfile = new ProfilerMarker("Spore.V1.StorageCommit");
    // Timing-only scopes are value types. No callback/list allocation is added to a save operation.
    readonly struct Measurement : IDisposable
    {
        readonly ProfilerMarker.AutoScope profile;
        readonly long start, id, bytes;
        readonly Operation operation;
        public Measurement(ProfilerMarker marker, Operation operation, long bytes = -1)
        { profile=marker.Auto(); this.operation=operation; this.bytes=bytes; start=TimingEnabled?System.Diagnostics.Stopwatch.GetTimestamp():0; id=request; }
        public void Dispose()
        {
            profile.Dispose(); if(start==0)return;
            int slot=System.Threading.Interlocked.Increment(ref timingCount)-1;
            if(slot>=TimingCapacity){System.Threading.Interlocked.Increment(ref droppedTimings);return;}
            timings[slot]=new Timing{request=id,operation=operation,bytes=bytes,frame=UnityEngine.Time.frameCount,ticks=System.Diagnostics.Stopwatch.GetTimestamp()-start};
        }
    }
    readonly struct Request : IDisposable
    {
        readonly long prior;
        public Request(bool begin){prior=request;if(begin)request=System.Threading.Interlocked.Increment(ref nextRequest);}
        public void Dispose(){request=prior;}
    }
    /// <summary>Evidence export only; phases are inclusive attempted scopes, not success acknowledgements.</summary>
    public static void ExportTimings(TextWriter file)
    {
        file.WriteLine("request,frame,operation,bytes,inclusiveNanoseconds,completedScope,outcome");
        int count=Math.Min(timingCount,TimingCapacity);
        for(int i=0;i<count;i++){var t=timings[i];long ns=(long)(t.ticks*(1000000000.0/System.Diagnostics.Stopwatch.Frequency));file.WriteLine(t.request+","+t.frame+","+t.operation+","+t.bytes+","+ns+",1,attempted-scope-not-commit-acknowledgement");}
        file.WriteLine("-1,-1,diagnosticOverflow,"+droppedTimings+",-1,0,unavailable-if-nonzero");
    }

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
        long length = new FileInfo(path).Length;
        if (length > MaximumBytes) throw new InvalidDataException("Campaign snapshot exceeds bounded size");
        using var measurement = new Measurement(readProfile, Operation.Read, length);
        return File.ReadAllText(path, Utf8);
    }
    public static string Hash(string text)
    {
        using var measurement = new Measurement(hashProfile, Operation.Hash);
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
    }
    public static void Commit(string path, string text, Action<string> validate, Action<string> fault = null)
    {
        using var correlation = new Request(true);
        using var commitMeasurement = new Measurement(commitProfile, Operation.Commit);
        path = ValidatePath(path);
        using (new Measurement(validateProfile, Operation.Validate)) validate(text);
        byte[] bytes = Utf8.GetBytes(text);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Campaign snapshot exceeds bounded size");
        // Never rotate an unreadable primary over the last known-good backup.
        if (File.Exists(path)) using (new Measurement(validateProfile, Operation.Validate)) validate(Read(path));
        string backup = ValidatePath(path + ".bak");
        if (!File.Exists(path) && File.Exists(backup)) throw new IOException("Explicit backup recovery required");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string pending = ValidatePath(path + ".pending-" + Guid.NewGuid().ToString("N"));
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            using (new Measurement(writeProfile, Operation.Write, bytes.Length)) stream.Write(bytes, 0, bytes.Length);
            using (new Measurement(flushProfile, Operation.Flush, bytes.Length)) stream.Flush(true);
        }
        fault?.Invoke("flushed");
        using (new Measurement(validateProfile, Operation.Validate, bytes.Length)) validate(Read(pending));
        fault?.Invoke("validated");
        using (new Measurement(replaceProfile, Operation.Replace, bytes.Length))
        {
            if (File.Exists(path)) File.Replace(pending, path, backup);
            else File.Move(pending, path);
        }
        // No post-promotion fault injection: a visible successful promotion is a committed generation.
    }
    public static void Recover(string path, Action<string> validate)
    {
        using var correlation = new Request(true);
        using var recoveryMeasurement = new Measurement(recoveryProfile, Operation.Recovery);
        path = ValidatePath(path); string backup = ValidatePath(path + ".bak");
        string text = Read(backup); using (new Measurement(validateProfile, Operation.Validate)) validate(text);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string pending = ValidatePath(path + ".recovery-" + Guid.NewGuid().ToString("N"));
        byte[] bytes = Utf8.GetBytes(text);
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            using (new Measurement(writeProfile, Operation.Write, bytes.Length)) stream.Write(bytes, 0, bytes.Length);
            using (new Measurement(flushProfile, Operation.Flush, bytes.Length)) stream.Flush(true);
        }
        using (new Measurement(validateProfile, Operation.Validate, bytes.Length)) validate(Read(pending));
        if (File.Exists(path))
        {
            // Preserve the failed primary separately; never replace the good backup with corrupt bytes.
            string evidence = ValidatePath(path + ".failed-" + Guid.NewGuid().ToString("N"));
            File.Copy(path, evidence, false);
            using (new Measurement(replaceProfile, Operation.Replace, bytes.Length)) File.Replace(pending, path, null);
        }
        else using (new Measurement(replaceProfile, Operation.Replace, bytes.Length)) File.Move(pending, path);
    }
}
