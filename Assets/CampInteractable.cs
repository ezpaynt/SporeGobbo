using UnityEngine;

public interface ICampInteractable
{
    string GetInteractPrompt();
    void Interact(GobboController player);
}

/// <summary>
/// Optional metadata for the shared world interaction authority. ICampInteractable is the
/// historical name of the common contract used by both camp and run content.
/// </summary>
public interface IWorldInteractionMetadata
{
    bool CanInteract(GobboController player);
    Vector2 GetInteractionPoint();
    int InteractionPriority { get; }
    float InteractionRange { get; }
}

public class CampSimpleInteractable : MonoBehaviour, ICampInteractable
{
    [Header("Camp Interaction")]
    public string prompt = "Talk";
    [TextArea(2, 5)] public string[] lines;
    public bool randomLine = true;

    [Header("Voice Lines")]
    public AudioSource audioSource;
    public AudioClip[] voiceLines;
    public bool matchVoiceIndexToLine = true;

    private int nextLineIndex = 0;

    public string GetInteractPrompt()
    {
        return prompt;
    }

    public void Interact(GobboController player)
    {
        int index;
        string line = GetLine(out index);

        if (string.IsNullOrWhiteSpace(line))
            line = gameObject.name + " has nothing to say yet.";

        CampMessageUI.Show(line);
        PlayVoiceForIndex(index);
        Debug.Log("Camp interact: " + line);
    }

    string GetLine(out int chosenIndex)
    {
        chosenIndex = -1;

        if (lines == null || lines.Length == 0)
            return "";

        if (randomLine)
        {
            chosenIndex = Random.Range(0, lines.Length);
            return lines[chosenIndex];
        }

        chosenIndex = Mathf.Clamp(nextLineIndex, 0, lines.Length - 1);
        string line = lines[chosenIndex];
        nextLineIndex = (nextLineIndex + 1) % lines.Length;
        return line;
    }

    void PlayVoiceForIndex(int lineIndex)
    {
        if (voiceLines == null || voiceLines.Length == 0)
            return;

        AudioClip clip = null;

        if (matchVoiceIndexToLine && lineIndex >= 0 && lineIndex < voiceLines.Length)
            clip = voiceLines[lineIndex];
        else
            clip = voiceLines[Random.Range(0, voiceLines.Length)];

        PlayClip(clip);
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null)
            return;

        if (audioSource != null)
            audioSource.PlayOneShot(clip);
        else
            AudioSource.PlayClipAtPoint(clip, transform.position);
    }
}
