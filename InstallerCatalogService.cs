using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace AmaazLoader;

public sealed class InstallerCatalogService
{
    private const string RemoteCatalogUrl =
        "https://raw.githubusercontent.com/amaazbots/AmaazLoader/main/catalog/apps.json";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public async Task<IReadOnlyList<InstallerCatalogItem>> LoadCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response =
                await HttpClient.GetAsync(RemoteCatalogUrl, cancellationToken);

            response.EnsureSuccessStatusCode();

            string json =
                await response.Content.ReadAsStringAsync(cancellationToken);

            InstallerCatalogEnvelope? catalog =
                JsonSerializer.Deserialize<InstallerCatalogEnvelope>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (catalog?.Apps.Count > 0)
                return catalog.Apps;
        }
        catch
        {
            // Fall back to the built-in trusted list when the remote catalog is unavailable.
        }

        return CreateFallbackCatalog();
    }

    public async Task<InstallerResolvedDownload> ResolveChannelReleaseAsync(
        InstallerCatalogItem app,
        string channel,
        CancellationToken cancellationToken = default)
    {
        if (!channel.Equals(
            "nightly",
            StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveLatestReleaseWithFallbackAsync(
                app,
                cancellationToken);
        }

        string downloadUrl;
        string fileName;

        if (app.Id.Equals(
            "sidestore",
            StringComparison.OrdinalIgnoreCase))
        {
            downloadUrl =
                "https://github.com/SideStore/SideStore/releases/download/nightly/SideStore.ipa";

            fileName =
                "SideStore-Nightly.ipa";
        }
        else if (app.Id.Equals(
            "livecontainer",
            StringComparison.OrdinalIgnoreCase))
        {
            // Resolve official nightly assets dynamically; use the mirror
            // when the upstream repository is unavailable.
            try
            {
                return await ResolveReleaseFromApiAsync(
                    app,
                    "https://api.github.com/repos/LiveContainer/LiveContainer/releases/tags/nightly",
                    "Nightly",
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                return await ResolveMirrorReleaseAsync(app, cancellationToken);
            }

            downloadUrl =
                "https://github.com/LiveContainer/LiveContainer/releases/download/nightly/LiveContainer+SideStore.ipa";

            fileName =
                "LiveContainerSideStore-Nightly.ipa";
        }
        else
        {
            return await ResolveLatestReleaseWithFallbackAsync(
                app,
                cancellationToken);
        }

        return new InstallerResolvedDownload
        {
            App = app,
            Version = "Nightly",
            FileName = fileName,
            DownloadUrl = downloadUrl,
            Sha256 = null,
            SizeBytes = 0
        };
    }

    public async Task<InstallerResolvedDownload> ResolveLatestReleaseWithFallbackAsync(
        InstallerCatalogItem app,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ResolveLatestReleaseAsync(app, cancellationToken);
        }
        catch (HttpRequestException) when (
            app.Id.Equals("livecontainer", StringComparison.OrdinalIgnoreCase))
        {
            // The official LiveContainer repository has experienced temporary
            // unavailability. Use the project mirror only if the primary fails.
            return await ResolveMirrorReleaseAsync(app, cancellationToken);
        }
    }

    private async Task<InstallerResolvedDownload> ResolveMirrorReleaseAsync(
        InstallerCatalogItem app,
        CancellationToken cancellationToken)
    {
        InstallerCatalogItem mirror =
            CloneWithRepository(app, "LiveContainerMirror/LiveContainer");
        try
        {
            return await ResolveLatestReleaseAsync(mirror, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Mirror often publishes a tagged nightly release only.
            // Make the fallback build type explicit in the UI.
            return await ResolveReleaseFromApiAsync(
                mirror,
                "https://api.github.com/repos/LiveContainerMirror/LiveContainer/releases/tags/nightly",
                "Nightly (mirror fallback)",
                cancellationToken);
        }
    }

    private static InstallerCatalogItem CloneWithRepository(
        InstallerCatalogItem source,
        string repository)
    {
        return new InstallerCatalogItem
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Category = source.Category,
            Repository = repository,
            AssetName = source.AssetName,
            Website = source.Website,
            Notes = source.Notes,
            Featured = source.Featured
        };
    }

    public async Task<InstallerResolvedDownload> ResolveLatestReleaseAsync(
        InstallerCatalogItem app,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(app.Repository))
            throw new InvalidOperationException(
                $"{app.Name} does not have an official repository configured.");

        string releaseApiUrl =
            $"https://api.github.com/repos/{app.Repository}/releases/latest";

        return await ResolveReleaseFromApiAsync(
            app,
            releaseApiUrl,
            null,
            cancellationToken);
    }

    private async Task<InstallerResolvedDownload> ResolveReleaseFromApiAsync(
        InstallerCatalogItem app,
        string releaseApiUrl,
        string? overrideVersion,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response =
            await HttpClient.GetAsync(releaseApiUrl, cancellationToken);

        response.EnsureSuccessStatusCode();

        await using Stream stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using JsonDocument document =
            await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken);

        JsonElement root = document.RootElement;

        string version =
            root.TryGetProperty("tag_name", out JsonElement versionElement)
                ? versionElement.GetString() ?? "Latest"
                : "Latest";

        version = overrideVersion ?? version;

        if (!root.TryGetProperty("assets", out JsonElement assetsElement))
            throw new InvalidOperationException(
                $"No downloadable assets were found in the latest {app.Name} release.");

        JsonElement? selectedAsset = null;

        foreach (JsonElement asset in assetsElement.EnumerateArray())
        {
            string assetName =
                asset.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString() ?? ""
                    : "";

            bool matches =
                !string.IsNullOrWhiteSpace(app.AssetName)
                    ? assetName.Equals(
                        app.AssetName,
                        StringComparison.OrdinalIgnoreCase)
                    : assetName.EndsWith(
                        ".ipa",
                        StringComparison.OrdinalIgnoreCase);

            if (!matches)
                continue;

            selectedAsset = asset;
            break;
        }

        if (selectedAsset == null)
            throw new InvalidOperationException(
                $"The latest {app.Name} release does not contain the expected IPA file.");

        JsonElement selected = selectedAsset.Value;

        string fileName =
            selected.GetProperty("name").GetString()
            ?? $"{app.Id}.ipa";

        string downloadUrl =
            selected.GetProperty("browser_download_url").GetString()
            ?? throw new InvalidOperationException(
                "GitHub did not return a download URL.");

        long sizeBytes =
            selected.TryGetProperty("size", out JsonElement sizeElement)
                ? sizeElement.GetInt64()
                : 0;

        string? digest =
            selected.TryGetProperty("digest", out JsonElement digestElement)
                ? digestElement.GetString()
                : null;

        string? sha256 =
            digest?.StartsWith(
                "sha256:",
                StringComparison.OrdinalIgnoreCase) == true
                ? digest["sha256:".Length..]
                : null;

        return new InstallerResolvedDownload
        {
            App = app,
            Version = version,
            FileName = fileName,
            DownloadUrl = downloadUrl,
            Sha256 = sha256,
            SizeBytes = sizeBytes
        };
    }

    public async Task<string> DownloadAsync(
        InstallerResolvedDownload release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string downloadDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "AmaazLoader",
                "Installers");

        Directory.CreateDirectory(downloadDirectory);

        string destinationPath =
            Path.Combine(
                downloadDirectory,
                Path.GetFileName(release.FileName));

        if (File.Exists(
            destinationPath))
        {
            bool cacheValid =
                false;

            if (!string.IsNullOrWhiteSpace(
                release.Sha256))
            {
                string cachedHash =
                    await ComputeSha256Async(
                        destinationPath,
                        cancellationToken);

                cacheValid =
                    cachedHash.Equals(
                        release.Sha256,
                        StringComparison.OrdinalIgnoreCase);
            }
            else if (release.SizeBytes > 0)
            {
                cacheValid =
                    new FileInfo(
                        destinationPath)
                        .Length ==
                    release.SizeBytes;
            }

            if (cacheValid)
            {
                progress?.Report(
                    100);

                return destinationPath;
            }
        }

        using HttpResponseMessage response =
            await HttpClient.GetAsync(
                release.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        if (!response.IsSuccessStatusCode &&
            release.App.Id.Equals("livecontainer", StringComparison.OrdinalIgnoreCase) &&
            release.App.Repository.Equals("LiveContainer/LiveContainer", StringComparison.OrdinalIgnoreCase))
        {
            // Retry through the project mirror, retaining the mirror's own
            // version, asset size and checksum for cache and verification.
            InstallerResolvedDownload mirrorRelease =
                await ResolveMirrorReleaseAsync(release.App, cancellationToken);
            return await DownloadAsync(mirrorRelease, progress, cancellationToken);
        }

        response.EnsureSuccessStatusCode();

        long? totalBytes =
            response.Content.Headers.ContentLength;

        {
            await using Stream source =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            await using var destination =
                new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true);

            byte[] buffer = new byte[81920];
            long totalRead = 0;

            while (true)
            {
                int read =
                    await source.ReadAsync(
                        buffer,
                        cancellationToken);

                if (read == 0)
                    break;

                await destination.WriteAsync(
                    buffer.AsMemory(
                        0,
                        read),
                    cancellationToken);

                totalRead +=
                    read;

                if (totalBytes > 0)
                {
                    progress?.Report(
                        Math.Clamp(
                            totalRead * 100d /
                            totalBytes.Value,
                            0,
                            100));
                }
            }

            await destination.FlushAsync(
                cancellationToken);
        }

        progress?.Report(100);

        if (!string.IsNullOrWhiteSpace(
            release.Sha256))
        {
            string actualHash =
                await ComputeSha256Async(
                    destinationPath,
                    cancellationToken);

            if (!actualHash.Equals(
                release.Sha256,
                StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(
                    destinationPath);

                throw new InvalidOperationException(
                    "The downloaded IPA failed SHA-256 verification.");
            }
        }

        return destinationPath;
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream =
            File.OpenRead(path);

        byte[] hash =
            await SHA256.HashDataAsync(
                stream,
                cancellationToken);

        return Convert.ToHexString(hash)
            .ToLowerInvariant();
    }

    private static HttpClient CreateHttpClient()
    {
        var client =
            new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "AmaazLoader/1.2 (+https://github.com/amaazbots/AmaazLoader)");

        client.DefaultRequestHeaders.Accept.ParseAdd(
            "application/vnd.github+json");

        return client;
    }

    private static IReadOnlyList<InstallerCatalogItem>
        CreateFallbackCatalog()
    {
        return new List<InstallerCatalogItem>
        {
            new()
            {
                Id = "sidestore",
                Name = "SideStore",
                Description = "A community-driven alternative app store for iPhone and iPad.",
                Category = "Sideloading",
                Repository = "SideStore/SideStore",
                AssetName = "SideStore.ipa",
                Website = "https://sidestore.io/",
                Notes = "Official SideStore GitHub release.",
                Featured = true
            },
            new()
            {
                Id = "livecontainer",
                Name = "LiveContainer",
                Description = "Install LiveContainer with SideStore support for app refreshing and container workflows.",
                Category = "Utilities",
                Repository = "LiveContainer/LiveContainer",
                AssetName = "LiveContainer+SideStore.ipa",
                Website = "https://livecontainer.github.io/",
                Notes = "Official LiveContainer + SideStore release with automatic pairing support.",
                Featured = true
            }
        };
    }
}
