using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using com.cyborgAssets.inspectorButtonPro;
using UnityEngine;
using SRCustomLib;
using SRQuestDownloader;
using SRTimestampLib;

namespace SRQuestDownloader
{
    /// <summary>
    /// Note - needs to start disabled, to avoid double-initialization
    /// </summary>
    public class DownloadManager : MonoBehaviour
    {
        public DisplayManager displayManager;
        public SRQDLogHandler logger;
        public CustomFileManagerBehaviour customFileManager;
        public DownloadFilters downloadFilters;
        public SynthLauncher synthLauncher;
        public ZeroClickModeToggle zeroClickModeToggle;

        public bool UseZ = true;
        public bool UseSyn = true;
        public bool UseMagnet = true;

        private bool _isDownloading = false;
        private MapRepo _customMapRepo;
        private DownloadManagerZ _downloadManagerZ;
        private DownloadManagerSyn _downloadManagerSyn;
        
        [SerializeField]
        private int _zeroClickCountdownSeconds = 5;
        private bool _cancelZeroClickFlow = false;
        private bool _launchCountdownActive = false;
        private bool _zeroClickFlowInProgress = false;


        private void Awake()
        {
            _customMapRepo = new MapRepo(logger, UseZ, UseSyn, UseMagnet, customFileManager.FileManager);
            _downloadManagerZ = new DownloadManagerZ(logger, customFileManager.FileManager);
            _downloadManagerSyn = new DownloadManagerSyn(logger, customFileManager.FileManager);
        }

        private async void OnEnable()
        {
            if (_zeroClickFlowInProgress)
            {
                logger.DebugLog("Zero-click flow already running; skipping duplicate OnEnable.");
                return;
            }

            var zeroClickRequested = ZeroClickModeToggle.ShouldRunZeroClickFlow();
            displayManager.DisableActions(zeroClickRequested ? "Checking for new customs..." : "Initializing maps source...");

            await _customMapRepo.Initialize();

            zeroClickModeToggle?.RefreshAvailability();

            if (ZeroClickModeToggle.ShouldRunZeroClickFlow())
            {
                _zeroClickFlowInProgress = true;
                try
                {
                    await RunZeroClickFlowAsync();
                }
                finally
                {
                    _zeroClickFlowInProgress = false;
                }
                return;
            }

            displayManager.EnableActions();
        }

        public void StartDownloading() => _ = StartDownloadingAsync();

        public async Task<bool> StartDownloadingAsync(bool useLastFetchCutoffOnly = false, bool suppressEnableActions = false)
        {
            if (_isDownloading)
            {
                logger.DebugLog("Already downloading!");
                return false;
            }

            _isDownloading = true;

            if (!suppressEnableActions)
            {
                displayManager.DisableActions("Downloading...");
            }
            else
            {
                displayManager.DisableActions("Checking for new customs...");
            }

            var success = false;

            try
            {
                var nowUtc = DateTime.UtcNow;
                var cutoffTimeUtc = useLastFetchCutoffOnly
                    ? Preferences.GetLastDownloadedTime()
                    : downloadFilters.GetDateCutoffFromCurrentSelection(nowUtc);
                logger.DebugLog($"Using cutoff time (local) {cutoffTimeUtc.ToLocalTime()}");

                var difficultySelections = downloadFilters.GetDifficultiesEnabled();
                logger.DebugLog("Using difficulties " + String.Join(",", difficultySelections));

                var result = await _customMapRepo.TryDownloadWithFallbacks(cutoffTimeUtc, difficultySelections, Application.exitCancellationToken);
                success = result.Success;
                if (success)
                {
                    Preferences.SetLastDownloadedTime(nowUtc);
                    Preferences.MarkInitialFetchCompleted();
                    customFileManager.SetLastDownloadedTime(nowUtc);
                    displayManager.UpdateLastFetchTime();
                    zeroClickModeToggle?.RefreshAvailability();

                    if (suppressEnableActions)
                    {
                        displayManager.ShowZeroClickFetchResult(result.NewMapsFound);
                    }
                }
            }
            catch (Exception e)
            {
                logger.ErrorLog("Failed to download: " + e.Message);
            }

            logger.DebugLog("Finished downloading");

            _isDownloading = false;

            if (!suppressEnableActions)
            {
                displayManager.EnableActions();
            }

            return success;
        }

        public void CancelPendingZeroClickFlow()
        {
            _cancelZeroClickFlow = true;
            logger.DebugLog("Zero-click auto-launch cancelled by user.");
        }

        public void OnLaunchSynthRidersButtonClicked()
        {
            if (_launchCountdownActive)
            {
                CancelPendingZeroClickFlow();
                _launchCountdownActive = false;
                displayManager.ResetLaunchSynthRidersButton();
                displayManager.EnableActions();
                return;
            }

            synthLauncher?.LaunchSynthRiders();
        }

