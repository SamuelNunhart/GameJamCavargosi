using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stores chapter completion across game launches independently of settings.</summary>
public static class StoryChapterProgress
{
    public static IReadOnlyList<StoryId> Chapters { get; } = Array.AsReadOnly(new[]
    {
        StoryId.Arthur, StoryId.Father, StoryId.Mother, StoryId.Epilogue
    });

    public static bool IsCompleted(StoryId chapter) => PlayerPrefs.GetInt(CompletedKey(chapter), 0) == 1;

    public static void MarkCompleted(StoryId chapter)
    {
        PlayerPrefs.SetInt(CompletedKey(chapter), 1);
        PlayerPrefs.Save();
    }

    public static void Reset(StoryId chapter)
    {
        PlayerPrefs.DeleteKey(CompletedKey(chapter));
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        foreach (StoryId chapter in Chapters) PlayerPrefs.DeleteKey(CompletedKey(chapter));
        PlayerPrefs.Save();
    }

    private static string CompletedKey(StoryId chapter) => $"StoryProgress.{chapter}.Completed";
}
