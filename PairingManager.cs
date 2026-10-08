using System.Diagnostics;
using System.IO;
using System.Text;

namespace AmaazLoader;

public sealed record PairingAppStatus(
    string Name,
    string BundleId,
    string Path);

public sealed class PairingOperationResult
{
    public bool Success { get; init; }

    public string Output { get; init; } = "";

    public string Error { get; init; } = "";

    public IReadOnlyList<PairingAppStatus> Apps { get; init; } =
        Array.Empty<PairingAppStatus>();
}

public sealed class PairingManager
{
    private static string PairingBackendPath =>
        AppPaths.PairingBackendExecutable;

    public Task<PairingOperationResult> ScanAsync()
    {
        return RunAsync(
            "scan");
    }

    public Task<PairingOperationResult> PlaceAllAsync()
    {
        return RunAsync(
            "place-all");
    }

    public Task<PairingOperationResult> PlaceSideStoreAsync()
    {
        return RunAsync(
            "place-sidestore");
    }

    public Task<PairingOperationResult> PlaceLiveContainerAsync()
    {
        return RunAsync(
            "place-livecontainer");
    }

    public Task<PairingOperationResult> ResetAsync()
    {
        return RunAsync(
            "reset");
    }

    public async Task<PairingOperationResult> RebuildSideStoreAsync()
    {
        PairingOperationResult reset =
            await ResetAsync();

        if (!reset.Success)
            return reset;

        return await PlaceSideStoreAsync();
    }

    public async Task<PairingOperationResult> RebuildAllAsync()
    {
        PairingOperationResult reset =
            await ResetAsync();

        if (!reset.Success)
            return reset;

        return await PlaceAllAsync();
    }

    public Task<PairingOperationResult> ExportAsync(
        string destinationPath)
    {
        return RunAsync(
            "export",
            destinationPath);
    }

    private static async Task<PairingOperationResult> RunAsync(
        params string[] arguments)
    {
        if (!File.Exists(
            PairingBackendPath))
        {
            return new PairingOperationResult
            {
                Success = false,
                Error =
                    "AmaazLoader pairing helper was not found."
            };
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    PairingBackendPath,

                UseShellExecute =
                    false,

                RedirectStandardOutput =
                    true,

                RedirectStandardError =
                    true,

                CreateNoWindow =
                    true,

                WorkingDirectory =
                    Path.GetDirectoryName(
                        PairingBackendPath)!
            };

        foreach (string argument in
                 arguments)
        {
            startInfo.ArgumentList.Add(
                argument);
        }

        using var process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        process.Start();

        Task<string> outputTask =
            process.StandardOutput
                .ReadToEndAsync();

        Task<string> errorTask =
            process.StandardError
                .ReadToEndAsync();

        await process.WaitForExitAsync();

        string output =
            await outputTask;

        string error =
            await errorTask;

        List<PairingAppStatus> apps =
            ParseApps(
                output);

        string readableError =
            ParseReadableError(
                error);

        return new PairingOperationResult
        {
            Success =
                process.ExitCode == 0,

            Output =
                output,

            Error =
                readableError,

            Apps =
                apps
        };
    }

    private static List<PairingAppStatus> ParseApps(
        string output)
    {
        var result =
            new List<PairingAppStatus>();

        using var reader =
            new StringReader(
                output);

        while (reader.ReadLine() is
               string line)
        {
            const string prefix =
                "AMAAZ_PAIRING_APP:";

            if (!line.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value =
                line[
                    prefix.Length..];

            string[] parts =
                value.Split(
                    '|',
                    3);

            if (parts.Length != 3)
                continue;

            result.Add(
                new PairingAppStatus(
                    parts[0],
                    parts[1],
                    parts[2]));
        }

        return result;
    }

    private static string ParseReadableError(
        string error)
    {
        if (string.IsNullOrWhiteSpace(
            error))
        {
            return "";
        }

        const string prefix =
            "AMAAZ_PAIRING_ERROR:";

        using var reader =
            new StringReader(
                error);

        while (reader.ReadLine() is
               string line)
        {
            if (line.StartsWith(
                prefix,
                StringComparison.OrdinalIgnoreCase))
            {
                return line[
                    prefix.Length..]
                    .Trim();
            }
        }

        return error.Trim();
    }
}
