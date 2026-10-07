using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
        SetBusyState(
            true);

        InstallerStatusText.Text =
            "Loading apps...";

        InstallerDetailsText.Text =
            "Checking official release sources.";

        DownloadProgressBar.Value =
            0;

        try
        {
            IReadOnlyList<InstallerCatalogItem> apps =
                await catalogService.LoadCatalogAsync(
                    cancellationTokenSource.Token);

            InstallerCardsPanel.Children.Clear();

            foreach (InstallerCatalogItem app in apps)
            {
                InstallerCardsPanel.Children.Add(
                    CreateInstallerCard(
                        app));
            }

            InstallerStatusText.Text =
                $"{apps.Count} apps ready";

            InstallerDetailsText.Text =
                "Pick an app and press Install.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            InstallerStatusText.Text =
                "Could not load apps";

            InstallerDetailsText.Text =
                ex.Message;
        }
        finally
        {
            SetBusyState(
                false);
        }
    }

    private Border CreateInstallerCard(
        InstallerCatalogItem app)
    {
        bool recommended =
            app.Id.Equals(
                "sidestore",
                StringComparison.OrdinalIgnoreCase);

        var card =
            new Border
            {
                Width =
                    455,

                MinHeight =
                    255,

                Margin =
                    new Thickness(
                        0,
                        0,
                        18,
                        18),

                Padding =
                    new Thickness(
                        22),

                CornerRadius =
                    new CornerRadius(
                        18),

                Background =
                    new SolidColorBrush(
                        Color.FromRgb(
                            14,
                            14,
                            17)),

                BorderBrush =
                    new SolidColorBrush(
                        recommended
                            ? Color.FromRgb(
                                37,
                                67,
                                47)
                            : Color.FromRgb(
                                36,
                                36,
                                41)),

                BorderThickness =
                    new Thickness(
                        1)
            };

        var root =
            new Grid();

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height =
                    GridLength.Auto
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
                Height =
                    GridLength.Auto
            });


        var header =
            new Grid();

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        58)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    new GridLength(
                        1,
                        GridUnitType.Star)
            });

        header.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width =
                    GridLength.Auto
            });


        var icon =
            new Border
            {
                Width =
                    48,

                Height =
                    48,

                CornerRadius =
                    new CornerRadius(
                        14),

                Background =
                    new SolidColorBrush(
                        recommended
                            ? Color.FromRgb(
                                245,
                                245,
                                247)
                            : Color.FromRgb(
                                28,
                                28,
                                33))
            };

        icon.Child =
            new TextBlock
            {
                Text =
                    string.IsNullOrWhiteSpace(
                        app.Name)
                        ? "A"
                        : app.Name[..1]
                            .ToUpperInvariant(),

                Foreground =
                    new SolidColorBrush(
                        recommended
                            ? Color.FromRgb(
                                8,
                                8,
                                9)
                            : Color.FromRgb(
                                245,
                                245,
                                247)),

                FontSize =
                    18,

                FontWeight =
                    FontWeights.ExtraBold,

                HorizontalAlignment =
                    HorizontalAlignment.Center,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        header.Children.Add(
            icon);


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

        Grid.SetColumn(
            titlePanel,
            1);

        titlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    app.Name,

                Foreground =
                    Brushes.White,

                FontSize =
                    19,

                FontWeight =
                    FontWeights.Bold
            });

        titlePanel.Children.Add(
            new TextBlock
            {
                Text =
                    app.Category,

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            111,
                            111,
                            120)),

                FontSize =
                    10,

                FontWeight =
                    FontWeights.SemiBold,

                Margin =
                    new Thickness(
                        0,
                        5,
                        0,
                        0)
            });

        header.Children.Add(
            titlePanel);


        var badge =
            new Border
            {
                Background =
                    new SolidColorBrush(
                        recommended
                            ? Color.FromRgb(
                                15,
                                33,
                                21)
                            : Color.FromRgb(
                                22,
                                22,
                                27)),

                BorderBrush =
                    new SolidColorBrush(
                        recommended
                            ? Color.FromRgb(
                                31,
                                73,
                                45)
                            : Color.FromRgb(
                                45,
                                45,
                                52)),

                BorderThickness =
                    new Thickness(
                        1),

                CornerRadius =
                    new CornerRadius(
                        10),

                Padding =
                    new Thickness(
                        10,
                        6,
                        10,
                        6),

                VerticalAlignment =
                    VerticalAlignment.Top
            };

        badge.Child =
            new TextBlock
            {
                Text =
                    recommended
                        ? "RECOMMENDED"
                        : "OFFICIAL",

                Foreground =
                    new SolidColorBrush(
                        recommended
                            ? Color.FromRgb(
                                115,
                                245,
                                164)
                            : Color.FromRgb(
                                165,
                                165,
                                173)),

                FontSize =
                    9,

                FontWeight =
                    FontWeights.Bold
            };

        Grid.SetColumn(
            badge,
            2);

        header.Children.Add(
            badge);

        root.Children.Add(
            header);


        var body =
            new StackPanel
            {
                Margin =
                    new Thickness(
                        0,
                        20,
                        0,
                        20)
            };

        Grid.SetRow(
            body,
            1);

        body.Children.Add(
            new TextBlock
            {
                Text =
                    app.Description,

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            166,
                            166,
                            175)),

                FontSize =
                    12,

                TextWrapping =
                    TextWrapping.Wrap,

                LineHeight =
                    19
            });


        var trustLine =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                Margin =
                    new Thickness(
                        0,
                        15,
                        0,
                        0)
            };

        trustLine.Children.Add(
            new Ellipse
            {
                Width =
                    7,

                Height =
                    7,

                Fill =
                    new SolidColorBrush(
                        Color.FromRgb(
                            115,
                            245,
                            164)),

                Margin =
                    new Thickness(
                        0,
                        0,
                        7,
                        0),

                VerticalAlignment =
                    VerticalAlignment.Center
            });

        trustLine.Children.Add(
            new TextBlock
            {
                Text =
                    "Latest official GitHub release",

                Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(
                            119,
                            151,
                            130)),

                FontSize =
                    10,

                FontWeight =
                    FontWeights.SemiBold
            });

        body.Children.Add(
            trustLine);

        root.Children.Add(
            body);


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
                Width =
                    GridLength.Auto
            });

        Grid.SetRow(
            actions,
            2);


        var sourceButton =
            new Button
            {
                Content =
                    "Official Source ↗",

                Tag =
                    app,

                Style =
                    (Style)FindResource(
                        "SecondaryButton"),

                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        sourceButton.Click +=
            SourceButton_Click;

        actions.Children.Add(
            sourceButton);


        var installButton =
            new Button
            {
                Content =
                    "Install",

                Tag =
                    app,

                Style =
                    (Style)FindResource(
                        "PrimaryButton"),

                MinWidth =
                    135
            };

        installButton.Click +=
            InstallButton_Click;

        Grid.SetColumn(
            installButton,
            1);

        actions.Children.Add(
            installButton);

        root.Children.Add(
            actions);

        card.Child =
            root;

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

        SetBusyState(
            true);

        string originalButtonText =
            button.Content?.ToString()
            ?? "Install";

        button.IsEnabled =
            false;

        button.Content =
            "Preparing...";

        DownloadProgressBar.Value =
            0;

        try
        {
            InstallerStatusText.Text =
                $"Preparing {app.Name}...";

            InstallerDetailsText.Text =
                "Finding the latest official release.";

            InstallerResolvedDownload release =
                await catalogService.ResolveLatestReleaseAsync(
                    app,
                    cancellationTokenSource.Token);

            button.Content =
                "Downloading...";

            InstallerStatusText.Text =
                $"Downloading {app.Name}";

            InstallerDetailsText.Text =
                $"{release.Version}  •  {release.FileName}";

            var progress =
                new Progress<double>(
                    value =>
                    {
                        DownloadProgressBar.Value =
                            value;

                        InstallerStatusText.Text =
                            $"Downloading {app.Name}  •  {value:0}%";
                    });

            string ipaPath =
                await catalogService.DownloadAsync(
                    release,
                    progress,
                    cancellationTokenSource.Token);

            SelectedIpaPath =
                ipaPath;

            SelectedInstallerName =
                app.Name;

            button.Content =
                "Ready ✓";

            InstallerStatusText.Text =
                $"{app.Name} is ready";

            InstallerDetailsText.Text =
                "Returning to the main screen so you can sideload it.";

            await Task.Delay(
                450);

            DialogResult =
                true;

            Close();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            InstallerStatusText.Text =
                $"Could not prepare {app.Name}";

            InstallerDetailsText.Text =
                ex.Message;

            MessageBox.Show(
                ex.Message,
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (IsVisible)
            {
                button.Content =
                    originalButtonText;

                button.IsEnabled =
                    true;

                SetBusyState(
                    false);
            }
        }
    }

    private void SourceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not InstallerCatalogItem app ||
            string.IsNullOrWhiteSpace(
                app.Repository))
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

                    UseShellExecute =
                        true
                });
        }
        catch
        {
            MessageBox.Show(
                "AmaazLoader could not open the official source.",
                "AmaazLoader",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SetBusyState(
        bool busy)
    {
        operationInProgress =
            busy;

        RefreshButton.IsEnabled =
            !busy;
    }

    protected override void OnClosed(
        EventArgs e)
    {
        cancellationTokenSource.Cancel();

        cancellationTokenSource.Dispose();

        base.OnClosed(
            e);
    }
}
