using System.IO;
using System.Windows;
using ReplayOverlay.Host.Models;
using ReplayOverlay.Host.Services;

namespace ReplayOverlay.Host.Views;

public partial class SetupWizardWindow : Window
{
    private const int PageCount = 5;

    /// <summary>
    /// The resulting config after wizard completion. Null if cancelled.
    /// </summary>
    public AppConfig? Result { get; private set; }

    public SetupWizardWindow()
    {
        InitializeComponent();

        // Try to auto-detect recording folder, fall back to Videos
        var obsDir = HotkeySyncService.ReadObsRecordDirectory();
        WizFolderBox.Text = obsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

        UpdateNavigation();
    }

    private int CurrentPage => WizardPages.SelectedIndex;

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage < PageCount - 1)
        {
            WizardPages.SelectedIndex++;
            UpdateNavigation();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentPage > 0)
        {
            WizardPages.SelectedIndex--;
            UpdateNavigation();
        }
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        var config = new AppConfig
        {
            ObsPort = ParseInt(WizPortBox.Text, 4455, 1, 65535),
            ObsPassword = WizPasswordBox.Text,
            AutoLaunchObs = WizAutoLaunchCb.IsChecked == true,
            AutoStartBuffer = WizAutoBufferCb.IsChecked == true,
            ToggleHotkey = HotkeyService.ParseHotkeyDisplay(WizToggleBox.Text.Trim()),
            SaveHotkey = HotkeyService.ParseHotkeyDisplay(WizSaveBox.Text.Trim()),
            WatchFolder = WizFolderBox.Text.Trim(),
            OrganizeByGame = WizOrganizeCb.IsChecked == true,
            SyncObsFolder = WizSyncFolderCb.IsChecked == true
        };

        Result = config;
        DialogResult = true;
        Close();
    }

    private void UpdateNavigation()
    {
        bool isFirst = CurrentPage == 0;
        bool isLast = CurrentPage == PageCount - 1;

        BackButton.Visibility = isFirst ? Visibility.Collapsed : Visibility.Visible;
        NextButton.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;
        FinishButton.Visibility = isLast ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WizSyncObs_Click(object sender, RoutedEventArgs e)
    {
        var hotkey = HotkeySyncService.ReadReplayOverlayHotkey();
        if (hotkey != null)
        {
            WizSaveBox.Text = HotkeyService.FormatHotkeyDisplay(hotkey);
        }
        else
        {
            System.Windows.MessageBox.Show(
                "Could not read the replay buffer hotkey.\n\n" +
                "Make sure your streaming software is installed\n" +
                "and has a hotkey assigned to Save Replay Buffer.",
                "Sync Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void WizDetectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = HotkeySyncService.ReadObsRecordDirectory();
        if (dir != null)
        {
            WizFolderBox.Text = dir;
        }
        else
        {
            System.Windows.MessageBox.Show(
                "Could not detect the recording folder.\n\n" +
                "Make sure your streaming software is installed\n" +
                "and has a recording path configured.",
                "Detection Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void WizBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select replay watch folder",
            ShowNewFolderButton = true,
            SelectedPath = WizFolderBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            WizFolderBox.Text = dialog.SelectedPath;
    }

    private static int ParseInt(string text, int fallback, int min, int max)
    {
        if (int.TryParse(text, out int val))
            return Math.Clamp(val, min, max);
        return fallback;
    }
}
