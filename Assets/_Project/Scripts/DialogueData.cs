using UnityEngine;

public enum DialogueSpeakerSide
{
    Left,
    Right
}

[System.Serializable]
public class DialoguePause
{
    [Tooltip("The exact words or punctuation immediately before the pause.")]
    public string afterText;
    [Min(0f)] public float seconds;
}

[System.Serializable]
public class DialogueLine
{
    public string speakerName;

    [TextArea(2, 6)]
    public string text;

    public DialogueSpeakerSide side;

    [Min(0f), Tooltip("Minimum time in seconds to hold the fully revealed line before auto-advancing.")]
    public float minimumReadHold;
    public DialoguePause[] pauses;

    // Optional now, useful later for recorded voice or generated speech clips
    public AudioClip voiceClip;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Conversation")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;
}
