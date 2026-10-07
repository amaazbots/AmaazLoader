using System.IO;
using Claunia.PropertyList;

namespace AmaazLoader;

public class SigningInfo
{
    public string AppId { get; set; } = "";
    public string ProfileBundleId { get; set; } = "";
    public string TeamId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime? ExpirationDate { get; set; }

    public List<string> ProvisionedDevices { get; set; } = new();

    public bool IsExpired =>
        ExpirationDate.HasValue &&
        ExpirationDate.Value < DateTime.UtcNow;

    public bool ContainsDevice(string udid)
    {
        return ProvisionedDevices.Any(
            device => string.Equals(
                device.Trim(),
                udid.Trim(),
                StringComparison.OrdinalIgnoreCase));
    }

    public bool MatchesBundleId(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(ProfileBundleId))
            return false;

        if (ProfileBundleId == "*")
            return true;

        if (ProfileBundleId.EndsWith(".*"))
        {
            string prefix =
                ProfileBundleId[..^2];

            return bundleId.StartsWith(
                prefix + ".",
                StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            ProfileBundleId,
            bundleId,
            StringComparison.OrdinalIgnoreCase);
    }
}

public class SigningManager
{
    public SigningInfo? ReadProvisioningProfile(
        string profilePath)
    {
        if (!File.Exists(profilePath))
            return null;

        try
        {
            byte[] data =
                File.ReadAllBytes(profilePath);

            string text =
                System.Text.Encoding.UTF8.GetString(data);

            int plistStart =
                text.IndexOf("<?xml");

            if (plistStart < 0)
            {
                plistStart =
                    text.IndexOf("<plist");
            }

            int plistEnd =
                text.LastIndexOf("</plist>");

            if (plistStart < 0 || plistEnd < 0)
                return null;

            plistEnd += "</plist>".Length;

            string plistXml =
                text.Substring(
                    plistStart,
                    plistEnd - plistStart);

            using var stream =
                new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes(
                        plistXml));

            var plist =
                PropertyListParser.Parse(stream);

            if (plist is not NSDictionary dictionary)
                return null;

            var result =
                new SigningInfo();

            result.Name =
                GetString(
                    dictionary,
                    "Name") ?? "";

            result.AppId =
                GetEntitlement(
                    dictionary,
                    "application-identifier");

            result.TeamId =
                GetEntitlement(
                    dictionary,
                    "com.apple.developer.team-identifier");

            result.ExpirationDate =
                GetDate(
                    dictionary,
                    "ExpirationDate");

            result.ProfileBundleId =
                ExtractBundleId(
                    result.AppId);

            if (dictionary.ContainsKey(
                "ProvisionedDevices"))
            {
                var devices =
                    dictionary["ProvisionedDevices"]
                    as NSArray;

                if (devices != null)
                {
                    foreach (var item in devices)
                    {
                        if (item != null)
                        {
                            result.ProvisionedDevices.Add(
                                item.ToString() ?? "");
                        }
                    }
                }
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractBundleId(
        string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return "";

        int separator =
            appId.IndexOf('.');

        if (separator < 0 ||
            separator >= appId.Length - 1)
        {
            return "";
        }

        return appId[(separator + 1)..];
    }

    private static string GetEntitlement(
        NSDictionary dictionary,
        string key)
    {
        if (!dictionary.ContainsKey(
            "Entitlements"))
            return "";

        var entitlements =
            dictionary["Entitlements"]
            as NSDictionary;

        if (entitlements == null)
            return "";

        return GetString(
            entitlements,
            key) ?? "";
    }

    private static string? GetString(
        NSDictionary dictionary,
        string key)
    {
        if (!dictionary.ContainsKey(key))
            return null;

        return dictionary[key]?.ToString();
    }

    private static DateTime? GetDate(
        NSDictionary dictionary,
        string key)
    {
        if (!dictionary.ContainsKey(key))
            return null;

        if (dictionary[key] is NSDate date)
            return date.Date;

        return null;
    }
}