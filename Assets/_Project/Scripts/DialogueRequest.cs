using System;

public sealed class DialogueRequest
{
    public DialogueRequest(DialogueData dialogue, bool lockPlayerControls = false, Action onFinished = null)
    {
        Dialogue = dialogue;
        LockPlayerControls = lockPlayerControls;
        OnFinished = onFinished;
    }

    public DialogueData Dialogue { get; }
    public bool LockPlayerControls { get; }
    public Action OnFinished { get; }
}
