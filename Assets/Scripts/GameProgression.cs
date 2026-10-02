using UnityEngine;

public static class GameProgression
{
    private const string HighestUnlockedKey = "Progress.HighestUnlocked";

    public static int HighestUnlocked => Mathf.Clamp(PlayerPrefs.GetInt(HighestUnlockedKey, 1), 1, 3);

    public static bool IsUnlocked(int levelNumber) => levelNumber >= 1 && levelNumber <= HighestUnlocked;
    public static bool IsCompleted(int levelNumber) => PlayerPrefs.GetInt($"Progress.Level{levelNumber}.Completed", 0) == 1;

    public static float GetBestTime(int levelNumber) => PlayerPrefs.GetFloat($"Progress.Level{levelNumber}.BestTime", -1f);

    public static void CompleteLevel(int levelNumber, float elapsedSeconds)
    {
        if (levelNumber < 1 || levelNumber > 3) return;

        PlayerPrefs.SetInt($"Progress.Level{levelNumber}.Completed", 1);
        float previous = GetBestTime(levelNumber);
        if (previous < 0f || elapsedSeconds < previous)
            PlayerPrefs.SetFloat($"Progress.Level{levelNumber}.BestTime", elapsedSeconds);

        PlayerPrefs.SetInt(HighestUnlockedKey, Mathf.Max(HighestUnlocked, Mathf.Min(3, levelNumber + 1)));
        PlayerPrefs.Save();
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}.{Mathf.FloorToInt((seconds - Mathf.Floor(seconds)) * 10f):0}";
    }
}
