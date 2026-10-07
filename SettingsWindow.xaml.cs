using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace AmaazLoader;

public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;

    public SettingsWindow()
    {
        InitializeComponent();

        settings =
            SettingsManager.Load();

        LoadSettings();
    }

    private void LoadSettings()
    {
        ZSignPathText.Text =
            settings.ZSignPath;

        LibimobiledevicePathText.Text =
            settings.LibimobiledevicePath;

        AutoDetectDeviceCheckBox.IsChecked =
            settings.AutoDetectDevice;

        AutoCleanCheckBox.IsChecked =
            settings.AutoClean;
    }

    private void SaveSettings()
    {
        settings.ZSignPath =
            string.IsNullOrWhiteSpace(
                ZSignPathText.Text)
                ? AppPaths.ZSignExecutable
                : ZSignPathText.Text.Trim();

        settings.LibimobiledevicePath =
            string.IsNullOrWhiteSpace(
                LibimobiledevicePathText.Text)
                ? AppPaths.LibimobiledeviceDirectory
                : LibimobiledevicePathText.Text.Trim();

        settings.AutoDetectDevice =
            AutoDetectDeviceCheckBox.IsChecked ==
            true;

        settings.AutoClean =
            AutoCleanCheckBox.IsChecked ==
            true;

        bool saved =
            SettingsManager.Save(
                settings);

        if (!saved)
        {
            MessageBox.Show(
                "AmaazLoader could not save your settings.",
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void ChangeZSignButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog
            {
                Title =
                    "Select zsign.exe",

                Filter =
                    "zsign executable (*.exe)|*.exe",

                Multiselect =
                    false
            };

        if (dialog.ShowDialog() != true)
            return;

        settings.ZSignPath =
            dialog.FileName;

        ZSignPathText.Text =
            settings.ZSignPath;
    }

    private void ChangeLibimobiledeviceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string? selectedFolder =
            SelectFolder(
                "Select libimobiledevice Folder");

        if (string.IsNullOrWhiteSpace(
            selectedFolder))
        {
            return;
        }

        settings.LibimobiledevicePath =
            selectedFolder;

        LibimobiledevicePathText.Text =
            settings.LibimobiledevicePath;
    }

    private void ChangeOutputFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string? selectedFolder =
            SelectFolder(
                "Select Signed IPA Output Folder");

        if (string.IsNullOrWhiteSpace(
            selectedFolder))
        {
            return;
        }

        settings.OutputFolder =
            selectedFolder;

        MessageBox.Show(
            $"Signed IPA output folder selected:\n\n" +
            $"{settings.OutputFolder}",
            "AmaazLoader",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string? SelectFolder(
        string title)
    {
        var dialog =
            new OpenFileDialog
            {
                Title =
                    title,

                CheckFileExists =
                    false,

                CheckPathExists =
                    true,

                ValidateNames =
                    false,

                FileName =
                    "Select this folder"
            };

        if (dialog.ShowDialog() != true)
            return null;

        return Path.GetDirectoryName(
            dialog.FileName);
    }

    private void OpenYouTubeButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenExternalUrl(
            "https://www.youtube.com/@vTxMazi");
    }

    private void OpenDiscordButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenExternalUrl(
            "https://discord.com/invite/Kt2UkD4Q8k");
    }

    private void OpenWebsiteButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        OpenExternalUrl(
            "https://www.amaazbotsite.com/");
    }

    private static void OpenExternalUrl(
        string url)
    {
        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        url,

                    UseShellExecute =
                        true
                });
        }
        catch
        {
            MessageBox.Show(
                "AmaazLoader could not open this link.",
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void TitleBar_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.LeftButton !=
            MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosed(
        EventArgs e)
    {
        SaveSettings();

        base.OnClosed(
            e);
    }
}