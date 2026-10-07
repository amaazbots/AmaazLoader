using System.Diagnostics;
using System.IO;

namespace AmaazLoader;

public class SigningBackendInfo
{
    public bool IsAvailable { get; set; }

    public string ExecutablePath { get; set; } = "";

    public string Version { get; set; } = "";

    public string Status { get; set; } = "";

    public string Details { get; set; } = "";
}

public class SigningBackendManager
{
    public SigningBackendInfo CheckBackend()
    {
        AppSettings settings =
            SettingsManager.Load();

        string zSignPath =
            settings.ZSignPath;

        if (string.IsNullOrWhiteSpace(
                zSignPath))
        {
            return new SigningBackendInfo
            {
                IsAvailable = false,

                Status =
                    "Signing backend not configured",

                Details =
                    "No zsign executable has been configured."
            };
        }

        if (!File.Exists(zSignPath))
        {
            return new SigningBackendInfo
            {
                IsAvailable = false,

                ExecutablePath =
                    zSignPath,

                Status =
                    "Signing backend not found",

                Details =
                    $"zsign.exe was not found at:\n{zSignPath}"
            };
        }

        try
        {
            string output =
                RunCommand(
                    zSignPath,
                    "-v");

            string version =
                output.Trim();

            return new SigningBackendInfo
            {
                IsAvailable = true,

                ExecutablePath =
                    zSignPath,

                Version =
                    version,

                Status =
                    "Signing backend available",

                Details =
                    string.IsNullOrWhiteSpace(version)
                        ? "zsign is available."
                        : $"zsign {version}"
            };
        }
        catch (Exception ex)
        {
            return new SigningBackendInfo
            {
                IsAvailable = false,

                ExecutablePath =
                    zSignPath,

                Status =
                    "Signing backend failed",

                Details =
                    $"zsign could not be started:\n{ex.Message}"
            };
        }
    }

    private static string RunCommand(
        string executable,
        string arguments)
    {
        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName =
                            executable,

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

        string output =
            process.StandardOutput.ReadToEnd();

        string error =
            process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception(
                string.IsNullOrWhiteSpace(error)
                    ? $"Process exited with code {process.ExitCode}."
                    : error.Trim());
        }

        return output;
    }
}