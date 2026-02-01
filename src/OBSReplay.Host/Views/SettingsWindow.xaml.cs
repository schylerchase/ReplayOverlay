using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using OBSReplay.Host.Models;
using OBSReplay.Host.Services;

namespace OBSReplay.Host.Views;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;

    /// <summary>
    /// The edited config. Null if the user cancelled.
    /// </summary>
    public AppConfig? Result { get; private set; }

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        LoadConfig();
    }

    private void LoadConfig()
    {
        // Overlay
        WatchFolderBox.Text = _config.WatchFolder;
        SyncObsFolderCb.IsChecked = _config.SyncObsFolder;
        NotificationDurationBox.Text = _config.NotificationDuration.ToString("F1");
        NotificationOpacityBox.Text = _config.NotificationOpacity.ToString();
        NotificationMessageBox.Text = _config.NotificationMessage;
        ShowRecIndicatorCb.IsChecked = _config.ShowRecIndicator;
        SelectComboItem(RecPositionCombo, _config.RecIndicatorPosition);

        // OBS
        ObsPathBox.Text = _config.ObsPath;
        ObsPortBox.Text = _config.ObsPort.ToString();
        ObsPasswordBox.Text = _config.ObsPassword;
        AutoLaunchObsCb.IsChecked = _config.AutoLaunchObs;
        AutoLaunchMinimizedCb.IsChecked = _config.AutoLaunchMinimized;
        OrganizeByGameCb.IsChecked = _config.OrganizeByGame;
        AutoStartBufferCb.IsChecked = _config.AutoStartBuffer;

        // Hotkeys (display in user-friendly format)
        HotkeyEnabledCb.IsChecked = _config.HotkeyEnabled;
        SaveHotkeyBox.Text = HotkeyService.FormatHotkeyDisplay(_config.SaveHotkey);
        ToggleHotkeyBox.Text = HotkeyService.FormatHotkeyDisplay(_config.ToggleHotkey);
        RunAsAdminCb.IsChecked = _config.RunAsAdmin;

        // System
        StartWithWindowsCb.IsChecked = _config.StartWithWindows;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var newConfig = new AppConfig
        {
            // Overlay
            WatchFolder = WatchFolderBox.Text.Trim(),
            SyncObsFolder = SyncObsFolderCb.IsChecked == true,
            NotificationDuration = ParseDouble(NotificationDurationBox.Text, 3.0, 0.5, 10.0),
            NotificationOpacity = ParseInt(NotificationOpacityBox.Text, 25, 10, 100),
            NotificationMessage = NotificationMessageBox.Text.Trim(),
            ShowNotifications = _config.ShowNotifications,
            ShowRecIndicator = ShowRecIndicatorCb.IsChecked == true,
            RecIndicatorPosition = GetComboText(RecPositionCombo) ?? "top-left",

            // OBS
            ObsPath = ObsPathBox.Text.Trim(),
            ObsPort = ParseInt(ObsPortBox.Text, 4455, 1, 65535),
            ObsPassword = ObsPasswordBox.Text,
            AutoLaunchObs = AutoLaunchObsCb.IsChecked == true,
            AutoLaunchMinimized = AutoLaunchMinimizedCb.IsChecked == true,
            OrganizeByGame = OrganizeByGameCb.IsChecked == true,
            AutoStartBuffer = AutoStartBufferCb.IsChecked == true,

            // Hotkeys (convert from display format back to internal)
            HotkeyEnabled = HotkeyEnabledCb.IsChecked == true,
            SaveHotkey = HotkeyService.ParseHotkeyDisplay(SaveHotkeyBox.Text.Trim()),
            ToggleHotkey = HotkeyService.ParseHotkeyDisplay(ToggleHotkeyBox.Text.Trim()),
            RunAsAdmin = RunAsAdminCb.IsChecked == true,

            // System
            StartWithWindows = StartWithWindowsCb.IsChecked == true,

            // Preserve overlay position
            OverlayX = _config.OverlayX,
            OverlayY = _config.OverlayY
        };

        Result = newConfig;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DetectFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = ObsHotkeySyncService.ReadObsRecordDirectory();
        if (dir != null)
        {
            WatchFolderBox.Text = dir;
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

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select replay watch folder",
            ShowNewFolderButton = true,
            SelectedPath = WatchFolderBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            WatchFolderBox.Text = dialog.SelectedPath;
    }

    private void BrowseObs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select streaming software executable",
            Filter = "Executables|*.exe",
            InitialDirectory = Path.GetDirectoryName(ObsPathBox.Text) ?? ""
        };

        if (dialog.ShowDialog() == true)
            ObsPathBox.Text = dialog.FileName;
    }

    private void SyncFromObs_Click(object sender, RoutedEventArgs e)
    {
        var hotkey = ObsHotkeySyncService.ReadObsReplayHotkey();
        if (hotkey != null)
        {
            SaveHotkeyBox.Text = HotkeyService.FormatHotkeyDisplay(hotkey);
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

    private static void SelectComboItem(WpfComboBox combo, string value)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is WpfComboBoxItem item && item.Content?.ToString() == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static string? GetComboText(WpfComboBox combo)
    {
        return (combo.SelectedItem as WpfComboBoxItem)?.Content?.ToString();
    }

    private static double ParseDouble(string text, double fallback, double min, double max)
    {
        if (double.TryParse(text, out double val))
            return Math.Clamp(val, min, max);
        return fallback;
    }

    private static int ParseInt(string text, int fallback, int min, int max)
    {
        if (int.TryParse(text, out int val))
            return Math.Clamp(val, min, max);
        return fallback;
    }
}
