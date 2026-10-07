using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AmaazLoader;

public class SigningEnvironmentInfo
{
    public bool IsWindows { get; set; }
    public bool IsMacOS { get; set; }

    public bool AppleCodeSignAvailable { get; set; }

    public string Status { get; set; } = "";
    public string Details { get; set; } = "";
}

public class SigningEnvironmentManager
{
    public SigningEnvironmentInfo CheckEnvironment()
    {
        bool isWindows =
            RuntimeInformation.IsOSPlatform(
                OSPlatform.Windows);

        bool isMacOS =
            RuntimeInformation.IsOSPlatform(
                OSPlatform.OSX);

        if (isMacOS)
        {
            string codesignPath =
                "/usr/bin/codesign";

            bool available =
                File.Exists(codesignPath);

            return new SigningEnvironmentInfo
            {
                IsWindows = false,
                IsMacOS = true,
                AppleCodeSignAvailable = available,

                Status = available
                    ? "Ready"
                    : "Apple signing tools unavailable",

                Details = available
                    ? "Apple codesign is available."
                    : "The macOS codesign tool could not be found."
            };
        }

        if (isWindows)
        {
            return new SigningEnvironmentInfo
            {
                IsWindows = true,
                IsMacOS = false,
                AppleCodeSignAvailable = false,

                Status = "Windows signing backend required",

                Details =
                    "Apple's native codesign tool is macOS-only. " +
                    "AmaazLoader needs a supported signing backend " +
                    "for Windows."
            };
        }

        return new SigningEnvironmentInfo
        {
            IsWindows = false,
            IsMacOS = false,
            AppleCodeSignAvailable = false,

            Status = "Unsupported platform",

            Details =
                "This operating system is not currently supported."
        };
    }
}