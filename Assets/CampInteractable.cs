using UnityEngine;

public interface ICampInteractable
{
    string GetInteractPrompt();
    void Interact(GobboController player);
}

public interface ICampHoldInteractable
{
    string GetHoldPrompt();
    void HoldInteract(GobboController player);
}

public class CampSimpleInteractable : MonoBehaviour, ICampInteractable, ICampHoldInteractable
{
    [Header("Camp Interaction")]
    public string prompt = "Talk";
    [TextArea(2, 5)] public string[] lines;
    public bool randomLine = true;

    [Header("Voice Lines")]
    public AudioSource audioSource;
    public AudioClip[] voiceLines;
    public bool matchVoiceIndexToLine = true;

    [Header("Hold Interaction Placeholder")]
    public bool allowHoldInteraction = false;
    public string holdPrompt = "Hold: dance later";
    [TextArea(2, 5)] public string holdMessage = "Dance placeholder. Add sprite/animation later.";
    public AudioClip holdVoiceLine;

    private int nextLineIndex = 0;

    public string GetInteractPrompt()
    {
        return prompt;
    }

    public string GetHoldPrompt()
    {
        return allowHoldInteraction ? holdPrompt : "";
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

    public void HoldInteract(GobboController player)
    {
        if (!allowHoldInteraction)
            return;

        CampMessageUI.Show(holdMessage);

        if (holdVoiceLine != null)
            PlayClip(holdVoiceLine);
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
