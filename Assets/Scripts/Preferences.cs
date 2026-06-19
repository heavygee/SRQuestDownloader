using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Preferences {

    private static string KEY_LAST_DOWNLOADED_TIME_SEC = "lastDownloadedTimeSec";
    public static int DEFAULT_LAST_DOWNLOADED_TIME_SEC = 0;
    private static string KEY_SELECTED_DIFFICULTIES = "selectedDifficulties";
    private static string KEY_HAS_COMPLETED_INITIAL_FETCH = "hasCompletedInitialFetch";
    private static string KEY_ZERO_CLICK_MODE = "zeroClickMode";

    public static void SetLastDownloadedTime(DateTime lastDownloadedTime) {
        DateTimeOffset dto = lastDownloadedTime;
        PlayerPrefs.SetInt(KEY_LAST_DOWNLOADED_TIME_SEC, (int)dto.ToUnixTimeSeconds());
    }

    public static DateTime GetLastDownloadedTime() {
        int timeSec = PlayerPrefs.GetInt(KEY_LAST_DOWNLOADED_TIME_SEC, DEFAULT_LAST_DOWNLOADED_TIME_SEC);
        return DateTimeOffset.FromUnixTimeSeconds(timeSec).UtcDateTime;
    }

    public static void SetDifficultiesEnabled(List<string> enabledDifficulties) {
        // Save as sorted list csv
        enabledDifficulties.Sort();
        var csv = String.Join(",", enabledDifficulties);
        PlayerPrefs.SetString(KEY_SELECTED_DIFFICULTIES, csv);
    }

    public static HashSet<string> GetDifficultiesEnabled() {
        // Default to all difficulties
        var csvDifficulties = PlayerPrefs.GetString(KEY_SELECTED_DIFFICULTIES, "Easy,Normal,Hard,Expert,Master,Custom");
        return csvDifficulties.Split(",", StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    /// <summary>
    /// True after the user completes at least one successful fetch. Unlocks zero-click mode.
    /// Existing installs with a prior fetch timestamp also qualify.
    /// </summary>
    public static bool HasCompletedInitialFetch() {
        if (PlayerPrefs.GetInt(KEY_HAS_COMPLETED_INITIAL_FETCH, 0) == 1) {
            return true;
        }

        return GetLastDownloadedTime() > DateTime.UnixEpoch;
    }

    public static void MarkInitialFetchCompleted() {
        PlayerPrefs.SetInt(KEY_HAS_COMPLETED_INITIAL_FETCH, 1);
        PlayerPrefs.Save();
    }

    public static bool GetZeroClickMode() {
        return HasCompletedInitialFetch() && PlayerPrefs.GetInt(KEY_ZERO_CLICK_MODE, 0) == 1;
    }

    public static void SetZeroClickMode(bool enabled) {
        PlayerPrefs.SetInt(KEY_ZERO_CLICK_MODE, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}
