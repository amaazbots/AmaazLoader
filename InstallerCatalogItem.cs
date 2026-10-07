using System.Text.Json.Serialization;

namespace AmaazLoader;

public sealed class InstallerCatalogItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "Sideloading";

    [JsonPropertyName("repository")]
    public string Repository { get; set; } = "";

    [JsonPropertyName("assetName")]
    public string AssetName { get; set; } = "";

    [JsonPropertyName("website")]
    public string Website { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    [JsonPropertyName("featured")]
    public bool Featured { get; set; }
}

public sealed class InstallerCatalogEnvelope
{
    [JsonPropertyName("apps")]
    public List<InstallerCatalogItem> Apps { get; set; } = new();
}

public sealed class InstallerResolvedDownload
{
    public required InstallerCatalogItem App { get; init; }

    public required string Version { get; init; }

    public required string FileName { get; init; }

    public required string DownloadUrl { get; init; }

    public string? Sha256 { get; init; }

    public long SizeBytes { get; init; }
}
