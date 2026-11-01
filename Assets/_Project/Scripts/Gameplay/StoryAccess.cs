using UnityEngine;

public enum StoryId { All = 0, Arthur = 1, Father = 2, Mother = 3, Epilogue = 4 }

/// <summary>Story ownership is separate from the character speaking a line. Scenes without a rig retain Arthur's original story.</summary>
public static class StoryAccess
{
    public static StoryId Current(Component source)
    {
        StoryPlayerRig rig = StoryPlayerRig.Instance;
        return rig != null && rig.gameObject.scene == source.gameObject.scene ? rig.Story : StoryId.Arthur;
    }

    public static bool Allows(Component source, StoryId owner)
    {
        return owner == StoryId.All || owner == Current(source);
    }
}
