# Temporary story debugging — remove before release

These controls are for Unity Editor Play Mode only. They are excluded from all player builds, including Development Builds. They change runtime state only; exit Play Mode to reset. Do not save debug state as story progress.

## Controls

- **F7**: finish the current conversation (all remaining lines). Uses normal completion callbacks and releases dialogue-owned controls/audio. Repeat for separate conversations. A scripted input lock or open settings blocks this action; wait for the animation or close settings.
- **F8**: unlock all active interactions belonging to the current story, including shared doors.
- **Tools > The Visitor > Debug**: choose a specific unlock: window, puzzle, door, or family minigame. Use the menu if the keyboard layout or laptop intercepts F7/F8.
- Existing **backtick (`)** shortcuts: complete the open window minigame, complete the open family minigame, or finish the current phone interruption. These now run only in the Editor. Inspect the corresponding component for its serialized key override.

To test Arthur's door: finish the opening conversations with F7, wait for the intro transition, then choose **Unlock door (bypass puzzle)** or press F8. Walk through the door normally. The puzzle remains unsolved, and the door's normal dialogue/progression runs when triggered.

Unlock commands refuse to run during dialogue, the intro, settings, an open minigame, or a view transition. Close/finish the current section first. Unlocking does not complete tasks, replay consumed triggers, reset one-shot interactions, teleport the player, activate disabled objects, or change the active story. A later authored sequence may apply its own gates again; these commands are for testing interactions, not for validating the complete progression path.

Demo Burzon currently runs Father: Arthur components stay blocked and inactive. Use the original Game scene in Play Mode to test Arthur; no changes to that scene are required. The father fridge has no progression gate to bypass yet.

## Removal checklist before finishing the game

- [ ] Delete `Assets/_Project/Editor/StoryDebugTools.cs` and its `.meta`.
- [ ] Remove the marked `UNITY_EDITOR // DEBUG-ONLY` block containing `DebugFinishConversation` in `DialogueUI.cs`.
- [ ] Remove the marked block containing `DebugIsIntroRunning` in `OpeningGhostIntroController.cs`.
- [ ] Remove the marked keyboard skip blocks in `WindowSequenceController.cs`, `FamilyMinigamePlaceholderController.cs`, and `PuzzleEventDirector.cs`.
- [ ] Remove their unused debug fields: `enableSkipShortcut`, `skipSequenceKey`, `skipKey`, `completePhoneEventKey`; remove `WindowSequenceController.SkipSequenceForTesting`. Clean stale serialized fields through normal Unity serialization as needed.
- [ ] Keep real completion paths: `CompleteCurrentPhoneEvent`, `CompleteSequence`, dialogue callbacks and puzzle/door progression are also used by actual gameplay.
- [ ] Search for `DEBUG-ONLY`, `DebugFinishConversation`, `DebugIsIntroRunning`, `SkipSequenceForTesting`, and `KeyCode.BackQuote` to find leftovers.
- [ ] Test the normal full story progression and verify F7/F8/backtick do not bypass anything in the release build.

No scene objects or camera positions are changed by installing these tools. This technical checklist is tracked in Git so the team can see it; team decisions remain in the ignored local notes.
