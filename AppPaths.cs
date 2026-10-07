using System.IO;

namespace AmaazLoader;

public static class AppPaths
{
    public static string BaseDirectory =>
        AppContext.BaseDirectory;

    public static string BackendDirectory =>
        Path.Combine(
            BaseDirectory,
            "Backend");

    public static string BackendExecutable =>
        Path.Combine(
            BackendDirectory,
            "amaazloader-signing.exe");

    public static string ToolsDirectory =>
        Path.Combine(
            BaseDirectory,
            "Tools");

    public static string ZSignDirectory =>
        Path.Combine(
            ToolsDirectory,
            "zsign");

    public static string ZSignExecutable =>
        Path.Combine(
            ZSignDirectory,
            "zsign.exe");

    public static string LibimobiledeviceDirectory =>
        Path.Combine(
            ToolsDirectory,
            "libimobiledevice");
}