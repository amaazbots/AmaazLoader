using System.Diagnostics;
using System.IO;

namespace AmaazLoader;

public class DeviceInfo
{
    public string Udid { get; set; } = "";
    public string Name { get; set; } = "";
    public string iOSVersion { get; set; } = "";
    public string ProductType { get; set; } = "";
}

public class DeviceManager
{
    public DeviceInfo? GetConnectedDevice()
    {
        AppSettings settings =
            SettingsManager.Load();

        string toolsPath =
            settings.LibimobiledevicePath;

        string ideviceIdPath =
            Path.Combine(
                toolsPath,
                "idevice_id.exe");

        string ideviceInfoPath =
            Path.Combine(
                toolsPath,
                "ideviceinfo.exe");

        if (!File.Exists(ideviceIdPath) ||
            !File.Exists(ideviceInfoPath))
        {
            return null;
        }

        try
        {
            string udidOutput =
                RunCommand(
                    ideviceIdPath,
                    "-l");

            string udid =
                udidOutput
                    .Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(
                        x => x.Trim())
                    .FirstOrDefault()
                ?? "";

            if (string.IsNullOrWhiteSpace(udid))
                return null;

            string name =
                RunCommand(
                    ideviceInfoPath,
                    "-k DeviceName").Trim();

            string iosVersion =
                RunCommand(
                    ideviceInfoPath,
                    "-k ProductVersion").Trim();

            string productType =
                RunCommand(
                    ideviceInfoPath,
                    "-k ProductType").Trim();

            return new DeviceInfo
            {
                Udid = udid,
                Name = name,
                iOSVersion = iosVersion,
                ProductType = productType
            };
        }
        catch
        {
            return null;
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

        process.WaitForExit();

        return output;
    }
}