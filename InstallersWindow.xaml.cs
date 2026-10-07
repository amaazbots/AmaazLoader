using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AmaazLoader;

public partial class InstallersWindow : Window
{
    private readonly InstallerCatalogService catalogService =
        new();

    private readonly CancellationTokenSource cancellationTokenSource =
        new();

    private bool operationInProgress;

    public string? SelectedIpaPath { get; private set; }

    public string? SelectedInstallerName { get; private set; }

    public InstallersWindow()
    {
        InitializeComponent();

        Loaded +=
            InstallersWindow_Loaded;
    }

    private async void InstallersWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadCatalogAsync();
    }

    private async void RefreshButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        await LoadCatalogAsync();
    }

    private async Task LoadCatalogAsync()
    {
        SetBusyState(true);

        InstallerStatusText.Text =
            "Loading installer catalog...";

        InstallerDetailsText.Text =
            "Checking official release sources.";

        DownloadProgressBar.Value = 0;

        try
        {
            IReadOnlyList<InstallerCatalogItem> apps =
                await catalogService.LoadCatalogAsync(
                    cancellationTokenSource.Token);

            InstallerCardsPanel.Children.Clear();

            foreach (InstallerCatalogItem app in apps)
            {
                InstallerCardsPanel.Children.Add(
                    CreateInstallerCard(app));
            }

            InstallerStatusText.Text =
                $"{apps.Count} installers available";

            InstallerDetailsText.Text =
                "Choose an app to download its latest official IPA release.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            InstallerStatusText.Text =
                "Could not load installers";

            InstallerDetailsText.Text =
                ex.Message;
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private Border CreateInstallerCard(
        InstallerCatalogItem app)
    {
        var card =
            new Border
            {
                Width = 470,
                MinHeight = 230,
                Margin = new Thickness(0, 0, 18, 18),
                Padding = new Thickness(22),
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromRgb(14, 14, 17)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 36, 41)),
                BorderThickness = new Thickness(1)
            };

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        var header =
            new Grid();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(56)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        var icon =
            new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(13),
                Background =
                    new SolidColorBrush(
                        app.Featured
                            ? Color.FromRgb(245, 245, 247)
                            : Color.FromRgb(26, 26, 31))
            };

        icon.Child =
            new TextBlock
            {
                Text =
                    string.IsNullOrWhiteSpace(app.Name)
                        ? "A"
                        : app.Name[..1].ToUpperInvariant(),

                Foreground =
                    new SolidColorBrush(
                        app.Featured
                            ? Color.FromRgb(8, 8, 9)
                            : Color.FromRgb(245, 245, 247)),

                FontSize = 18,
                FontWeight = FontWeights.ExtraBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        header.Children.Add(icon);

        var titlePanel =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        14,
                        1,
                        0,
                        0)
            };

        Grid.SetColumn(titlePanel, 1);

        titlePanel.Children.Add(
            new TextBlock
            {
                Text = app.Name,
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold
            });

        titlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    $"{app.Category}  •  Official GitHub Release",

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            112,
                            112,
                            121)),

                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            });

        header.Children.Add(titlePanel);

        root.Children.Add(header);

        var body =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0,
                        18,
                        0,
                        18)
            };

        Grid.SetRow(body, 1);

        body.Children.Add(
            new TextBlock
            {
                Text = app.Description,
                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            154,
                            154,
                            163)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19
            });

        if (!string.IsNullOrWhiteSpace(app.Notes))
        {
            body.Children.Add(
                new TextBlock
                {
                    Text = app.Notes,
                    Foreground =
                        new SolidColorBrush(
                            Color.FromRgb(
                                105,
                                105,
                                114)),
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                });
        }

        root.Children.Add(body);

        var actions =
            new Grid();

        actions.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        actions.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        Grid.SetRow(actions, 2);

        var sourceButton =
            new Button
            {
                Content = "View Source",
                Tag = app,
                Background = new SolidColorBrush(Color.FromRgb(27, 27, 32)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 54)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15, 9, 15, 9),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };

        sourceButton.Click +=
            SourceButton_Click;

        actions.Children.Add(sourceButton);

        var installButton =
            new Button
            {
                Content = "Download & Sideload",
                Tag = app,
                Style =
                    (Style)FindResource(
                        "InstallerButton"),
                MinWidth = 160
            };

        installButton.Click +=
            InstallButton_Click;

        Grid.SetColumn(installButton, 1);

        actions.Children.Add(installButton);

        root.Children.Add(actions);

        card.Child = root;

        return card;
    }

    private async void InstallButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        if (sender is not Button button ||
            button.Tag is not InstallerCatalogItem app)
        {
            return;
        }

        SetBusyState(true);

        button.IsEnabled = false;

        DownloadProgressBar.Value = 0;

        try
        {
            InstallerStatusText.Text =
                $"Finding latest {app.Name} release...";

            InstallerDetailsText.Text =
                app.Repository;

            InstallerResolvedDownload release =
                await catalogService.ResolveLatestReleaseAsync(
                    app,
                    cancellationTokenSource.Token);

            InstallerStatusText.Text =
                $"Downloading {app.Name} {release.Version}...";

            InstallerDetailsText.Text =
                release.FileName;

            var progress =
                new Progress<double>(
                    value =>
                    {
                        DownloadProgressBar.Value =
                            value;

                        InstallerStatusText.Text =
                            $"Downloading {app.Name}... {value:0}%";
                    });

            string ipaPath =
                await catalogService.DownloadAsync(
                    release,
                    progress,
                    cancellationTokenSource.Token);

            SelectedIpaPath = ipaPath;
            SelectedInstallerName = app.Name;

            InstallerStatusText.Text =
                $"{app.Name} downloaded and verified";

            InstallerDetailsText.Text =
                "Returning to AmaazLoader to continue the sideload.";

            DialogResult = true;

            Close();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            InstallerStatusText.Text =
                $"Could not install {app.Name}";

            InstallerDetailsText.Text =
                ex.Message;

            MessageBox.Show(
                ex.Message,
                "AmaazLoader Installers",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (IsVisible)
            {
                button.IsEnabled = true;

                SetBusyState(false);
            }
        }
    }

    private void SourceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not InstallerCatalogItem app ||
            string.IsNullOrWhiteSpace(app.Repository))
        {
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        $"https://github.com/{app.Repository}",

                    UseShellExecute = true
                });
        }
        catch
        {
            MessageBox.Show(
                "AmaazLoader could not open the project source.",
                "AmaazLoader Installers",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SetBusyState(
        bool busy)
    {
        operationInProgress = busy;

        RefreshButton.IsEnabled = !busy;
    }

    protected override void OnClosed(
        EventArgs e)
    {
        cancellationTokenSource.Cancel();

        cancellationTokenSource.Dispose();

        base.OnClosed(e);
    }
}
