using System;
using System.Collections.Generic;

[Serializable]
public sealed class StoryEventRecord
{
    public string eventId = "";
    public int unlockSequence = 0;
    public int runNumber = 0;
    public int campCycle = 0;

    public StoryEventRecord Clone()
    {
        return new StoryEventRecord
        {
            eventId = eventId,
            unlockSequence = unlockSequence,
            runNumber = runNumber,
            campCycle = campCycle
        };
    }
}

[Serializable]
public sealed class StoryProgressData
{
    public int nextUnlockSequence = 1;
    public List<StoryEventRecord> completedEvents = new List<StoryEventRecord>();

    public void Normalize()
    {
        completedEvents ??= new List<StoryEventRecord>();
        completedEvents.RemoveAll(record => record == null || string.IsNullOrWhiteSpace(record.eventId));

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        int highestSequence = 0;
        for (int i = completedEvents.Count - 1; i >= 0; i--)
        {
            StoryEventRecord record = completedEvents[i];
            record.eventId = record.eventId.Trim();
            if (!seen.Add(record.eventId))
            {
                completedEvents.RemoveAt(i);
                continue;
            }

            record.unlockSequence = Math.Max(1, record.unlockSequence);
            record.runNumber = Math.Max(0, record.runNumber);
            record.campCycle = Math.Max(0, record.campCycle);
            highestSequence = Math.Max(highestSequence, record.unlockSequence);
        }

        completedEvents.Sort((a, b) => a.unlockSequence.CompareTo(b.unlockSequence));
        nextUnlockSequence = Math.Max(Math.Max(1, nextUnlockSequence), highestSequence + 1);
    }

    public bool Contains(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return false;
        Normalize();
        string target = eventId.Trim();
        foreach (StoryEventRecord record in completedEvents)
            if (string.Equals(record.eventId, target, StringComparison.Ordinal)) return true;
        return false;
    }

    public StoryEventRecord Find(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return null;
        Normalize();
        string target = eventId.Trim();
        foreach (StoryEventRecord record in completedEvents)
            if (string.Equals(record.eventId, target, StringComparison.Ordinal)) return record;
        return null;
    }

    public StoryProgressData Clone()
    {
        Normalize();
        StoryProgressData copy = new StoryProgressData { nextUnlockSequence = nextUnlockSequence };
        foreach (StoryEventRecord record in completedEvents) copy.completedEvents.Add(record.Clone());
        return copy;
    }
}
