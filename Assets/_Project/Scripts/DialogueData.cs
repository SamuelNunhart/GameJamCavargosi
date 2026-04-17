using UnityEngine;

public enum DialogueSpeakerSide
{
    Left,
    Right
}

[System.Serializable]
public class DialogueLine
{
    public string speakerName;

    [TextArea(2, 6)]
    public string text;

    public DialogueSpeakerSide side;

    // Optional now, useful later for recorded voice or generated speech clips
    public AudioClip voiceClip;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Conversation")]
public class DialogueData : ScriptableObject
{
    public DialogueLine[] lines;
}