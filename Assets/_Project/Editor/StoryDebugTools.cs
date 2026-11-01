#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Temporary Play Mode tools. Entire file is excluded from players. Removal checklist: docs/debug-skips.md.</summary>
public static class StoryDebugTools
{
    private const string Menu = "Tools/The Visitor/Debug/";
    public enum Section { All, Window, Puzzle, Door, Family }

    [MenuItem(Menu + "Finish current conversation _F7")]
    public static void FinishConversation()
    {
        bool finished = Application.isPlaying && DialogueUI.Instance != null && DialogueUI.Instance.DebugFinishConversation();
        Report(finished ? "Conversation finished; normal completion callbacks ran." :
            "No skippable conversation. Close settings or wait for the scripted input lock to end.");
    }

    [MenuItem(Menu + "Unlock all story interactions _F8")]
    private static void UnlockAll() => Unlock(Section.All);
    [MenuItem(Menu + "Unlock window")]
    private static void UnlockWindow() => Unlock(Section.Window);
    [MenuItem(Menu + "Unlock puzzle")]
    private static void UnlockPuzzle() => Unlock(Section.Puzzle);
    [MenuItem(Menu + "Unlock door (bypass puzzle)")]
    private static void UnlockDoor() => Unlock(Section.Door);
    [MenuItem(Menu + "Unlock family minigame")]
    private static void UnlockFamily() => Unlock(Section.Family);

    [MenuItem(Menu + "Finish current conversation _F7", true)]
    [MenuItem(Menu + "Unlock all story interactions _F8", true)]
    [MenuItem(Menu + "Unlock window", true)]
    [MenuItem(Menu + "Unlock puzzle", true)]
    [MenuItem(Menu + "Unlock door (bypass puzzle)", true)]
    [MenuItem(Menu + "Unlock family minigame", true)]
    private static bool Validate() => EditorApplication.isPlaying && !EditorApplication.isPaused;

    /// <summary>Unlocks active matching-story components only. Does not enable objects or pretend tasks were solved.</summary>
    public static int Unlock(Section section)
    {
        if (!Application.isPlaying || AudioSettingsOverlay.IsOpen ||
            (DialogueUI.Instance != null && DialogueUI.Instance.IsDialogueOpen))
        { Report("Close settings and finish the conversation (F7) before unlocking."); return 0; }
        var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>().Where(MatchesStory).ToArray();
        if (behaviours.Any(IsBusy))
        { Report("Finish the intro or close the current minigame/transition before unlocking. F7 skips conversations."); return 0; }
        int count = 0;
        foreach (var b in behaviours)
        {
            if ((section == Section.All || section == Section.Window) && b is WindowSequenceController window)
            { window.SetInteractionEnabled(true); count++; }
            if ((section == Section.All || section == Section.Puzzle) && b is PuzzleOpener puzzle)
            { puzzle.SetInteractionEnabled(true); count++; }
            if (section == Section.All || section == Section.Door)
            {
                if (b is DoorTriggerRotator door) { door.SetInteractionEnabled(true); count++; }
                if (b is DoorDialogueTrigger dialogue) { dialogue.SetInteractionEnabled(true); count++; }
            }
            if ((section == Section.All || section == Section.Family) && b is FamilyMinigamePlaceholderController family)
            { family.SetInteractionEnabled(true); count++; }
        }
        Report(count > 0 ? $"Unlocked {section}: {count} matching components. Walk to the interaction normally. Nothing marked solved." :
            "No active matching-story interactions found. Inactive objects stay inactive.");
        return count;
    }

    private static bool MatchesStory(MonoBehaviour b)
    {
        if (b == null || !b.isActiveAndEnabled || !b.gameObject.scene.IsValid()) return false;
        var owner = new SerializedObject(b).FindProperty("story");
        return owner != null && StoryAccess.Allows(b, (StoryId)owner.intValue);
    }

    private static bool IsBusy(MonoBehaviour b)
    {
        if (b is OpeningGhostIntroController intro && intro.DebugIsIntroRunning) return true;
        if (!(b is WindowSequenceController) && !(b is PuzzleOpener) && !(b is FamilyMinigamePlaceholderController)) return false;
        var so = new SerializedObject(b);
        return new[] { "isTransitioning", "sequenceOpen", "puzzleOpen" }.Any(name => so.FindProperty(name)?.boolValue == true);
    }

    private static void Report(string message)
    {
        Debug.Log("[Story debug] " + message);
        if (Application.isPlaying && DialogueUI.Instance != null)
            DialogueUI.Instance.ShowTemporaryCenterMessage("DEBUG: " + message, 4f);
    }
}
#endif
