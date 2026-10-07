using System.Diagnostics;
using System.IO;

namespace AmaazLoader;

public class InstallationResult
{
    public bool Success { get; set; }

    public string Output { get; set; } = "";

    public string Error { get; set; } = "";
}

public class IpaInstaller
{
    public InstallationResult Install(
        string ipaPath,
        string udid)
    {
        AppSettings settings =
            SettingsManager.Load();

        string toolsPath =
            settings.LibimobiledevicePath;

        if (string.IsNullOrWhiteSpace(
                toolsPath))
        {
            return new InstallationResult
            {
                Success = false,

                Error =
                    "The libimobiledevice path is not configured."
            };
        }

        string installerPath =
            Path.Combine(
                toolsPath,
                "ideviceinstaller.exe");

        if (!File.Exists(installerPath))
        {
            return new InstallationResult
            {
                Success = false,

                Error =
                    "ideviceinstaller.exe was not found at:\n" +
                    installerPath
            };
        }

        if (!File.Exists(ipaPath))
        {
            return new InstallationResult
            {
                Success = false,

                Error =
                    "The IPA file to install was not found."
            };
        }

        if (string.IsNullOrWhiteSpace(udid))
        {
            return new InstallationResult
            {
                Success = false,

                Error =
                    "No connected device UDID was provided."
            };
        }

        try
        {
            string arguments =
                "-u " +
                Quote(udid) +
                " -i " +
                Quote(ipaPath);

            var process =
                new Process
                {
                    StartInfo =
                        new ProcessStartInfo
                        {
                            FileName =
                                installerPath,

                            Arguments =
                                arguments,

                            UseShellExecute =
                                false,

                            RedirectStandardOutput =
                                true,

                            RedirectStandardError =
                                true,

                            CreateNoWindow =
                                true
                        }
                };

            process.Start();

            string standardOutput =
                process.StandardOutput.ReadToEnd();

            string standardError =
                process.StandardError.ReadToEnd();

            process.WaitForExit();

            bool success =
                process.ExitCode == 0;

            return new InstallationResult
            {
                Success =
                    success,

                Output =
                    standardOutput.Trim(),

                Error =
                    success
                        ? standardError.Trim()
                        : BuildError(
                            process.ExitCode,
                            standardError,
                            standardOutput)
            };
        }
        catch (Exception ex)
        {
            return new InstallationResult
            {
                Success = false,

                Error =
                    ex.Message
            };
        }
    }

    private static string BuildError(
        int exitCode,
        string standardError,
        string standardOutput)
    {
        string details =
            string.IsNullOrWhiteSpace(
                standardError)
                ? standardOutput
                : standardError;

        if (string.IsNullOrWhiteSpace(
                details))
        {
            details =
                $"ideviceinstaller exited with code {exitCode}.";
        }

        return details.Trim();
    }

    private static string Quote(
        string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return "\"" +
               value.Replace(
                   "\"",
                   "\\\"") +
               "\"";
    }
}