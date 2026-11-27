using UnityEngine;

/// <summary>Selects the player and interactions when entering a chapter scene.</summary>
[DefaultExecutionOrder(-300)]
internal sealed class ChapterSceneSetup : MonoBehaviour
{
    private static StoryId nextStory = StoryId.Arthur;

    [SerializeField] private bool previewFatherOnPlay;
    [SerializeField] private GameObject arthurPlayer;
    [SerializeField] private StoryPlayerRig fatherPlayer;
    [SerializeField] private DialogueUI dialogue;
    [SerializeField] private PlayerControlLocker fatherControlLocker;
    [SerializeField] private GameObject[] arthurOnlyObjects;
    [SerializeField] private Behaviour[] arthurOnlyBehaviours;
    [SerializeField] private GameObject[] fatherOnlyObjects;

    internal static void SelectNextStory(StoryId story) => nextStory = story;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSelection() => nextStory = StoryId.Arthur;

    private void Awake()
    {
        bool playingFather = previewFatherOnPlay || nextStory == StoryId.Father;
        nextStory = StoryId.Arthur;
        if (!playingFather) return;

        arthurPlayer.SetActive(false);
        foreach (GameObject target in arthurOnlyObjects) target.SetActive(false);
        foreach (Behaviour target in arthurOnlyBehaviours) target.enabled = false;
        dialogue.UsePlayerControlLocker(fatherControlLocker);
        fatherPlayer.gameObject.SetActive(true);
        foreach (GameObject target in fatherOnlyObjects) target.SetActive(true);
    }
}
