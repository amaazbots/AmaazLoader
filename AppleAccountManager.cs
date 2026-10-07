using System.Diagnostics;
using System.IO;
using System.Text;

namespace AmaazLoader;

public class AppleAccountInfo
{
    public bool IsConnected { get; set; }

    public string Status { get; set; } = "";

    public string Details { get; set; } = "";

    public string TeamName { get; set; } = "";

    public string TeamId { get; set; } = "";
}

public class AppleAccountManager
{
    private static string BackendPath =>
        AppPaths.BackendExecutable;

    public AppleAccountInfo CheckAccount()
    {
        return new AppleAccountInfo
        {
            IsConnected = false,

            Status =
                "Apple Account not connected",

            Details =
                "Connect your Apple Account to enable Apple signing."
        };
    }

    public async Task<AppleAccountInfo> AuthenticateAsync(
        string email,
        string password)
    {
        if (!File.Exists(
            BackendPath))
        {
            return new AppleAccountInfo
            {
                IsConnected = false,

                Status =
                    "Signing backend not found",

                Details =
                    $"AmaazLoader could not find the Rust signing backend at:" +
                    $"{Environment.NewLine}{BackendPath}"
            };
        }

        if (string.IsNullOrWhiteSpace(
            email))
        {
            return new AppleAccountInfo
            {
                IsConnected = false,

                Status =
                    "Apple Account email required",

                Details =
                    "Please enter your Apple Account email address."
            };
        }

        if (string.IsNullOrEmpty(
            password))
        {
            return new AppleAccountInfo
            {
                IsConnected = false,

                Status =
                    "Apple Account password required",

                Details =
                    "Please enter your Apple Account password."
            };
        }

        var startInfo =
            new ProcessStartInfo
            {
                FileName =
                    BackendPath,

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
                        BackendPath)!
            };

        using var process =
            new Process
            {
                StartInfo =
                    startInfo
            };

        var output =
            new StringBuilder();

        var errorOutput =
            new StringBuilder();

        try
        {
            process.Start();

            /*
             * Send the Apple Account credentials
             * to the Rust backend.
             *
             * The password is not written to disk
             * by AmaazLoader.
             */

            await process.StandardInput.WriteLineAsync(
                email);

            await process.StandardInput.WriteLineAsync(
                password);

            await process.StandardInput.FlushAsync();

            /*
             * Remove our local reference after
             * handing it to the backend.
             */

            password =
                string.Empty;

            /*
             * Read stdout continuously so the
             * backend cannot block on its output.
             */

            Task outputTask =
                Task.Run(
                    async () =>
                    {
                        while (true)
                        {
                            string? line =
                                await process
                                    .StandardOutput
                                    .ReadLineAsync();

                            if (line == null)
                                break;

                            output.AppendLine(
                                line);
                        }
                    });

            /*
             * Read stderr continuously as well.
             */

            Task errorTask =
                Task.Run(
                    async () =>
                    {
                        while (true)
                        {
                            string? line =
                                await process
                                    .StandardError
                                    .ReadLineAsync();

                            if (line == null)
                                break;

                            errorOutput.AppendLine(
                                line);
                        }
                    });

            /*
             * Wait for the Rust backend.
             */

            await process.WaitForExitAsync();

            await Task.WhenAll(
                outputTask,
                errorTask);

            string fullOutput =
                output.ToString();

            string fullError =
                errorOutput.ToString();

            /*
             * Successful authentication.
             */

            if (
                process.ExitCode == 0 &&
                fullOutput.Contains(
                    "Apple Account authentication SUCCESS",
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                string teamName =
                    ExtractValue(
                        fullOutput,
                        "Name:");

                string teamId =
                    ExtractValue(
                        fullOutput,
                        "Team ID:");

                return new AppleAccountInfo
                {
                    IsConnected =
                        true,

                    Status =
                        "Apple Account connected",

                    Details =
                        $"Authenticated successfully.{Environment.NewLine}" +
                        $"Developer Team: {teamName}{Environment.NewLine}" +
                        $"Team ID: {teamId}",

                    TeamName =
                        teamName,

                    TeamId =
                        teamId
                };
            }

            /*
             * Authentication failed.
             */

            string details =
                fullOutput.Trim();

            if (!string.IsNullOrWhiteSpace(
                fullError))
            {
                if (!string.IsNullOrWhiteSpace(
                    details))
                {
                    details +=
                        Environment.NewLine +
                        Environment.NewLine;
                }

                details +=
                    fullError.Trim();
            }

            if (string.IsNullOrWhiteSpace(
                details))
            {
                details =
                    "The signing backend exited without returning a result.";
            }

            return new AppleAccountInfo
            {
                IsConnected =
                    false,

                Status =
                    "Apple Account authentication failed",

                Details =
                    details
            };
        }
        catch (Exception ex)
        {
            return new AppleAccountInfo
            {
                IsConnected =
                    false,

                Status =
                    "Unable to start signing backend",

                Details =
                    ex.Message
            };
        }
        finally
        {
            password =
                string.Empty;
        }
    }

    private static string ExtractValue(
        string output,
        string label)
    {
        foreach (
            string line in output.Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed =
                line.Trim();

            if (
                trimmed.StartsWith(
                    label,
                    StringComparison.OrdinalIgnoreCase)
            )
            {
                return trimmed[
                    label.Length..]
                    .Trim();
            }
        }

        return "Unknown";
    }
}