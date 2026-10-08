using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Ellipse = System.Windows.Shapes.Ellipse;
using Claunia.PropertyList;

namespace AmaazLoader;

public partial class MainWindow : Window
{
    private string? selectedIpaPath;
    private string? selectedProvisioningProfilePath;
    private string? selectedSigningCertificatePath;
    private string? signingCertificatePassword;

    private readonly DeviceManager deviceManager;
    private readonly SigningManager signingManager;
    private readonly SigningCertificateManager certificateManager;
    private readonly SigningEnvironmentManager signingEnvironmentManager;
    private readonly SigningBackendManager signingBackendManager;
    private readonly IpaSigner ipaSigner;
    private readonly IpaInstaller ipaInstaller;
    private readonly AppleAccountManager appleAccountManager;
    private readonly InstallerCatalogService installerCatalogService =
        new();
    private readonly PairingManager pairingManager =
        new();
    private readonly DispatcherTimer deviceTimer;

    private SigningInfo? currentSigningInfo;
    private SigningCertificateInfo? currentCertificateInfo;
    private SigningEnvironmentInfo? currentSigningEnvironment;
    private SigningBackendInfo? currentSigningBackend;
    private AppleAccountInfo? currentAppleAccount;

    private string? connectedAppleEmail;

    private bool sideloadInProgress;
    private bool appleAuthenticationInProgress;
    private bool twoFactorPromptShowing;
    private bool quickInstallInProgress;

    private string? pendingPairingTarget;
    private string? lastQuickInstallDeviceUdid;

    private bool quickInstallStatusRefreshInProgress;

    private int currentAnimatedProgress;

    private static string RustBackendPath =>
    AppPaths.BackendExecutable;

    public MainWindow()
    {
        InitializeComponent();

        Version? appVersion =
            typeof(MainWindow).Assembly.GetName().Version;

        AppVersionText.Text =
            appVersion == null
                ? "v1.2.0"
                : $"v{appVersion.Major}.{appVersion.Minor}.{appVersion.Build}";

        deviceManager = new DeviceManager();
        signingManager = new SigningManager();
        certificateManager = new SigningCertificateManager();
        signingEnvironmentManager = new SigningEnvironmentManager();
        signingBackendManager = new SigningBackendManager();
        ipaSigner = new IpaSigner();
        ipaInstaller = new IpaInstaller();
        appleAccountManager = new AppleAccountManager();

        currentSigningEnvironment =
            signingEnvironmentManager.CheckEnvironment();

        currentSigningBackend =
            signingBackendManager.CheckBackend();

        currentAppleAccount =
            appleAccountManager.CheckAccount();

        UpdateAppleAccountStatus();

        ResetSideloadProgress();

        deviceTimer =
            new DispatcherTimer
            {
                Interval =
                    TimeSpan.FromSeconds(2)
            };

        deviceTimer.Tick +=
            DeviceTimer_Tick;

        deviceTimer.Start();

        CheckDevice();
    }

    private void SponsoredLinkButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        const string sponsorUrl =
            "https://buzzonclick.com/jump/next.php?r=12275994";

        // The external link is optional and only opens from this click handler.
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = sponsorUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open the sponsored link: {ex.Message}",
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void SettingsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var settingsWindow =
            new SettingsWindow
            {
                Owner = this
            };

        settingsWindow.ShowDialog();

        currentSigningEnvironment =
            signingEnvironmentManager.CheckEnvironment();

        currentSigningBackend =
            signingBackendManager.CheckBackend();

