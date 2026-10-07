using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace AmaazLoader;

public class SigningCertificateInfo
{
    public string Subject { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Thumbprint { get; set; } = "";
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }

    public bool IsExpired =>
        DateTime.UtcNow > NotAfter.ToUniversalTime();
}

public class SigningCertificateManager
{
    public SigningCertificateInfo? ReadCertificate(
        string certificatePath,
        string? password = null)
    {
        if (!File.Exists(certificatePath))
            return null;

        try
        {
            byte[] certificateData =
                File.ReadAllBytes(certificatePath);

            X509Certificate2 certificate;

            if (string.IsNullOrEmpty(password))
            {
                certificate =
                    X509CertificateLoader.LoadCertificate(
                        certificateData);
            }
            else
            {
                certificate =
                    X509CertificateLoader.LoadPkcs12(
                        certificateData,
                        password,
                        X509KeyStorageFlags.Exportable);
            }

            return new SigningCertificateInfo
            {
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                Thumbprint =
                    certificate.Thumbprint ?? "",
                NotBefore = certificate.NotBefore,
                NotAfter = certificate.NotAfter
            };
        }
        catch
        {
            return null;
        }
    }
}