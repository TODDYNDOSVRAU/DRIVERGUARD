using DriverGuard.Core.Enums;

namespace DriverGuard.Core.Models;

/// <summary>
/// Resultado detalhado da validação criptográfica via WinVerifyTrust e inspeção de certificados.
/// </summary>
public class SignatureVerificationResult
{
    public required bool IsValid { get; init; }
    public required SignatureStatus Status { get; init; }
    public string SignerSubject { get; init; } = string.Empty;
    public string SignerIssuer { get; init; } = string.Empty;
    public string CertificateThumbprint { get; init; } = string.Empty;
    public bool IsWhqlCertified { get; init; }
    public string CatalogFilePath { get; init; } = string.Empty;
    public uint NativeErrorCode { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;

    public static SignatureVerificationResult Valid(string subject, string issuer, string thumbprint, bool isWhql, string catalogPath = "")
    {
        return new SignatureVerificationResult
        {
            IsValid = true,
            Status = isWhql ? SignatureStatus.ValidWhql : SignatureStatus.ValidCatalog,
            SignerSubject = subject,
            SignerIssuer = issuer,
            CertificateThumbprint = thumbprint,
            IsWhqlCertified = isWhql,
            CatalogFilePath = catalogPath,
            NativeErrorCode = 0,
            ErrorMessage = string.Empty
        };
    }

    public static SignatureVerificationResult Invalid(SignatureStatus status, string message, uint errorCode = 0)
    {
        return new SignatureVerificationResult
        {
            IsValid = false,
            Status = status,
            ErrorMessage = message,
            NativeErrorCode = errorCode
        };
    }
}
