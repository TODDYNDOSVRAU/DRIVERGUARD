using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o serviço de validação criptográfica de assinaturas digitais via WinVerifyTrust e inspeção de catálogo.
/// </summary>
public interface ISignatureVerificationService
{
    /// <summary>
    /// Valida a assinatura digital de um arquivo de catálogo (.cat) ou binário PE (.sys, .dll).
    /// </summary>
    SignatureVerificationResult VerifyFileSignature(string filePath);

    /// <summary>
    /// Valida o arquivo INF referenciando seu respectivo catálogo (.cat) listado na seção [Version].
    /// </summary>
    SignatureVerificationResult VerifyInfAndCatalog(string infPath, string? catalogPath = null);
}