        ValidateSigning();
    }

    private void PairingButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var pairingWindow =
            new PairingManagerWindow
            {
                Owner = this
            };

        pairingWindow.ShowDialog();
    }

    private void QuickInstallChannel_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        string channel =
            QuickInstallNightlyRadio.IsChecked == true
                ? "Nightly"
                : "Stable";

        QuickInstallSideStoreButton.Content =
            channel == "Nightly"
                ? "Install Nightly"
                : "Install Stable";

        QuickInstallLiveContainerButton.Content =
            channel == "Nightly"
                ? "Install Nightly"
                : "Install Stable";

        QuickInstallStatusText.Text =
            channel == "Nightly"
                ? "Nightly selected • newest development builds."
                : "Stable selected • recommended for most users.";
    }

    private async void QuickFixPairingButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (quickInstallInProgress)
            return;

        if (deviceManager.GetConnectedDevice() == null)
        {
            QuickInstallStatusText.Text =
                "Connect and unlock your iPhone first.";

            return;
        }

        quickInstallInProgress =
            true;

        QuickFixPairingButton.IsEnabled =
            false;

        QuickInstallSideStoreButton.IsEnabled =
            false;

        QuickInstallLiveContainerButton.IsEnabled =
            false;

        QuickFixPairingButton.IsEnabled =
            false;

        QuickInstallProgressBar.Value =
            25;

        QuickInstallStatusText.Text =
            "Rebuilding pairing...";

        try
        {
            PairingOperationResult result =
                await pairingManager.RebuildAllAsync();

            if (!result.Success)
            {
                QuickInstallProgressBar.Value =
                    0;

                QuickInstallStatusText.Text =
                    "Pairing repair failed.";

                MessageBox.Show(
                    result.Error,
                    "AmaazLoader Pairing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            QuickInstallProgressBar.Value =
                100;

            QuickInstallStatusText.Text =
                "Pairing repaired successfully ✓";

            await RefreshQuickInstallInstalledStateAsync();
        }
        catch (Exception ex)
        {
            QuickInstallProgressBar.Value =
                0;

            QuickInstallStatusText.Text =
                "Pairing repair failed.";

            MessageBox.Show(
                ex.Message,
                "AmaazLoader Pairing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            quickInstallInProgress =
                false;

            QuickFixPairingButton.IsEnabled =
                true;

            QuickInstallSideStoreButton.IsEnabled =
                true;

            QuickInstallLiveContainerButton.IsEnabled =
                true;

            QuickFixPairingButton.IsEnabled =
                true;
        }
    }

    private async void QuickInstallSideStoreButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunQuickInstallAsync(
            "sidestore",
            "SideStore");
    }

    private async void QuickInstallLiveContainerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        await RunQuickInstallAsync(
            "livecontainer",
            "LiveContainer");
    }

    private async Task RunQuickInstallAsync(
        string appId,
        string fallbackName)
    {
        if (quickInstallInProgress)
            return;

        quickInstallInProgress =
            true;

        QuickInstallSideStoreButton.IsEnabled =
            false;

        QuickInstallLiveContainerButton.IsEnabled =
            false;

        QuickInstallStableRadio.IsEnabled =
            false;

        QuickInstallNightlyRadio.IsEnabled =
            false;

        QuickInstallProgressBar.Value =
            0;

        QuickInstallStatusText.Text =
            $"Preparing {fallbackName}...";

        try
        {
            IReadOnlyList<InstallerCatalogItem> apps =
                await installerCatalogService.LoadCatalogAsync();

            InstallerCatalogItem? app =
                apps.FirstOrDefault(
                    item =>
                        item.Id.Equals(
                            appId,
                            StringComparison.OrdinalIgnoreCase));

            if (app == null)
            {
                throw new InvalidOperationException(
                    $"{fallbackName} is not available in the installer catalog.");
            }

            string channel =
                QuickInstallNightlyRadio.IsChecked == true
                    ? "Nightly"
                    : "Stable";

            InstallerResolvedDownload release =
                await installerCatalogService.ResolveChannelReleaseAsync(
                    app,
                    channel);

            QuickInstallStatusText.Text =
                $"{app.Name} • {channel} • preparing download...";

            QuickInstallStatusText.Text =
                $"Downloading {app.Name} {release.Version}...";

            var progress =
                new Progress<double>(
                    value =>
                    {
                        QuickInstallProgressBar.Value =
                            value;

                        QuickInstallStatusText.Text =
                            $"Downloading {app.Name}... {value:0}%";
                    });

            string ipaPath =
                await installerCatalogService.DownloadAsync(
                    release,
                    progress);

            pendingPairingTarget =
                app.Id.Equals(
                    "livecontainer",
                    StringComparison.OrdinalIgnoreCase)
                    ? "all"
                    : "sidestore";

            LoadIpaFromPath(
                ipaPath,
                $"{app.Name} downloaded and loaded. Pairing will be configured automatically after installation.");

            QuickInstallProgressBar.Value =
                100;

            QuickInstallStatusText.Text =
                $"{app.Name} is ready to sideload ✓";

            bool canStartImmediately =
                currentAppleAccount?.IsConnected == true &&
                !string.IsNullOrWhiteSpace(
                    connectedAppleEmail) &&
                deviceManager.GetConnectedDevice() != null;

            if (canStartImmediately)
            {
                QuickInstallStatusText.Text =
                    $"{app.Name} ready • starting one-click install...";

                SideloadButton.RaiseEvent(
                    new RoutedEventArgs(
                        Button.ClickEvent));
            }
        }
        catch (Exception ex)
        {
            QuickInstallProgressBar.Value =
                0;

            QuickInstallStatusText.Text =
                $"Could not prepare {fallbackName}.";

            MessageBox.Show(
                ex.Message,
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            quickInstallInProgress =
                false;

            QuickInstallSideStoreButton.IsEnabled =
                true;

            QuickInstallLiveContainerButton.IsEnabled =
                true;

            QuickInstallStableRadio.IsEnabled =
                true;

            QuickInstallNightlyRadio.IsEnabled =
                true;

            QuickFixPairingButton.IsEnabled =
                true;
        }
    }

    private void InstallersButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var installersWindow =
            new InstallersWindow
            {
                Owner = this
            };

        bool? result =
            installersWindow.ShowDialog();

        if (result != true ||
            string.IsNullOrWhiteSpace(
                installersWindow.SelectedIpaPath))
        {
            return;
        }

        string installerName =
            string.IsNullOrWhiteSpace(
                installersWindow.SelectedInstallerName)
                ? "Installer"
                : installersWindow.SelectedInstallerName;

        pendingPairingTarget =
            installerName.Contains(
                "LiveContainer",
                StringComparison.OrdinalIgnoreCase)
                ? "all"
                : installerName.Contains(
                    "SideStore",
                    StringComparison.OrdinalIgnoreCase)
                    ? "sidestore"
                    : null;

        LoadIpaFromPath(
            installersWindow.SelectedIpaPath,
            $"{installerName} downloaded and loaded. Connect your Apple Account and sideload when ready.");
    }

    private void DeviceTimer_Tick(
        object? sender,
        EventArgs e)
    {
        CheckDevice();
    }

    private void CheckDevice()
    {
        DeviceInfo? device =
            deviceManager.GetConnectedDevice();

        if (device != null)
        {
            DeviceNameText.Text =
                string.IsNullOrWhiteSpace(
                    device.Name)
                    ? "iPhone / iPad"
                    : device.Name;

            DeviceUdidText.Text =
                $"iOS {device.iOSVersion} • {device.Udid}";

            DeviceStatusText.Text =
                "Connected";

            DeviceStatusText.Foreground =
                new SolidColorBrush(
                    Colors.LightGreen);

            DeviceStatusDot.Fill =
                new SolidColorBrush(
                    Colors.LightGreen);

            if (!string.Equals(
                lastQuickInstallDeviceUdid,
                device.Udid,
                StringComparison.OrdinalIgnoreCase))
            {
                lastQuickInstallDeviceUdid =
                    device.Udid;

                _ =
                    RefreshQuickInstallInstalledStateAsync();
            }
        }
        else
        {
            DeviceNameText.Text =
                "No device connected";

            DeviceUdidText.Text =
                "Connect an iPhone or iPad via USB";

            DeviceStatusText.Text =
                "Not Connected";

            DeviceStatusText.Foreground =
                new SolidColorBrush(
                    Colors.Gray);

            DeviceStatusDot.Fill =
                new SolidColorBrush(
                    Colors.Gray);

            lastQuickInstallDeviceUdid =
                null;

            SideStoreInstalledBadge.Text =
                "Device status: connect iPhone";

            SideStoreInstalledBadge.Foreground =
                new SolidColorBrush(
                    Colors.Gray);

            LiveContainerInstalledBadge.Text =
                "Device status: connect iPhone";

            LiveContainerInstalledBadge.Foreground =
                new SolidColorBrush(
                    Colors.Gray);
        }

        ValidateSigning();
    }

    private async Task RefreshQuickInstallInstalledStateAsync()
    {
        if (quickInstallStatusRefreshInProgress)
            return;

        quickInstallStatusRefreshInProgress =
            true;

        try
        {
            PairingOperationResult result =
                await pairingManager.ScanAsync();

            if (!result.Success)
            {
                SideStoreInstalledBadge.Text =
                    "Device status: unavailable";

                LiveContainerInstalledBadge.Text =
                    "Device status: unavailable";

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

            SideStoreInstalledBadge.Text =
                sideStoreInstalled
                    ? "Installed on device ✓"
                    : "Not installed";

            SideStoreInstalledBadge.Foreground =
                new SolidColorBrush(
                    sideStoreInstalled
                        ? Colors.LightGreen
                        : Colors.Gray);

            LiveContainerInstalledBadge.Text =
                liveContainerInstalled
                    ? "Installed on device ✓"
                    : "Not installed";

            LiveContainerInstalledBadge.Foreground =
                new SolidColorBrush(
                    liveContainerInstalled
                        ? Colors.LightGreen
                        : Colors.Gray);
        }
        catch
        {
            SideStoreInstalledBadge.Text =
                "Device status: unavailable";

            LiveContainerInstalledBadge.Text =
                "Device status: unavailable";
        }
        finally
        {
            quickInstallStatusRefreshInProgress =
                false;
        }
    }

    private void MainWindow_DragOver(
        object sender,
        DragEventArgs e)
    {
        e.Effects =
            DragDropEffects.None;

        if (!e.Data.GetDataPresent(
            DataFormats.FileDrop))
        {
            e.Handled =
                true;

            return;
        }

        string[]? files =
            e.Data.GetData(
                DataFormats.FileDrop)
            as string[];

        if (files?.Length == 1 &&
            Path.GetExtension(
                files[0])
            .Equals(
                ".ipa",
                StringComparison.OrdinalIgnoreCase))
        {
            e.Effects =
                DragDropEffects.Copy;
        }

        e.Handled =
            true;
    }

    private void MainWindow_Drop(
        object sender,
        DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(
            DataFormats.FileDrop))
        {
            return;
        }

        string[]? files =
            e.Data.GetData(
                DataFormats.FileDrop)
            as string[];

        if (files?.Length != 1)
            return;

        string path =
            files[0];

        if (!Path.GetExtension(
                path)
            .Equals(
                ".ipa",
                StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "Drop a single .ipa file into AmaazLoader.",
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        pendingPairingTarget =
            null;

        LoadIpaFromPath(
            path,
            "IPA loaded from drag & drop.");
    }

    private void ClearSelectedIpaButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        selectedIpaPath =
            null;

        pendingPairingTarget =
            null;

        SelectedIpaText.Text =
            "Drop an IPA here or choose a file";

        AppNameText.Text =
            "No app selected";

        BundleIdText.Text =
            "—";

        VersionText.Text =
            "—";

        FileSizeText.Text =
            "—";

        StatusText.Text =
            "Select an IPA to begin.";

        QuickInstallProgressBar.Value =
            0;

        QuickInstallStatusText.Text =
            "Choose an app above to download and prepare it for sideloading.";

        ResetSideloadProgress();

        ValidateSigning();
    }

    private void AdvancedOptionsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        bool opening =
            AdvancedSigningPanel.Visibility !=
            Visibility.Visible;

        AdvancedSigningPanel.Visibility =
            opening
                ? Visibility.Visible
                : Visibility.Collapsed;

        AdvancedOptionsButton.Content =
            opening
                ? "Advanced signing options  −"
                : "Advanced signing options  +";
    }

    private void SelectIpaButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog
            {
                Title =
                    "Select an IPA file",

                Filter =
                    "iOS App Package (*.ipa)|*.ipa",

                Multiselect =
                    false
            };

        if (dialog.ShowDialog() != true)
            return;

        pendingPairingTarget =
            null;

        LoadIpaFromPath(
            dialog.FileName,
            "IPA loaded successfully.");
    }

    private void LoadIpaFromPath(
        string ipaPath,
        string successMessage)
    {
        selectedIpaPath =
            ipaPath;

        try
        {
            var fileInfo =
                new FileInfo(
                    selectedIpaPath);

            SelectedIpaText.Text =
                fileInfo.Name;

            FileSizeText.Text =
                FormatFileSize(
                    fileInfo.Length);

            ReadIpaMetadata(
                selectedIpaPath);

            StatusText.Text =
                successMessage;

            ValidateSigning();
        }
        catch (Exception ex)
        {
            selectedIpaPath =
                null;

            SelectedIpaText.Text =
                "No IPA selected";

            AppNameText.Text =
                "—";

            BundleIdText.Text =
                "—";

            VersionText.Text =
                "—";

            FileSizeText.Text =
                "—";

            StatusText.Text =
                $"Could not read IPA: {ex.Message}";

            SideloadButton.IsEnabled =
                false;
        }
    }

    private void ReadIpaMetadata(
        string ipaPath)
    {
        using var archive =
            ZipFile.OpenRead(
                ipaPath);

        var appEntry =
            archive.Entries.FirstOrDefault(
                entry =>
                    entry.FullName.StartsWith(
                        "Payload/",
                        StringComparison.OrdinalIgnoreCase) &&
                    entry.FullName.EndsWith(
                        ".app/",
                        StringComparison.OrdinalIgnoreCase));

        if (appEntry == null)
        {
            throw new Exception(
                "No iOS app was found inside the IPA.");
        }

        string appFolder =
            appEntry.FullName;

        string plistPath =
            appFolder +
            "Info.plist";

        var plistEntry =
            archive.GetEntry(
                plistPath);

        if (plistEntry == null)
        {
            throw new Exception(
                "Info.plist was not found.");
        }

        using var plistStream =
            plistEntry.Open();

        var plist =
            PropertyListParser.Parse(
                plistStream);

        if (plist is not NSDictionary dictionary)
        {
            throw new Exception(
                "Invalid Info.plist format.");
        }

        string appName =
            GetPlistString(
                dictionary,
                "CFBundleDisplayName")
            ?? GetPlistString(
                dictionary,
                "CFBundleName")
            ?? "Unknown App";

        string bundleId =
            GetPlistString(
                dictionary,
                "CFBundleIdentifier")
            ?? "Unknown";

        string version =
            GetPlistString(
                dictionary,
                "CFBundleShortVersionString")
            ?? "Unknown";

        AppNameText.Text =
            appName;

        BundleIdText.Text =
            bundleId;

        VersionText.Text =
            version;
    }

    private void SelectSigningCertificateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog
            {
                Title =
                    "Select Signing Certificate",

                Filter =
                    "PKCS#12 Certificate (*.p12;*.pfx)|*.p12;*.pfx",

                Multiselect =
                    false
            };

        if (dialog.ShowDialog() != true)
            return;

        selectedSigningCertificatePath =
            dialog.FileName;

        SigningCertificateText.Text =
            Path.GetFileName(
                selectedSigningCertificatePath);

        string? password =
            ShowPasswordDialog();

        if (password == null)
        {
            SigningCertificateText.Text =
                "No certificate selected";

            selectedSigningCertificatePath =
                null;

            signingCertificatePassword =
                null;

            currentCertificateInfo =
                null;

            ValidateSigning();

            return;
        }

        signingCertificatePassword =
            password;

        currentCertificateInfo =
            certificateManager.ReadCertificate(
                selectedSigningCertificatePath,
                signingCertificatePassword);

        if (currentCertificateInfo == null)
        {
            SigningStatusText.Text =
                "Certificate could not be loaded";

            SigningDetailsText.Text =
                "The certificate may be invalid or the password may be incorrect.";

            SetSigningStatus(
                false);

            return;
        }

        if (currentCertificateInfo.IsExpired)
        {
            SigningStatusText.Text =
                "Signing certificate expired";

            SigningDetailsText.Text =
                $"Expired: {currentCertificateInfo.NotAfter:g}";

            SetSigningStatus(
                false);

            return;
        }

        SigningStatusText.Text =
            "Signing certificate loaded";

        SigningDetailsText.Text =
            $"Expires: {currentCertificateInfo.NotAfter:d}";

        SetSigningStatus(
            true);

        StatusText.Text =
            "Signing certificate loaded successfully.";

        ValidateSigning();
    }

    private void SelectProvisioningProfileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog =
            new OpenFileDialog
            {
                Title =
                    "Select Provisioning Profile",

                Filter =
                    "Provisioning Profile (*.mobileprovision)|*.mobileprovision",

                Multiselect =
                    false
            };

        if (dialog.ShowDialog() != true)
            return;

        selectedProvisioningProfilePath =
            dialog.FileName;

        ProvisioningProfileText.Text =
            Path.GetFileName(
                selectedProvisioningProfilePath);

        currentSigningInfo =
            signingManager.ReadProvisioningProfile(
                selectedProvisioningProfilePath);

        if (currentSigningInfo == null)
        {
            SigningStatusText.Text =
                "Invalid provisioning profile";

            SigningDetailsText.Text =
                "AmaazLoader could not read this profile.";

            SetSigningStatus(
                false);

            return;
        }

        SigningStatusText.Text =
            "Signing profile loaded";

        SigningDetailsText.Text =
            $"Profile: {currentSigningInfo.Name}\n" +
            $"Team: {currentSigningInfo.TeamId}\n" +
            $"App ID: {currentSigningInfo.AppId}\n" +
            $"Bundle ID: {currentSigningInfo.ProfileBundleId}\n" +
            $"Devices: {currentSigningInfo.ProvisionedDevices.Count}\n" +
            $"Expires: {currentSigningInfo.ExpirationDate:d}";

        SetSigningStatus(
            true);

        StatusText.Text =
            "Signing profile loaded successfully.";

        ValidateSigning();
    }

    private void ValidateSigning()
    {
        if (sideloadInProgress ||
            appleAuthenticationInProgress)
        {
            SideloadButton.IsEnabled =
                false;

            return;
        }

        currentSigningEnvironment =
            signingEnvironmentManager.CheckEnvironment();

        currentSigningBackend =
            signingBackendManager.CheckBackend();

        if (!File.Exists(
            RustBackendPath))
        {
            SideloadButton.IsEnabled =
                false;

            SigningStatusText.Text =
                "Automatic signing backend unavailable";

            SigningDetailsText.Text =
                $"AmaazLoader could not find the Rust signing backend at:\n{RustBackendPath}";

            SetSigningStatus(
                false);

            return;
        }

        if (string.IsNullOrWhiteSpace(
            selectedIpaPath))
        {
            SideloadButton.IsEnabled =
                false;

            SigningStatusText.Text =
                "Waiting for IPA";

            SigningDetailsText.Text =
                "Select an IPA file to begin automatic signing.";

            SetSigningStatus(
                false);

            return;
        }

        DeviceInfo? device =
            deviceManager.GetConnectedDevice();

        if (device == null)
        {
            SideloadButton.IsEnabled =
                false;

            SigningStatusText.Text =
                "Device not connected";

            SigningDetailsText.Text =
                "Connect your iPhone or iPad via USB.";

            SetSigningStatus(
                false);

            return;
        }

        if (currentAppleAccount?.IsConnected != true ||
            string.IsNullOrWhiteSpace(
                connectedAppleEmail))
        {
            SideloadButton.IsEnabled =
                false;

            SigningStatusText.Text =
                "Apple Account required";

            SigningDetailsText.Text =
                "Connect your Apple Account before starting the automatic sideload.";

            SetSigningStatus(
                false);

            return;
        }

        SigningStatusText.Text =
            "Ready to Sideload";

        SigningDetailsText.Text =
            $"App: {AppNameText.Text}\n" +
            $"Bundle ID: {BundleIdText.Text}\n" +
            $"Device: {device.Name}\n" +
            $"Apple Account: {connectedAppleEmail}\n" +
            "Automatic Apple signing: Ready ✓\n" +
            "Direct USB installation: Ready ✓";

        SetSigningStatus(
            true);

        StatusText.Text =
            "Ready to sideload.";

        SideloadButton.IsEnabled =
            true;
    }

    private async void SideloadButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sideloadInProgress)
            return;

        if (string.IsNullOrWhiteSpace(
            selectedIpaPath))
        {
            StatusText.Text =
                "Select an IPA file first.";

            return;
        }

        if (currentAppleAccount?.IsConnected != true ||
            string.IsNullOrWhiteSpace(
                connectedAppleEmail))
        {
            StatusText.Text =
                "Connect your Apple Account first.";

            MessageBox.Show(
                "Connect your Apple Account before starting the automatic sideload.",
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        DeviceInfo? device =
            deviceManager.GetConnectedDevice();

        if (device == null)
        {
            StatusText.Text =
                "Connect your iPhone first.";

            return;
        }

        string? password =
            ShowApplePasswordDialog();

        if (password == null)
            return;

        sideloadInProgress =
            true;

        SideloadButton.IsEnabled =
            false;

        BeginSideloadProgress();

        SigningStatusText.Text =
            "Starting automatic sideload...";

        SigningDetailsText.Text =
            "AmaazLoader is starting the Apple signing backend.";

        StatusText.Text =
            "Starting sideload...";

        SetSigningStatus(
            true);

        try
        {
            bool success =
                await RunAutomaticSideloadAsync(
                    connectedAppleEmail,
                    password,
                    selectedIpaPath);

            if (success)
            {
                bool pairingConfigured =
                    false;

                string pairingMessage =
                    "";

                if (!string.IsNullOrWhiteSpace(
                    pendingPairingTarget))
                {
                    UpdateSideloadProgress(
                        97,
                        "Pairing",
                        pendingPairingTarget == "sidestore"
                            ? "Importing and verifying the pairing file inside SideStore automatically."
                            : "Configuring and verifying pairing inside the installed apps automatically.");

                    StatusText.Text =
                        pendingPairingTarget == "sidestore"
                            ? "Importing pairing file into SideStore..."
                            : "Configuring pairing file...";

                    PairingOperationResult pairingResult =
                        pendingPairingTarget == "sidestore"
                            ? await pairingManager.PlaceSideStoreAsync()
                            : await pairingManager.PlaceAllAsync();

                    if (!pairingResult.Success)
                    {
                        UpdateSideloadProgress(
                            98,
                            "Pairing",
                            "The first pairing attempt did not validate. AmaazLoader is rebuilding pairing automatically.");

                        pairingResult =
                            pendingPairingTarget == "sidestore"
                                ? await pairingManager.RebuildSideStoreAsync()
                                : await pairingManager.RebuildAllAsync();
                    }

                    pairingConfigured =
                        pairingResult.Success;

                    pairingMessage =
                        pairingConfigured
                            ? pendingPairingTarget == "sidestore"
                                ? "\nSideStore pairing: Imported automatically ✓"
                                : "\nPairing file: Configured automatically ✓"
                            : pendingPairingTarget == "sidestore"
                                ? "\nSideStore pairing: Installation succeeded, but automatic import needs attention."
                                : "\nPairing file: App installed, but pairing setup needs attention.";

                    if (pairingConfigured)
                    {
                        pendingPairingTarget =
                            null;
                    }
                }

                CompleteSideloadProgress();

                SigningStatusText.Text =
                    "Sideload completed successfully";

                SigningDetailsText.Text =
                    $"App: {AppNameText.Text}\n" +
                    $"Bundle ID: {BundleIdText.Text}\n" +
                    $"Device: {device.Name}\n\n" +
                    "The application was signed and installed directly to your iPhone." +
                    pairingMessage;

                StatusText.Text =
                    pairingConfigured
                        ? "Sideload and pairing setup completed successfully."
                        : "Sideload completed successfully.";

                SideloadSuccessText.Text =
                    pairingConfigured
                        ? $"{AppNameText.Text} installed + paired ✓"
                        : $"{AppNameText.Text} installed ✓";

                QuickInstallStatusText.Text =
                    pairingConfigured
                        ? $"{AppNameText.Text} installed and paired ✓"
                        : $"{AppNameText.Text} installed ✓";

                SetSigningStatus(
                    true);

                _ =
                    RefreshQuickInstallInstalledStateAsync();

                MessageBox.Show(
                    $"The IPA was signed and installed successfully.\n\n" +
                    $"App: {AppNameText.Text}\n" +
                    $"Bundle ID: {BundleIdText.Text}" +
                    pairingMessage,
                    "AmaazLoader",
                    MessageBoxButton.OK,
                    pairingConfigured ||
                    string.IsNullOrWhiteSpace(
                        pendingPairingTarget)
                        ? MessageBoxImage.Information
                        : MessageBoxImage.Warning);
            }
            else
            {
                FailSideloadProgress();

                StatusText.Text =
                    "Sideload failed.";
            }
        }
        catch (Exception ex)
        {
            FailSideloadProgress();

            SigningStatusText.Text =
                "Sideload failed";

            SigningDetailsText.Text =
                ex.Message;

            StatusText.Text =
                "An error occurred during sideloading.";

            SetSigningStatus(
                false);

            MessageBox.Show(
                ex.Message,
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            password =
                string.Empty;

            sideloadInProgress =
                false;

            ValidateSigning();
        }
    }

    private async Task<bool> RunAutomaticSideloadAsync(
        string email,
        string password,
        string ipaPath)
    {
        if (!File.Exists(
            RustBackendPath))
        {
            throw new FileNotFoundException(
                "AmaazLoader signing backend was not found.",
                RustBackendPath);
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    RustBackendPath,

                UseShellExecute =
                    false,

                RedirectStandardInput =
                    true,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                CreateNoWindow =
                    true,

                WorkingDirectory =
                    Path.GetDirectoryName(
                        RustBackendPath)!
            };

        startInfo.ArgumentList.Add(
            "--gui");

        startInfo.ArgumentList.Add(
            "--email");

        startInfo.ArgumentList.Add(
            email);

        startInfo.ArgumentList.Add(
            "--ipa");

        startInfo.ArgumentList.Add(
            ipaPath);

        using var process =
            new Process
            {
                StartInfo =
                    startInfo,

                EnableRaisingEvents =
                    true
            };

        var outputBuilder =
            new StringBuilder();

        var errorBuilder =
            new StringBuilder();

        TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            process.Start();

            Task outputTask =
                ReadBackendOutputAsync(
                    process,
                    outputBuilder,
                    completion);

            Task errorTask =
                ReadBackendErrorAsync(
                    process,
                    errorBuilder);

            await WriteBackendPasswordAsync(
                process,
                password);

            password =
                string.Empty;

            bool success =
                await completion.Task;

            await Task.WhenAll(
                outputTask,
                errorTask);

            if (!process.HasExited)
            {
                await process.WaitForExitAsync();
            }

            if (success)
                return true;

            string error =
                errorBuilder
                    .ToString()
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                error))
            {
                error =
                    "The automatic signing backend did not complete successfully.";
            }

            string backendOutput =
                outputBuilder
                    .ToString()
                    .Trim();

            if (!string.IsNullOrWhiteSpace(
                backendOutput))
            {
                error =
                    $"{error}\n\nBackend output:\n{backendOutput}";
            }

            SigningStatusText.Text =
                "Sideload failed";

            SigningDetailsText.Text =
                error;

            StatusText.Text =
                "The Rust signing backend reported a failure.";

            SetSigningStatus(
                false);

            MessageBox.Show(
                error,
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return false;
        }
        catch
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(
                        true);
                }
                catch
                {
                    // Ignore cleanup failure.
                }
            }

            throw;
        }
        finally
        {
            password =
                string.Empty;
        }
    }

    private async Task WriteBackendPasswordAsync(
        Process process,
        string password)
    {
        await process.StandardInput.WriteLineAsync(
            password);

        await process.StandardInput.FlushAsync();

        password =
            string.Empty;
    }

    private async Task ReadBackendOutputAsync(
        Process process,
        StringBuilder outputBuilder,
        TaskCompletionSource<bool> completion)
    {
        try
        {
            while (true)
            {
                string? line =
                    await process.StandardOutput.ReadLineAsync();

                if (line == null)
                    break;

                outputBuilder.AppendLine(
                    line);

                await Dispatcher.InvokeAsync(
                    () =>
                        ProcessBackendEvent(
                            line,
                            process,
                            completion));
            }

            if (!completion.Task.IsCompleted &&
                process.HasExited)
            {
                completion.TrySetResult(
                    process.ExitCode == 0);
            }
        }
        catch (Exception ex)
        {
            completion.TrySetException(
                ex);
        }
    }

    private async Task ReadBackendErrorAsync(
        Process process,
        StringBuilder errorBuilder)
    {
        while (true)
        {
            string? line =
                await process.StandardError.ReadLineAsync();

            if (line == null)
                break;

            errorBuilder.AppendLine(
                line);
        }
    }

    private void ProcessBackendEvent(
        string line,
        Process process,
        TaskCompletionSource<bool> completion)
    {
        if (line.StartsWith(
            "AMAaz_EVENT:",
            StringComparison.OrdinalIgnoreCase))
        {
            string eventName =
                line[
                    "AMAaz_EVENT:".Length..]
                    .Trim();

            switch (eventName.ToUpperInvariant())
            {
                case "BACKEND_STARTED":

                    UpdateSideloadProgress(
                        0,
                        "Preparing",
                        "Preparing Apple signing services.");

                    SigningStatusText.Text =
                        "Starting AmaazLoader backend";

                    SigningDetailsText.Text =
                        "Preparing Apple signing services.";

                    StatusText.Text =
                        "Starting backend...";

                    break;

                case "WAITING_FOR_PASSWORD":

                    UpdateSideloadProgress(
                        5,
                        "Authenticating",
                        "Preparing your Apple Account credentials.");

                    SigningStatusText.Text =
                        "Authenticating Apple Account";

                    SigningDetailsText.Text =
                        "Sending your Apple Account credentials to Apple.";

                    StatusText.Text =
                        "Authenticating with Apple...";

                    break;

                case "AUTHENTICATION_STARTED":

                    UpdateSideloadProgress(
                        10,
                        "Authenticating",
                        "Contacting Apple's authentication services.");

                    SigningStatusText.Text =
                        "Authenticating Apple Account";

                    SigningDetailsText.Text =
                        "Contacting Apple's authentication services.";

                    StatusText.Text =
                        "Authenticating with Apple...";

                    break;

                case "AUTHENTICATION_SUCCESS":

                    UpdateSideloadProgress(
                        15,
                        "Authenticating",
                        "Apple Account authentication successful.");

                    SigningStatusText.Text =
                        "Apple Account authenticated";

                    SigningDetailsText.Text =
                        "Apple Account authentication successful.";

                    StatusText.Text =
                        "Apple Account authenticated.";

                    break;

                case "2FA_REQUIRED":

                    SigningStatusText.Text =
                        "Two-Factor Authentication Required";

                    SigningDetailsText.Text =
                        "Apple requires additional verification. Follow the verification prompt.";

                    StatusText.Text =
                        "Waiting for Apple verification...";

                    break;

                case "2FA_WAITING":

                    SigningStatusText.Text =
                        "Enter Apple Verification Code";

                    SigningDetailsText.Text =
                        "Enter the verification code sent to your trusted device.";

                    StatusText.Text =
                        "Waiting for verification code...";

                    if (!twoFactorPromptShowing)
                    {
                        _ =
                            HandleTwoFactorPromptAsync(
                                process);
                    }

                    break;

                case "DEVELOPER_SESSION_STARTED":

                    UpdateSideloadProgress(
                        20,
                        "Authenticating",
                        "Creating an Apple Developer session.");

                    SigningStatusText.Text =
                        "Creating Developer Session";

                    SigningDetailsText.Text =
                        "Connecting to Apple's developer services.";

                    StatusText.Text =
                        "Preparing developer session...";

                    break;

                case "DEVELOPER_SESSION_SUCCESS":

                    UpdateSideloadProgress(
                        25,
                        "Authenticating",
                        "Apple Developer session created successfully.");

                    SigningStatusText.Text =
                        "Developer Session Ready";

                    SigningDetailsText.Text =
                        "Apple Developer session created successfully.";

                    StatusText.Text =
                        "Developer session ready.";

                    break;

                case "SIDELOADER_STARTED":

                    UpdateSideloadProgress(
                        30,
                        "Preparing",
                        "Creating the Apple sideloading session.");

                    SigningStatusText.Text =
                        "Preparing Automatic Signing";

                    SigningDetailsText.Text =
                        "Creating the Apple sideloading session.";

                    StatusText.Text =
                        "Preparing automatic signing...";

                    break;

                case "SIDELOADER_SUCCESS":

                    UpdateSideloadProgress(
                        35,
                        "Preparing",
                        "The Apple sideloader is ready.");

                    SigningStatusText.Text =
                        "Automatic Signing Ready";

                    SigningDetailsText.Text =
                        "The Apple sideloader is ready.";

                    StatusText.Text =
                        "Automatic signing ready.";

                    break;

                case "TEAM_REQUEST_STARTED":

                    UpdateSideloadProgress(
                        40,
                        "Preparing",
                        "Loading your Apple Developer team.");

                    SigningStatusText.Text =
                        "Loading Developer Team";

                    SigningDetailsText.Text =
                        "Requesting your Apple Developer team.";

                    StatusText.Text =
                        "Loading developer team...";

                    break;

                case "TEAM_SUCCESS":

                    UpdateSideloadProgress(
                        45,
                        "Preparing",
                        "Your Apple Developer team is ready for signing.");

                    SigningStatusText.Text =
                        "Developer Team Ready";

                    SigningDetailsText.Text =
                        "Your Apple Developer team is ready for signing.";

                    StatusText.Text =
                        "Developer team ready.";

                    break;

                case "DEVICE_DETECTION_STARTED":

                    UpdateSideloadProgress(
                        50,
                        "Preparing",
                        "Looking for your connected iPhone.");

                    SigningStatusText.Text =
                        "Detecting iPhone";

                    SigningDetailsText.Text =
                        "Looking for your connected iPhone.";

                    StatusText.Text =
                        "Detecting iPhone...";

                    break;

                case "DEVICE_DETECTED":

                    UpdateSideloadProgress(
                        55,
                        "Preparing",
                        "Your iPhone was detected successfully.");

                    SigningStatusText.Text =
                        "iPhone Detected";

                    SigningDetailsText.Text =
                        "Your iPhone was detected successfully.";

                    StatusText.Text =
                        "iPhone detected.";

                    break;

                case "USB_PROVIDER_STARTED":

                    UpdateSideloadProgress(
                        60,
                        "Preparing",
                        "Creating the direct USB device connection.");

                    SigningStatusText.Text =
                        "Preparing USB Connection";

                    SigningDetailsText.Text =
                        "Creating the direct USB device connection.";

                    StatusText.Text =
                        "Preparing USB connection...";

                    break;

                case "USB_PROVIDER_SUCCESS":

                    UpdateSideloadProgress(
                        65,
                        "Preparing",
                        "Direct USB communication with the iPhone is ready.");

                    SigningStatusText.Text =
                        "USB Connection Ready";

                    SigningDetailsText.Text =
                        "Direct USB communication with the iPhone is ready.";

                    StatusText.Text =
                        "USB connection ready.";

                    break;

                case "IPA_SELECTED":

                    UpdateSideloadProgress(
                        70,
                        "Signing",
                        $"Preparing {AppNameText.Text} for automatic signing.");

                    SigningStatusText.Text =
                        "IPA Selected";

                    SigningDetailsText.Text =
                        $"Preparing {AppNameText.Text} for automatic signing.";

                    StatusText.Text =
                        "IPA selected.";

                    break;

                case "SIDELOAD_STARTED":

                    UpdateSideloadProgress(
                        75,
                        "Signing",
                        "AmaazLoader is signing the application and preparing it for installation.");

                    SigningStatusText.Text =
                        "Signing & Installing";

                    SigningDetailsText.Text =
                        "AmaazLoader is automatically signing the application and installing it directly to your iPhone.";

                    StatusText.Text =
                        "Signing and installing...";

                    break;

                case "SIDELOAD_SUCCESS":

                    UpdateSideloadProgress(
                        94,
                        "Installing",
                        "The application is installed. AmaazLoader is finishing setup.");

                    SigningStatusText.Text =
                        "Installation Complete";

                    SigningDetailsText.Text =
                        "The application was successfully signed and installed directly to the iPhone.";

                    StatusText.Text =
                        "Installation complete.";

                    SetSigningStatus(
                        true);

                    completion.TrySetResult(
                        true);

                    break;

                case "BACKEND_COMPLETED":

                    if (!sideloadInProgress)
                    {
                        completion.TrySetResult(
                            true);
                    }

                    break;

                case "AUTHENTICATION_FAILED":
                case "DEVELOPER_SESSION_FAILED":
                case "SIDELOADER_FAILED":
                case "TEAM_FAILED":
                case "DEVICE_DETECTION_FAILED":
                case "USB_PROVIDER_FAILED":
                case "SIDELOAD_FAILED":

                    FailSideloadProgress();

                    SigningStatusText.Text =
                        "Sideload failed";

                    SigningDetailsText.Text =
                        $"The signing backend reported: {eventName}";

                    StatusText.Text =
                        "Automatic sideload failed.";

                    SetSigningStatus(
                        false);

                    completion.TrySetResult(
                        false);

                    break;
            }

            return;
        }

        if (line.StartsWith(
            "AMAaz_PROGRESS:",
            StringComparison.OrdinalIgnoreCase))
        {
            string value =
                line[
                    "AMAaz_PROGRESS:".Length..]
                    .Trim();

            if (int.TryParse(
                value,
                out int progress))
            {
                UpdateSideloadProgress(
                    progress,
                    GetProgressStage(
                        progress),
                    GetProgressDescription(
                        progress));

                SigningStatusText.Text =
                    $"Signing & Installing — {progress}%";

                SigningDetailsText.Text =
                    GetProgressDescription(
                        progress);

                StatusText.Text =
                    $"Sideload progress: {progress}%";
            }

            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_NAME:",
            StringComparison.OrdinalIgnoreCase))
        {
            string teamName =
                line[
                    "AMAaz_TEAM_NAME:".Length..]
                    .Trim();

            SigningDetailsText.Text =
                $"Developer Team: {teamName}";

            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_ID:",
            StringComparison.OrdinalIgnoreCase))
        {
            string teamId =
                line[
                    "AMAaz_TEAM_ID:".Length..]
                    .Trim();

            SigningDetailsText.Text +=
                $"\nTeam ID: {teamId}";

            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_TYPE:",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_STATUS:",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (line.StartsWith(
            "AMAaz_DEVICE_UDID:",
            StringComparison.OrdinalIgnoreCase))
        {
            string udid =
                line[
                    "AMAaz_DEVICE_UDID:".Length..]
                    .Trim();

            DeviceUdidText.Text =
                $"Connected • {udid}";

            return;
        }

        string lower =
            line.ToLowerInvariant();

        if (lower.Contains(
                "two-factor") ||
            lower.Contains(
                "two factor") ||
            lower.Contains(
                "verification code") ||
            lower.Contains(
                "6-digit") ||
            lower.Contains(
                "trusted device"))
        {
            if (!twoFactorPromptShowing)
            {
                _ =
                    HandleTwoFactorPromptAsync(
                        process);
            }
        }
    }

    private string GetProgressStage(
        int progress)
    {
        return progress switch
        {
            < 15 =>
                "Authenticating",

            < 40 =>
                "Preparing",

            < 70 =>
                "Preparing",

            < 85 =>
                "Signing",

            < 100 =>
                "Installing",

            _ =>
                "Complete"
        };
    }

    private static string GetProgressDescription(
        int progress)
    {
        return progress switch
        {
            <= 10 =>
                "Authenticating with Apple.",

            <= 20 =>
                "Creating your Apple Developer session.",

            <= 30 =>
                "Preparing the Apple sideloading session.",

            <= 40 =>
                "Loading your Apple Developer team.",

            <= 50 =>
                "Registering the connected device.",

            <= 60 =>
                "Preparing the direct USB connection.",

            <= 70 =>
                "Preparing the IPA for signing.",

            <= 80 =>
                "Signing the application.",

            <= 90 =>
                "Installing the signed application on your iPhone.",

            < 100 =>
                "Finalizing installation.",

            _ =>
                "Installation completed successfully."
        };
    }

    private void BeginSideloadProgress()
    {
        currentAnimatedProgress =
            0;

        StopProgressAnimations();

        SideloadProgressCard.Visibility =
            Visibility.Visible;

        SideloadProgressBar.BeginAnimation(
            RangeBase.ValueProperty,
            null);

        SideloadProgressBar.Value =
            0;

        SideloadPercentageText.Text =
            "0%";

        SideloadStageText.Text =
            "Preparing...";

        SideloadProgressDetailsText.Text =
            "Preparing AmaazLoader...";

        SideloadSuccessPanel.Visibility =
            Visibility.Collapsed;

        SetProgressDot(
            ProgressPreparingDot,
            true,
            false);

        SetProgressDot(
            ProgressAuthenticatingDot,
            false,
            false);

        SetProgressDot(
            ProgressSigningDot,
            false,
            false);

        SetProgressDot(
            ProgressInstallingDot,
            false,
            false);

        SetProgressDot(
            ProgressPairingDot,
            false,
            false);
    }

    private void ResetSideloadProgress()
    {
        currentAnimatedProgress =
            0;

        StopProgressAnimations();

        SideloadProgressCard.Visibility =
            Visibility.Collapsed;

        SideloadProgressBar.BeginAnimation(
            RangeBase.ValueProperty,
            null);

        SideloadProgressBar.Value =
            0;

        SideloadPercentageText.Text =
            "0%";

        SideloadStageText.Text =
            "Preparing...";

        SideloadProgressDetailsText.Text =
            "Preparing AmaazLoader...";

        SideloadSuccessPanel.Visibility =
            Visibility.Collapsed;

        SetProgressDot(
            ProgressPreparingDot,
            true,
            false);

        SetProgressDot(
            ProgressAuthenticatingDot,
            false,
            false);

        SetProgressDot(
            ProgressSigningDot,
            false,
            false);

        SetProgressDot(
            ProgressInstallingDot,
            false,
            false);

        SetProgressDot(
            ProgressPairingDot,
            false,
            false);
    }

    private void UpdateSideloadProgress(
        int progress,
        string stage,
        string details)
    {
        progress =
            Math.Clamp(
                progress,
                0,
                100);

        SideloadProgressCard.Visibility =
            Visibility.Visible;

        AnimateProgressBar(
            progress);

        AnimateProgressText(
            progress,
            stage,
            details);

        UpdateProgressDots(
            progress,
            stage);
    }

    private void AnimateProgressBar(
        int targetProgress)
    {
        double currentValue =
            SideloadProgressBar.Value;

        if (double.IsNaN(
            currentValue))
        {
            currentValue =
                currentAnimatedProgress;
        }

        if (targetProgress <
            currentValue)
        {
            SideloadProgressBar.BeginAnimation(
                RangeBase.ValueProperty,
                null);

            SideloadProgressBar.Value =
                targetProgress;

            currentAnimatedProgress =
                targetProgress;

            return;
        }

        var animation =
            new DoubleAnimation
            {
                From =
                    currentValue,

                To =
                    targetProgress,

                Duration =
                    new Duration(
                        TimeSpan.FromMilliseconds(
                            450)),

                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    },

                FillBehavior =
                    FillBehavior.HoldEnd
            };

        SideloadProgressBar.BeginAnimation(
            RangeBase.ValueProperty,
            animation);

        currentAnimatedProgress =
            targetProgress;
    }

    private void AnimateProgressText(
        int progress,
        string stage,
        string details)
    {
        var fadeOut =
            new DoubleAnimation
            {
                From =
                    1,

                To =
                    0.35,

                Duration =
                    new Duration(
                        TimeSpan.FromMilliseconds(
                            90))
            };

        fadeOut.Completed +=
            (_, _) =>
            {
                SideloadPercentageText.Text =
                    $"{progress}%";

                SideloadStageText.Text =
                    stage;

                SideloadProgressDetailsText.Text =
                    details;

                var fadeIn =
                    new DoubleAnimation
                    {
                        From =
                            0.35,

                        To =
                            1,

                        Duration =
                            new Duration(
                                TimeSpan.FromMilliseconds(
                                    180))
                    };

                SideloadPercentageText.BeginAnimation(
                    UIElement.OpacityProperty,
                    fadeIn);

                SideloadStageText.BeginAnimation(
                    UIElement.OpacityProperty,
                    fadeIn);

                SideloadProgressDetailsText.BeginAnimation(
                    UIElement.OpacityProperty,
                    fadeIn);
            };

        SideloadPercentageText.BeginAnimation(
            UIElement.OpacityProperty,
            fadeOut);

        SideloadStageText.BeginAnimation(
            UIElement.OpacityProperty,
            fadeOut);

        SideloadProgressDetailsText.BeginAnimation(
            UIElement.OpacityProperty,
            fadeOut);
    }

    private void UpdateProgressDots(
        int progress,
        string stage)
    {
        bool preparingComplete =
            progress >= 40;

        bool authenticatingComplete =
            progress >= 30;

        bool signingComplete =
            progress >= 85;

        bool installingComplete =
            progress >= 100;

        bool preparingActive =
            stage.Equals(
                "Preparing",
                StringComparison.OrdinalIgnoreCase);

        bool authenticatingActive =
            stage.Equals(
                "Authenticating",
                StringComparison.OrdinalIgnoreCase);

        bool signingActive =
            stage.Equals(
                "Signing",
                StringComparison.OrdinalIgnoreCase);

        bool installingActive =
            stage.Equals(
                "Installing",
                StringComparison.OrdinalIgnoreCase);

        bool pairingActive =
            stage.Equals(
                "Pairing",
                StringComparison.OrdinalIgnoreCase);

        bool pairingComplete =
            stage.Equals(
                "Complete",
                StringComparison.OrdinalIgnoreCase) ||
            progress >= 100;

        bool complete =
            stage.Equals(
                "Complete",
                StringComparison.OrdinalIgnoreCase) ||
            progress >= 100;

        SetProgressDot(
            ProgressPreparingDot,
            preparingActive ||
            preparingComplete ||
            complete,
            preparingActive &&
            !preparingComplete);

        SetProgressDot(
            ProgressAuthenticatingDot,
            authenticatingActive ||
            authenticatingComplete ||
            complete,
            authenticatingActive &&
            !authenticatingComplete);

        SetProgressDot(
            ProgressSigningDot,
            signingActive ||
            signingComplete ||
            complete,
            signingActive &&
            !signingComplete);

        SetProgressDot(
            ProgressInstallingDot,
            installingActive ||
            installingComplete ||
            complete,
            installingActive &&
            !installingComplete);

        SetProgressDot(
            ProgressPairingDot,
            pairingActive ||
            pairingComplete,
            pairingActive &&
            !pairingComplete);
    }

    private void SetProgressDot(
        Ellipse dot,
        bool activeOrCompleted,
        bool pulsing)
    {
        dot.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        if (!activeOrCompleted)
        {
            dot.Fill =
                new SolidColorBrush(
                    Color.FromRgb(
                        48,
                        48,
                        48));

            dot.Opacity =
                0.65;

            dot.Effect =
                null;

            return;
        }

        dot.Fill =
            new SolidColorBrush(
                pulsing
                    ? Colors.White
                    : Colors.LightGreen);

        dot.Opacity =
            1;

        if (pulsing)
        {
            dot.Effect =
                new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color =
                        Colors.White,

                    BlurRadius =
                        14,

                    ShadowDepth =
                        0,

                    Opacity =
                        0.8
                };

            var pulse =
                new DoubleAnimation
                {
                    From =
                        0.55,

                    To =
                        1.0,

                    Duration =
                        new Duration(
                            TimeSpan.FromMilliseconds(
                                650)),

                    AutoReverse =
                        true,

                    RepeatBehavior =
                        RepeatBehavior.Forever
                };

            dot.BeginAnimation(
                UIElement.OpacityProperty,
                pulse);
        }
        else
        {
            dot.Effect =
                new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color =
                        Colors.LightGreen,

                    BlurRadius =
                        9,

                    ShadowDepth =
                        0,

                    Opacity =
                        0.45
                };
        }
    }

    private void CompleteSideloadProgress()
    {
        StopProgressAnimations();

        UpdateSideloadProgress(
            100,
            "Complete",
            "The application was successfully signed and installed.");

        SideloadProgressBar.BeginAnimation(
            RangeBase.ValueProperty,
            null);

        SideloadProgressBar.Value =
            100;

        SideloadPercentageText.Text =
            "100%";

        SideloadStageText.Text =
            "Installation Complete";

        SideloadProgressDetailsText.Text =
            "Your application is now installed on your iPhone.";

        SideloadSuccessPanel.Opacity =
            0;

        SideloadSuccessPanel.Visibility =
            Visibility.Visible;

        var successAnimation =
            new DoubleAnimation
            {
                From =
                    0,

                To =
                    1,

                Duration =
                    new Duration(
                        TimeSpan.FromMilliseconds(
                            350)),

                EasingFunction =
                    new CubicEase
                    {
                        EasingMode =
                            EasingMode.EaseOut
                    }
            };

        SideloadSuccessPanel.BeginAnimation(
            UIElement.OpacityProperty,
            successAnimation);

        SetProgressDot(
            ProgressPreparingDot,
            true,
            false);

        SetProgressDot(
            ProgressAuthenticatingDot,
            true,
            false);

        SetProgressDot(
            ProgressSigningDot,
            true,
            false);

        SetProgressDot(
            ProgressInstallingDot,
            true,
            false);

        SetProgressDot(
            ProgressPairingDot,
            true,
            false);
    }

    private void FailSideloadProgress()
    {
        StopProgressAnimations();

        SideloadProgressCard.Visibility =
            Visibility.Visible;

        SideloadStageText.Text =
            "Sideload Failed";

        SideloadProgressDetailsText.Text =
            "AmaazLoader could not complete the installation.";

        SideloadSuccessPanel.Visibility =
            Visibility.Collapsed;

        SideloadProgressBar.BeginAnimation(
            RangeBase.ValueProperty,
            null);

        SideloadPercentageText.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        SideloadStageText.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        SideloadProgressDetailsText.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        SideloadPercentageText.Opacity =
            1;

        SideloadStageText.Opacity =
            1;

        SideloadProgressDetailsText.Opacity =
            1;

        ResetProgressDotsAfterFailure();
    }

    private void ResetProgressDotsAfterFailure()
    {
        SetProgressDot(
            ProgressPreparingDot,
            false,
            false);

        SetProgressDot(
            ProgressAuthenticatingDot,
            false,
            false);

        SetProgressDot(
            ProgressSigningDot,
            false,
            false);

        SetProgressDot(
            ProgressInstallingDot,
            false,
            false);

        SetProgressDot(
            ProgressPairingDot,
            false,
            false);
    }

    private void StopProgressAnimations()
    {
        SideloadProgressBar.BeginAnimation(
            RangeBase.ValueProperty,
            null);

        SideloadPercentageText.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        SideloadStageText.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        SideloadProgressDetailsText.BeginAnimation(
            UIElement.OpacityProperty,
            null);

        StopDotAnimation(
            ProgressPreparingDot);

        StopDotAnimation(
            ProgressAuthenticatingDot);

        StopDotAnimation(
            ProgressSigningDot);

        StopDotAnimation(
            ProgressInstallingDot);

        StopDotAnimation(
            ProgressPairingDot);
    }

    private static void StopDotAnimation(
        Ellipse dot)
    {
        dot.BeginAnimation(
            UIElement.OpacityProperty,
            null);
    }

    private async void AppleAccountButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (appleAuthenticationInProgress)
            return;

        if (currentAppleAccount?.IsConnected == true)
        {
            var reconnect =
                MessageBox.Show(
                    "Your Apple Account is already connected.\n\n" +
                    "Would you like to authenticate again?",
                    "Apple Account",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

            if (reconnect !=
                MessageBoxResult.Yes)
            {
                return;
            }
        }

        var credentials =
            ShowAppleAccountDialog();

        if (credentials == null)
            return;

        string email =
            credentials.Value.Email;

        string password =
            credentials.Value.Password;

        appleAuthenticationInProgress =
            true;

        AppleAccountStatusText.Text =
            "CONNECTING...";

        AppleAccountStatusText.Foreground =
            new SolidColorBrush(
                Colors.Orange);

        AppleAccountDetailsText.Text =
            "Starting Apple authentication...\nYour password is not being saved.";

        AppleAccountStatusDot.Fill =
            new SolidColorBrush(
                Colors.Orange);

        AppleAccountButton.Content =
            "Connecting...";

        AppleAccountButton.IsEnabled =
            false;

        SideloadButton.IsEnabled =
            false;

        StatusText.Text =
            "Connecting to Apple Account...";

        try
        {
            bool success =
                await RunAppleAccountAuthenticationAsync(
                    email,
                    password);

            if (success)
            {
                connectedAppleEmail =
                    email;

                currentAppleAccount =
                    new AppleAccountInfo
                    {
                        IsConnected =
                            true,

                        Status =
                            "Apple Account connected",

                        Details =
                            "Apple Account authentication completed successfully."
                    };

                AppleAccountStatusText.Text =
                    "CONNECTED";

                AppleAccountStatusText.Foreground =
                    new SolidColorBrush(
                        Colors.LightGreen);

                AppleAccountStatusDot.Fill =
                    new SolidColorBrush(
                        Colors.LightGreen);

                AppleAccountDetailsText.Text =
                    "Apple Account authentication successful.\n" +
                    "Apple signing services are ready.";

                AppleAccountButton.Content =
                    "✓  Connected";

                AppleAccountButton.IsEnabled =
                    true;

                StatusText.Text =
                    "Apple Account connected successfully.";

                MessageBox.Show(
                    "Apple Account connected successfully.\n\n" +
                    "Your Apple Developer session is ready for automatic signing.",
                    "AmaazLoader",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                connectedAppleEmail =
                    null;

                currentAppleAccount =
                    new AppleAccountInfo
                    {
                        IsConnected =
                            false,

                        Status =
                            "Apple Account authentication failed",

                        Details =
                            "The Rust signing backend could not authenticate the Apple Account."
                    };

                UpdateAppleAccountStatus();

                StatusText.Text =
                    "Apple Account authentication failed.";
            }
        }
        catch (Exception ex)
        {
            connectedAppleEmail =
                null;

            currentAppleAccount =
                new AppleAccountInfo
                {
                    IsConnected =
                        false,

                    Status =
                        "Apple Account connection failed",

                    Details =
                        ex.Message
                };

            UpdateAppleAccountStatus();

            StatusText.Text =
                "Apple Account connection failed.";

            MessageBox.Show(
                ex.Message,
                "Apple Account",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            password =
                string.Empty;

            appleAuthenticationInProgress =
                false;

            AppleAccountButton.IsEnabled =
                true;

            ValidateSigning();
        }
    }

    private async Task<bool> RunAppleAccountAuthenticationAsync(
        string email,
        string password)
    {
        if (!File.Exists(
            RustBackendPath))
        {
            throw new FileNotFoundException(
                "AmaazLoader signing backend was not found.",
                RustBackendPath);
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    RustBackendPath,

                UseShellExecute =
                    false,

                RedirectStandardInput =
                    true,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                CreateNoWindow =
                    true,

                WorkingDirectory =
                    Path.GetDirectoryName(
                        RustBackendPath)!
            };

        startInfo.ArgumentList.Add(
            "--gui");

        startInfo.ArgumentList.Add(
            "--email");

        startInfo.ArgumentList.Add(
            email);

        using var process =
            new Process
            {
                StartInfo =
                    startInfo,

                EnableRaisingEvents =
                    true
            };

        var outputBuilder =
            new StringBuilder();

        var errorBuilder =
            new StringBuilder();

        TaskCompletionSource<bool> completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            process.Start();

            Task outputTask =
                ReadAppleAuthenticationOutputAsync(
                    process,
                    outputBuilder,
                    completion);

            Task errorTask =
                ReadBackendErrorAsync(
                    process,
                    errorBuilder);

            await WriteBackendPasswordAsync(
                process,
                password);

            password =
                string.Empty;

            bool success =
                await completion.Task;

            await Task.WhenAll(
                outputTask,
                errorTask);

            if (!process.HasExited)
            {
                await process.WaitForExitAsync();
            }

            if (success)
                return true;

            string error =
                errorBuilder
                    .ToString()
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                error))
            {
                error =
                    "Apple Account authentication did not complete successfully.";
            }

            string backendOutput =
                outputBuilder
                    .ToString()
                    .Trim();

            if (!string.IsNullOrWhiteSpace(
                backendOutput))
            {
                error +=
                    $"\n\nBackend output:\n{backendOutput}";
            }

            throw new Exception(
                error);
        }
        catch
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(
                        true);
                }
                catch
                {
                    // Ignore cleanup failure.
                }
            }

            throw;
        }
        finally
        {
            password =
                string.Empty;
        }
    }

    private async Task ReadAppleAuthenticationOutputAsync(
        Process process,
        StringBuilder outputBuilder,
        TaskCompletionSource<bool> completion)
    {
        try
        {
            while (true)
            {
                string? line =
                    await process.StandardOutput.ReadLineAsync();

                if (line == null)
                    break;

                outputBuilder.AppendLine(
                    line);

                await Dispatcher.InvokeAsync(
                    async () =>
                    {
                        await ProcessAppleAuthenticationLineAsync(
                            line,
                            process,
                            completion);
                    });
            }

            if (!completion.Task.IsCompleted &&
                process.HasExited)
            {
                completion.TrySetResult(
                    process.ExitCode == 0);
            }
        }
        catch (Exception ex)
        {
            completion.TrySetException(
                ex);
        }
    }

    private async Task ProcessAppleAuthenticationLineAsync(
        string line,
        Process process,
        TaskCompletionSource<bool> completion)
    {
        if (line.StartsWith(
            "AMAaz_EVENT:",
            StringComparison.OrdinalIgnoreCase))
        {
            string eventName =
                line[
                    "AMAaz_EVENT:".Length..]
                    .Trim()
                    .ToUpperInvariant();

            switch (eventName)
            {
                case "BACKEND_STARTED":

                    AppleAccountStatusText.Text =
                        "CONNECTING...";

                    AppleAccountDetailsText.Text =
                        "Starting the AmaazLoader signing backend.";

                    StatusText.Text =
                        "Starting Apple signing backend...";

                    break;

                case "WAITING_FOR_PASSWORD":

                    AppleAccountStatusText.Text =
                        "AUTHENTICATING...";

                    AppleAccountDetailsText.Text =
                        "Authenticating your Apple Account.";

                    StatusText.Text =
                        "Authenticating with Apple...";

                    break;

                case "AUTHENTICATION_STARTED":

                    AppleAccountStatusText.Text =
                        "AUTHENTICATING...";

                    AppleAccountDetailsText.Text =
                        "Contacting Apple's authentication services.";

                    StatusText.Text =
                        "Authenticating with Apple...";

                    break;

                case "AUTHENTICATION_SUCCESS":

                    AppleAccountStatusText.Text =
                        "AUTHENTICATED";

                    AppleAccountDetailsText.Text =
                        "Apple Account authentication successful.";

                    StatusText.Text =
                        "Apple Account authenticated.";

                    break;

                case "2FA_REQUIRED":

                    AppleAccountStatusText.Text =
                        "2FA REQUIRED";

                    AppleAccountStatusText.Foreground =
                        new SolidColorBrush(
                            Colors.Orange);

                    AppleAccountDetailsText.Text =
                        "Apple requires additional verification.";

                    StatusText.Text =
                        "Waiting for Apple verification...";

                    break;

                case "2FA_WAITING":

                    AppleAccountStatusText.Text =
                        "2FA REQUIRED";

                    AppleAccountDetailsText.Text =
                        "Enter the verification code sent to your trusted device.";

                    StatusText.Text =
                        "Waiting for verification code...";

                    if (!twoFactorPromptShowing)
                    {
                        await HandleTwoFactorPromptAsync(
                            process);
                    }

                    break;

                case "DEVELOPER_SESSION_STARTED":

                    AppleAccountStatusText.Text =
                        "DEVELOPER SESSION...";

                    AppleAccountDetailsText.Text =
                        "Creating an Apple Developer session.";

                    StatusText.Text =
                        "Creating developer session...";

                    break;

                case "DEVELOPER_SESSION_SUCCESS":

                    AppleAccountStatusText.Text =
                        "DEVELOPER READY";

                    AppleAccountDetailsText.Text =
                        "Apple Developer session created successfully.";

                    StatusText.Text =
                        "Developer session ready.";

                    break;

                case "SIDELOADER_STARTED":

                    AppleAccountStatusText.Text =
                        "PREPARING...";

                    AppleAccountDetailsText.Text =
                        "Preparing automatic Apple signing.";

                    StatusText.Text =
                        "Preparing sideloader...";

                    break;

                case "SIDELOADER_SUCCESS":

                    AppleAccountStatusText.Text =
                        "SIGNING READY";

                    AppleAccountDetailsText.Text =
                        "Automatic Apple signing is ready.";

                    StatusText.Text =
                        "Automatic signing ready.";

                    break;

                case "TEAM_REQUEST_STARTED":

                    AppleAccountStatusText.Text =
                        "LOADING TEAM...";

                    AppleAccountDetailsText.Text =
                        "Loading your Apple Developer team.";

                    StatusText.Text =
                        "Loading developer team...";

                    break;

                case "TEAM_SUCCESS":

                    AppleAccountStatusText.Text =
                        "CONNECTED";

                    AppleAccountStatusText.Foreground =
                        new SolidColorBrush(
                            Colors.LightGreen);

                    AppleAccountStatusDot.Fill =
                        new SolidColorBrush(
                            Colors.LightGreen);

                    AppleAccountDetailsText.Text =
                        "Apple Developer team loaded successfully.\n" +
                        "Apple signing services are ready.";

                    StatusText.Text =
                        "Apple Account connected.";

                    completion.TrySetResult(
                        true);

                    break;

                case "AUTHENTICATION_FAILED":
                case "DEVELOPER_SESSION_FAILED":
                case "SIDELOADER_FAILED":
                case "TEAM_FAILED":

                    completion.TrySetResult(
                        false);

                    break;

                case "BACKEND_COMPLETED":

                    completion.TrySetResult(
                        true);

                    break;
            }

            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_NAME:",
            StringComparison.OrdinalIgnoreCase))
        {
            string teamName =
                line[
                    "AMAaz_TEAM_NAME:".Length..]
                    .Trim();

            AppleAccountDetailsText.Text =
                $"Developer Team: {teamName}";

            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_ID:",
            StringComparison.OrdinalIgnoreCase))
        {
            string teamId =
                line[
                    "AMAaz_TEAM_ID:".Length..]
                    .Trim();

            AppleAccountDetailsText.Text +=
                $"\nTeam ID: {teamId}";

            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_TYPE:",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (line.StartsWith(
            "AMAaz_TEAM_STATUS:",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string lower =
            line.ToLowerInvariant();

        if (lower.Contains(
                "two-factor") ||
            lower.Contains(
                "two factor") ||
            lower.Contains(
                "verification code") ||
            lower.Contains(
                "6-digit") ||
            lower.Contains(
                "trusted device"))
        {
            if (!twoFactorPromptShowing)
            {
                await HandleTwoFactorPromptAsync(
                    process);
            }
        }
    }

    private async Task HandleTwoFactorPromptAsync(
        Process process)
    {
        if (twoFactorPromptShowing)
            return;

        twoFactorPromptShowing =
            true;

        try
        {
            AppleAccountStatusText.Text =
                "2FA REQUIRED";

            AppleAccountStatusText.Foreground =
                new SolidColorBrush(
                    Colors.Orange);

            AppleAccountDetailsText.Text =
                "Apple requires additional verification.";

            StatusText.Text =
                "Waiting for Apple verification...";

            var result =
                ShowTwoFactorDialog();

            if (result == null)
            {
                await process.StandardInput.WriteLineAsync(
                    "abort");

                await process.StandardInput.FlushAsync();

                return;
            }

            if (result.Value.Type ==
                "code")
            {
                await process.StandardInput.WriteLineAsync(
                    $"code:{result.Value.Value}");
            }
            else
            {
                await process.StandardInput.WriteLineAsync(
                    result.Value.Value);
            }

            await process.StandardInput.FlushAsync();
        }
        finally
        {
            twoFactorPromptShowing =
                false;
        }
    }

    // PREMIUM APPLE 2FA WINDOW
    private (string Type, string Value)?
        ShowTwoFactorDialog()
    {
        var window =
            new AppleTwoFactorWindow(
                connectedAppleEmail)
            {
                Owner =
                    this
            };

        bool? dialogResult =
            window.ShowDialog();

        if (dialogResult != true)
            return null;

        string code =
            window.TakeCode();

        if (string.IsNullOrWhiteSpace(
            code))
        {
            return null;
        }

        return (
            "code",
            code);
    }

    private void UpdateAppleAccountStatus()
    {
        if (currentAppleAccount ==
            null)
        {
            return;
        }

        if (currentAppleAccount.IsConnected)
        {
            AppleAccountStatusText.Text =
                "CONNECTED";

            AppleAccountStatusText.Foreground =
                new SolidColorBrush(
                    Colors.LightGreen);

            AppleAccountStatusDot.Fill =
                new SolidColorBrush(
                    Colors.LightGreen);

            string teamName =
                string.IsNullOrWhiteSpace(
                    currentAppleAccount.TeamName)
                    ? "Unknown"
                    : currentAppleAccount.TeamName;

            string teamId =
                string.IsNullOrWhiteSpace(
                    currentAppleAccount.TeamId)
                    ? "Unknown"
                    : currentAppleAccount.TeamId;

            AppleAccountDetailsText.Text =
                $"Developer Team: {teamName}\n" +
                $"Team ID: {teamId}\n" +
                "Apple signing services are ready.";

            AppleAccountButton.Content =
                "✓  Connected";

            AppleAccountButton.IsEnabled =
                true;
        }
        else
        {
            AppleAccountStatusText.Text =
                string.IsNullOrWhiteSpace(
                    currentAppleAccount.Status)
                    ? "NOT CONNECTED"
                    : currentAppleAccount.Status;

            AppleAccountStatusText.Foreground =
                new SolidColorBrush(
                    Colors.Gray);

            AppleAccountStatusDot.Fill =
                new SolidColorBrush(
                    Colors.Gray);

            AppleAccountDetailsText.Text =
                string.IsNullOrWhiteSpace(
                    currentAppleAccount.Details)
                    ? "Connect your Apple Account to enable automatic signing."
                    : currentAppleAccount.Details;

            AppleAccountButton.Content =
                "  Connect Account";

            AppleAccountButton.IsEnabled =
                true;
        }
    }

    private (string Email, string Password)?
        ShowAppleAccountDialog()
    {
        var window =
            new AppleAccountWindow(
                connectedAppleEmail)
            {
                Owner =
                    this
            };

        bool? dialogResult =
            window.ShowDialog();

        if (dialogResult != true)
            return null;

        string email =
            window.AppleEmail;

        string password =
            window.ApplePassword;

        return (
            email,
            password);
    }

    private string? ShowApplePasswordDialog()
    {
        if (string.IsNullOrWhiteSpace(
            connectedAppleEmail))
        {
            MessageBox.Show(
                "Connect your Apple Account before continuing.",
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return null;
        }

        var window =
            new AppleAuthenticationWindow(
                connectedAppleEmail)
            {
                Owner =
                    this
            };

        bool? dialogResult =
            window.ShowDialog();

        if (dialogResult != true)
            return null;

        string password =
            window.TakePassword();

        if (string.IsNullOrEmpty(
            password))
        {
            return null;
        }

        return password;
    }

    private string? ShowPasswordDialog()
    {
        var window =
            new Window
            {
                Title =
                    "Certificate Password",

                Width =
                    440,

                Height =
                    220,

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner,

                Owner =
                    this,

                ResizeMode =
                    ResizeMode.NoResize,

                ShowInTaskbar =
                    false,

                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            24,
                            24,
                            24))
            };

        var panel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        22)
            };

        var label =
            new TextBlock
            {
                Text =
                    "Enter the password for your signing certificate:",

                Foreground =
                    Brushes.White,

                Margin =
                    new Thickness(
                        0,
                        0,
                        0,
                        12)
            };

        var passwordBox =
            new PasswordBox
            {
                Height =
                    34,

                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            40,
                            40,
                            40)),

                Foreground =
                    Brushes.White,

                Padding =
                    new Thickness(
                        8,
                        5,
                        8,
                        5)
            };

        var buttons =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Margin =
                    new Thickness(
                        0,
                        15,
                        0,
                        0)
            };

        var cancelButton =
            new Button
            {
                Content =
                    "Cancel",

                Width =
                    80,

                Height =
                    30,

                Margin =
                    new Thickness(
                        0,
                        0,
                        8,
                        0),

                IsCancel =
                    true
            };

        var okButton =
            new Button
            {
                Content =
                    "Continue",

                Width =
                    90,

                Height =
                    30,

                IsDefault =
                    true
            };

        string? result =
            null;

        cancelButton.Click +=
            (_, _) =>
            {
                passwordBox.Clear();

                window.DialogResult =
                    false;

                window.Close();
            };

        okButton.Click +=
            (_, _) =>
            {
                result =
                    passwordBox.Password;

                passwordBox.Clear();

                window.DialogResult =
                    true;

                window.Close();
            };

        passwordBox.KeyDown +=
            (_, e) =>
            {
                if (e.Key ==
                    System.Windows.Input.Key.Enter)
                {
                    okButton.RaiseEvent(
                        new RoutedEventArgs(
                            Button.ClickEvent));
                }
            };

        buttons.Children.Add(
            cancelButton);

        buttons.Children.Add(
            okButton);

        panel.Children.Add(
            label);

        panel.Children.Add(
            passwordBox);

        panel.Children.Add(
            buttons);

        window.Content =
            panel;

        window.Loaded +=
            (_, _) =>
            {
                passwordBox.Focus();
            };

        bool? dialogResult =
            window.ShowDialog();

        if (dialogResult != true)
            return null;

        return result;
    }

    private void ShowSigningError(
        string status,
        string details)
    {
        SigningStatusText.Text =
            status;

        SigningDetailsText.Text =
            details;

        SetSigningStatus(
            false);

        SideloadButton.IsEnabled =
            false;
    }

    private void SetSigningStatus(
        bool valid)
    {
        SigningStatusDot.Fill =
            new SolidColorBrush(
                valid
                    ? Colors.LightGreen
                    : Colors.Orange);
    }

    private static string? GetPlistString(
        NSDictionary dictionary,
        string key)
    {
        if (!dictionary.ContainsKey(
            key))
        {
            return null;
        }

        var value =
            dictionary[key];

        return value?.ToString();
    }

    private static string FormatFileSize(
        long bytes)
    {
        string[] units =
        {
            "B",
            "KB",
            "MB",
            "GB"
        };

        double size =
            bytes;

        int unit =
            0;

        while (
            size >= 1024 &&
            unit < units.Length - 1)
        {
            size /=
                1024;

            unit++;
        }

        return
            $"{size:0.##} {units[unit]}";
    }

    protected override void OnClosed(
        EventArgs e)
    {
        deviceTimer.Stop();

        StopProgressAnimations();

        signingCertificatePassword =
            null;

        connectedAppleEmail =
            null;

        base.OnClosed(
            e);
    }
}