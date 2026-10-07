using Microsoft.Win32;
using System.Windows;

namespace AmaazLoader;

public partial class PairingManagerWindow : Window
{
    private readonly PairingManager pairingManager =
        new();

    private bool operationInProgress;

    public PairingManagerWindow()
    {
        InitializeComponent();

        Loaded +=
            PairingManagerWindow_Loaded;
    }

    private async void PairingManagerWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshAppsAsync();
    }

    private async void RescanButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RefreshAppsAsync();
    }

    private async Task RefreshAppsAsync()
    {
        if (operationInProgress)
            return;

        operationInProgress =
            true;

        PairingStatusText.Text =
            "Scanning installed apps...";

        PairingDetailsText.Text =
            "Checking the connected device for SideStore and LiveContainer.";

        try
        {
            PairingOperationResult result =
                await pairingManager.ScanAsync();

            if (!result.Success)
            {
                PairingStatusText.Text =
                    "Could not scan pairing apps.";

                PairingDetailsText.Text =
                    result.Error;

                return;
            }

            bool sideStoreInstalled =
                result.Apps.Any(
                    app =>
                        app.Name.Equals(
                            "SideStore",
                            StringComparison.OrdinalIgnoreCase));

            bool liveContainerInstalled =
                result.Apps.Any(
                    app =>
                        app.Name.Equals(
                            "LiveContainer",
                            StringComparison.OrdinalIgnoreCase));

            SideStorePairingStatusText.Text =
                sideStoreInstalled
                    ? "Installed • Ready"
                    : "Not installed";

            LiveContainerPairingStatusText.Text =
                liveContainerInstalled
                    ? "Installed • Ready"
                    : "Not installed";

            PairingStatusText.Text =
                result.Apps.Count == 0
                    ? "No supported apps found."
                    : $"{result.Apps.Count} supported app(s) found.";

            PairingDetailsText.Text =
                "Use Place to refresh one app, or Automatic Setup to place the pairing file in all supported apps.";
        }
        finally
        {
            operationInProgress =
                false;
        }
    }

    private async void PlaceAllButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunPlacementAsync(
            "Placing pairing file in supported apps...",
            () =>
                pairingManager.PlaceAllAsync());
    }

    private async void PlaceSideStoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunPlacementAsync(
            "Placing pairing file in SideStore...",
            () =>
                pairingManager.PlaceSideStoreAsync());
    }

    private async void PlaceLiveContainerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunPlacementAsync(
            "Placing pairing file in LiveContainer...",
            () =>
                pairingManager.PlaceLiveContainerAsync());
    }

    private async Task RunPlacementAsync(
        string message,
        Func<Task<PairingOperationResult>> operation)
    {
        if (operationInProgress)
            return;

        operationInProgress =
            true;

        PairingStatusText.Text =
            message;

        PairingDetailsText.Text =
            "Keep the device connected and unlocked.";

        try
        {
            PairingOperationResult result =
                await operation();

            if (!result.Success)
            {
                PairingStatusText.Text =
                    "Pairing setup failed.";

                PairingDetailsText.Text =
                    result.Error;

                MessageBox.Show(
                    result.Error,
                    "AmaazLoader Pairing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            PairingStatusText.Text =
                "Pairing file placed successfully ✓";

            PairingDetailsText.Text =
                "SideStore or LiveContainer can now use the pairing data from its own app container.";
        }
        finally
        {
            operationInProgress =
                false;
        }

        await RefreshAppsAsync();
    }

    private async void ExportPairingButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        var dialog =
            new SaveFileDialog
            {
                Title =
                    "Export Pairing File",

                Filter =
                    "Pairing File (*.mobiledevicepairing)|*.mobiledevicepairing|Property List (*.plist)|*.plist",

                FileName =
                    "ALTPairingFile.mobiledevicepairing"
            };

        if (dialog.ShowDialog() != true)
            return;

        operationInProgress =
            true;

        PairingStatusText.Text =
            "Generating pairing backup...";

        try
        {
            PairingOperationResult result =
                await pairingManager.ExportAsync(
                    dialog.FileName);

            if (!result.Success)
            {
                PairingStatusText.Text =
                    "Could not export pairing file.";

                PairingDetailsText.Text =
                    result.Error;

                return;
            }

            PairingStatusText.Text =
                "Pairing backup exported ✓";

            PairingDetailsText.Text =
                dialog.FileName;
        }
        finally
        {
            operationInProgress =
                false;
        }
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
