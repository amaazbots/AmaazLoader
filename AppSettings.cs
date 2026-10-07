using System;
using System.IO;
using System.Text.Json;

namespace AmaazLoader;

public class AppSettings
{
    public string ZSignPath { get; set; } =
        AppPaths.ZSignExecutable;

    public string LibimobiledevicePath { get; set; } =
        AppPaths.LibimobiledeviceDirectory;

    public string OutputFolder { get; set; } =
        "";

    public bool AutoDetectDevice { get; set; } =
        true;

    public bool AutoClean { get; set; } =
        false;
}

public static class SettingsManager
{
    private static readonly string SettingsDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "AmaazLoader");

    private static readonly string SettingsFile =
        Path.Combine(
            SettingsDirectory,
            "settings.json");

    public static AppSettings Load()
    {
        try
        {
            AppSettings settings;

            if (!File.Exists(
                SettingsFile))
            {
                settings =
                    new AppSettings();

                NormalizeSettings(
                    settings);

                return settings;
            }

            string json =
                File.ReadAllText(
                    SettingsFile);

            settings =
                JsonSerializer.Deserialize<AppSettings>(
                    json)
                ?? new AppSettings();

            NormalizeSettings(
                settings);

            return settings;
        }
        catch
        {
            var settings =
                new AppSettings();

            NormalizeSettings(
                settings);

            return settings;
        }
    }

    public static bool Save(
        AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(
                SettingsDirectory);

            NormalizeSettings(
                settings);

            var options =
                new JsonSerializerOptions
                {
                    WriteIndented =
                        true
                };

            string json =
                JsonSerializer.Serialize(
                    settings,
                    options);

            File.WriteAllText(
                SettingsFile,
                json);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void NormalizeSettings(
        AppSettings settings)
    {
        /*
         * If the configured bundled tool location no longer
         * exists, use the tools beside the current
         * AmaazLoader executable.
         *
         * This also allows a portable AmaazLoader folder
         * to be moved to another location.
         */

        if (string.IsNullOrWhiteSpace(
                settings.ZSignPath) ||
            !File.Exists(
                settings.ZSignPath))
        {
            settings.ZSignPath =
                AppPaths.ZSignExecutable;
        }

        if (string.IsNullOrWhiteSpace(
                settings.LibimobiledevicePath) ||
            !Directory.Exists(
                settings.LibimobiledevicePath))
        {
            settings.LibimobiledevicePath =
                AppPaths.LibimobiledeviceDirectory;
        }

        settings.OutputFolder ??=
            "";
    }

    public static string GetSettingsFilePath()
    {
        return SettingsFile;
    }
}