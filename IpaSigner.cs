using System.Diagnostics;
using System.IO;

namespace AmaazLoader;

public class SigningResult
{
    public bool Success { get; set; }

    public string OutputPath { get; set; } = "";

    public string Output { get; set; } = "";

    public string Error { get; set; } = "";
}

public class IpaSigner
{
    public SigningResult Sign(
        string ipaPath,
        string certificatePath,
        string certificatePassword,
        string provisioningProfilePath,
        string outputPath)
    {
        AppSettings settings =
            SettingsManager.Load();

        string zSignPath =
            settings.ZSignPath;

        if (string.IsNullOrWhiteSpace(
                zSignPath))
        {
            return new SigningResult
            {
                Success = false,

                Error =
                    "The zsign path is not configured."
            };
        }

        if (!File.Exists(zSignPath))
        {
            return new SigningResult
            {
                Success = false,

                Error =
                    "zsign.exe was not found at:\n" +
                    zSignPath
            };
        }

        if (!File.Exists(ipaPath))
        {
            return new SigningResult
            {
                Success = false,

                Error =
                    "The selected IPA file was not found."
            };
        }

        if (!File.Exists(certificatePath))
        {
            return new SigningResult
            {
                Success = false,

                Error =
                    "The signing certificate was not found."
            };
        }

        if (!File.Exists(provisioningProfilePath))
        {
            return new SigningResult
            {
                Success = false,

                Error =
                    "The provisioning profile was not found."
            };
        }

        try
        {
            string arguments =
                BuildArguments(
                    ipaPath,
                    certificatePath,
                    certificatePassword,
                    provisioningProfilePath,
                    outputPath);

            var process =
                new Process
                {
                    StartInfo =
                        new ProcessStartInfo
                        {
                            FileName =
                                zSignPath,

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

            bool outputExists =
                File.Exists(outputPath);

            bool success =
                process.ExitCode == 0 &&
                outputExists;

            return new SigningResult
            {
                Success =
                    success,

                OutputPath =
                    outputPath,

                Output =
                    standardOutput,

                Error =
                    success
                        ? standardError
                        : BuildError(
                            process.ExitCode,
                            standardError,
                            standardOutput)
            };
        }
        catch (Exception ex)
        {
            return new SigningResult
            {
                Success = false,

                OutputPath =
                    outputPath,

                Error =
                    ex.Message
            };
        }
    }

    private static string BuildArguments(
        string ipaPath,
        string certificatePath,
        string password,
        string provisioningProfilePath,
        string outputPath)
    {
        var arguments =
            new List<string>();

        arguments.Add("-f");

        arguments.Add("-k");
        arguments.Add(
            Quote(certificatePath));

        arguments.Add("-p");
        arguments.Add(
            Quote(password));

        arguments.Add("-m");
        arguments.Add(
            Quote(provisioningProfilePath));

        arguments.Add("-o");
        arguments.Add(
            Quote(outputPath));

        arguments.Add("-z");
        arguments.Add("9");

        arguments.Add(
            Quote(ipaPath));

        return string.Join(
            " ",
            arguments);
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
                $"zsign exited with code {exitCode}.";
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