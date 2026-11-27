using System;

public sealed class DialogueRequest
{
    public DialogueRequest(DialogueData dialogue, bool lockPlayerControls = false, Action onFinished = null,
        bool autoAdvance = false, bool allowManualAdvance = false)
    {
        Dialogue = dialogue;
        LockPlayerControls = lockPlayerControls;
        OnFinished = onFinished;
        AutoAdvance = autoAdvance;
        AllowManualAdvance = allowManualAdvance;
    }

    public DialogueData Dialogue { get; }
    public bool LockPlayerControls { get; }
    public Action OnFinished { get; }
    public bool AutoAdvance { get; }
    public bool AllowManualAdvance { get; }
}
