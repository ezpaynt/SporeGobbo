using System.Collections.Generic;

public sealed class JournalSnapshot
{
    public List<JournalThreadSnapshot> threads = new List<JournalThreadSnapshot>();
}

public sealed class JournalThreadSnapshot
{
    public string threadId = "";
    public string displayName = "";
    public int sortOrder = 0;
    public List<JournalEntrySnapshot> entries = new List<JournalEntrySnapshot>();
}

public sealed class JournalEntrySnapshot
{
    public string entryId = "";
    public string bodyText = "";
    public int unlockSequence = 0;
    public int authoredOrder = 0;
}

public static class JournalSnapshotBuilder
{
    public static JournalSnapshot Build(JournalContentLibrary library, GameState state)
    {
        JournalSnapshot snapshot = new JournalSnapshot();
        if (library == null || state == null || state.storyProgress == null || library.threads == null) return snapshot;
        state.storyProgress.Normalize();

        foreach (JournalThreadDefinition definition in library.threads)
        {
            if (definition == null || definition.entries == null) continue;
            JournalThreadSnapshot thread = new JournalThreadSnapshot
            {
                threadId = definition.threadId,
                displayName = definition.displayName,
                sortOrder = definition.sortOrder
            };

            for (int i = 0; i < definition.entries.Count; i++)
            {
                JournalEntryDefinition entry = definition.entries[i];
                if (entry == null) continue;
                StoryEventRecord record = state.storyProgress.Find(entry.requiredStoryEventId);
                if (record == null) continue;
                thread.entries.Add(new JournalEntrySnapshot
                {
                    entryId = entry.entryId,
                    bodyText = entry.bodyText,
                    unlockSequence = record.unlockSequence,
                    authoredOrder = i
                });
            }

            if (thread.entries.Count == 0) continue;
            thread.entries.Sort((a, b) =>
            {
                int sequence = a.unlockSequence.CompareTo(b.unlockSequence);
                return sequence != 0 ? sequence : a.authoredOrder.CompareTo(b.authoredOrder);
            });
            snapshot.threads.Add(thread);
        }

        snapshot.threads.Sort((a, b) =>
        {
            int order = a.sortOrder.CompareTo(b.sortOrder);
            return order != 0 ? order : string.CompareOrdinal(a.threadId, b.threadId);
        });
        return snapshot;
    }
}