        private async Task RunZeroClickFlowAsync()
        {
            logger.DebugLog("Zero-click phase 1/3: fetching new customs since last fetch...");

            var fetchSucceeded = await RunZeroClickFetchAsync();
            if (!ZeroClickModeToggle.ShouldRunZeroClickFlow())
            {
                logger.DebugLog("Zero-click mode disabled during fetch; staying in app.");
                displayManager.EnableActions();
                return;
            }

            if (!fetchSucceeded)
            {
                logger.ErrorLog("Zero-click mode: fetch failed; staying in app for manual retry.");
                displayManager.EnableActions();
                return;
            }

            logger.DebugLog("Zero-click phase 2/3: fetch complete; starting launch countdown.");
            if (!await WaitForLaunchCountdownAsync())
            {
                logger.DebugLog("Zero-click auto-launch cancelled; staying in app.");
                displayManager.EnableActions();
                return;
            }

            logger.DebugLog("Zero-click phase 3/3: launching Synth Riders...");
            if (synthLauncher != null)
            {
                synthLauncher.LaunchSynthRiders();
            }
            else
            {
                logger.ErrorLog("Zero-click mode: SynthLauncher reference missing!");
                displayManager.EnableActions();
            }
        }

        private async Task<bool> RunZeroClickFetchAsync()
        {
            while (_isDownloading)
            {
                await Task.Delay(100, Application.exitCancellationToken);
            }

            displayManager.ShowZeroClickFetchInProgress();
            return await StartDownloadingAsync(useLastFetchCutoffOnly: true, suppressEnableActions: true);
        }

        private async Task<bool> WaitForLaunchCountdownAsync()
        {
            _cancelZeroClickFlow = false;
            _launchCountdownActive = true;
            zeroClickModeToggle?.SetInteractable(true);

            try
            {
                for (int secondsRemaining = _zeroClickCountdownSeconds; secondsRemaining >= 1; secondsRemaining--)
                {
                    if (_cancelZeroClickFlow || !ZeroClickModeToggle.ShouldRunZeroClickFlow())
                    {
                        return false;
                    }

                    displayManager.ShowLaunchCountdown(secondsRemaining);
                    await Task.Delay(1000, Application.exitCancellationToken);
                }

                return !_cancelZeroClickFlow && ZeroClickModeToggle.ShouldRunZeroClickFlow();
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _launchCountdownActive = false;
                zeroClickModeToggle?.SetInteractable(true);

                if (_cancelZeroClickFlow || !ZeroClickModeToggle.ShouldRunZeroClickFlow())
                {
                    displayManager.ResetLaunchSynthRidersButton();
                }
            }
        }

        /// <summary>
        /// For testing, change some map timestamps to be incorrect
        /// </summary>
        [ProPlayButton]
        public async void TestBreakMapTimestamps()
        {
            logger.DebugLog("Breaking map timestamp...");
            
            displayManager.DisableActions("Breaking Timestamps...");
            
            // First, try to fix timestamps with local file
            logger.DebugLog("  Trying local fixes...");
            await customFileManager.ApplyLocalTimestampMappings("test_incorrect_timestamp_mappings");

            logger.DebugLog("  Refreshing SynthDB timestamps...");
            await UpdateSynthDBTimestamps();

            logger.DebugLog("Done");
            displayManager.EnableActions();
        }

        [ProPlayButton]
        public void FixMapTimestamps() => _ = FixMapTimestampsAsync();
        
        /// Update local map timestamps to match the Z site published_at,
        /// to allow for correct sorting by timestamp in-game
        public async Task FixMapTimestampsAsync()
        {
            logger.DebugLog("Fixing map timestamp...");

            displayManager.DisableActions("Fixing Timestamps...");
            
            // First, try to fix timestamps with local file
            logger.DebugLog("  Trying local fixes...");
            await customFileManager.ApplyLocalTimestampMappings();

            // Use Z/Syn for any others
            List<string> difficultySelections = downloadFilters.GetDifficultiesEnabled();
            logger.DebugLog("  Getting online fixes...");
            bool success = UseZ && await _downloadManagerZ.ApplyTimestampFixes(difficultySelections, Application.exitCancellationToken);
            if (!success)
            {
                logger.DebugLog("  Not getting fixes from Z. Trying synplicity...");
                success = UseSyn && await _downloadManagerSyn.ApplyTimestampFixes(difficultySelections, Application.exitCancellationToken);
                if (!success)
                {
                    logger.ErrorLog("Failed to download timestamp fixes!");
                }
            }
            
            // Finally, update SynthDB so the next load is correct and doesn't need a slow reload of customs
            if (success)
            {
                logger.DebugLog("  Refreshing SynthDB timestamps...");
                await UpdateSynthDBTimestamps();
            }

            logger.DebugLog("Done");
            displayManager.EnableActions();
        }

        /// <summary>
        /// Updates SynthDB with current file timestamps
        /// </summary>
        [ProPlayButton]
        private async Task UpdateSynthDBTimestamps() => await customFileManager.UpdateSynthDBTimestamps();
    }
}
