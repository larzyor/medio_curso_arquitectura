using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ABC.Billing.Infrastructure.Cryptography;

/// <summary>Certificado autofirmado SOLO para desarrollo y pruebas (en producción: el de una entidad certificadora acreditada).</summary>
public static class CertificadoDePrueba
{
    public static X509Certificate2 Crear(string ruc)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN=ABC Software Corp (PRUEBA), OU={ruc}, O=ABC Software Corp, C=PE", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx, "prueba"), "prueba", X509KeyStorageFlags.Exportable);
    }
}
