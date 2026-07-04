using SRQuestDownloader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Optional zero-click mode: after the user completes one manual fetch, they can enable
/// auto-fetch-on-launch followed by launching Synth Riders with no in-app interaction.
/// </summary>
[RequireComponent(typeof(Button))]
public class ZeroClickModeToggle : MonoBehaviour
{
    public TextMeshProUGUI Label;
    public SRQDLogHandler logger;
    public DownloadManager downloadManager;

    private bool _isEnabled;
    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        RefreshAvailability();
    }

    public void SetInteractable(bool interactable)
    {
        if (_button != null)
        {
            _button.interactable = interactable;
        }
    }

    public void RefreshAvailability()
    {
        bool available = Preferences.HasCompletedInitialFetch();

        if (!available)
        {
            Preferences.SetZeroClickMode(false);
            gameObject.SetActive(false);
            return;
        }

        SetEnabled(Preferences.GetZeroClickMode(), save: false);
    }

    public void Toggle()
    {
        if (!Preferences.HasCompletedInitialFetch())
        {
            return;
        }

        var enabling = !_isEnabled;
        SetEnabled(enabling, save: true);

        if (!enabling)
        {
            downloadManager?.CancelPendingZeroClickFlow();
        }
    }

    private void SetEnabled(bool enabled, bool save)
    {
        _isEnabled = enabled;

        if (save)
        {
            Preferences.SetZeroClickMode(enabled);
            logger?.DebugLog($"Zero-click mode {(enabled ? "enabled" : "disabled")}");
        }

        if (Label == null)
        {
            return;
        }

        Label.fontStyle = enabled ? FontStyles.Underline : FontStyles.Normal;
        Label.color = enabled ? Color.white : Color.gray;
        Label.SetText(enabled ? "0-Click: ON" : "0-Click: OFF");
    }

    public static bool ShouldRunZeroClickFlow()
    {
        return Preferences.HasCompletedInitialFetch() && Preferences.GetZeroClickMode();
    }
}
