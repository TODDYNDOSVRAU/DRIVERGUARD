using System.Runtime.InteropServices;
using System.Security.Cryptography.Pkcs;
using DriverGuard.Core.Enums;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Serilog;

namespace DriverGuard.Infrastructure.Security;

/// <summary>
/// Serviço de validação criptográfica utilizando a API nativa WinVerifyTrust e leitura do signatário via PKCS#7.
/// </summary>
public class WinVerifyTrustService : ISignatureVerificationService
{
    private readonly ILogger _logger = Log.ForContext<WinVerifyTrustService>();

    public SignatureVerificationResult VerifyFileSignature(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return SignatureVerificationResult.Invalid(
                SignatureStatus.Unsigned,
                $"Arquivo para validação de assinatura não encontrado: {filePath}");
        }

        IntPtr pFileInfo = IntPtr.Zero;
        try
        {
            var fileInfo = new WinVerifyTrustNative.WINTRUST_FILE_INFO
            {
                cbStruct = (uint)Marshal.SizeOf<WinVerifyTrustNative.WINTRUST_FILE_INFO>(),
                pcwszFilePath = filePath,
                hFile = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero
            };

            pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WinVerifyTrustNative.WINTRUST_FILE_INFO>());
            Marshal.StructureToPtr(fileInfo, pFileInfo, false);

            var trustData = new WinVerifyTrustNative.WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WinVerifyTrustNative.WINTRUST_DATA>(),
                pPolicyCallbackData = IntPtr.Zero,
                pSIPClientData = IntPtr.Zero,
                dwUIChoice = WinVerifyTrustNative.WTD_UI_NONE,
                fdwRevocationChecks = WinVerifyTrustNative.WTD_REVOKE_NONE,
                dwUnionChoice = WinVerifyTrustNative.WTD_CHOICE_FILE,
                pFileOrCatalog = pFileInfo,
                dwStateAction = WinVerifyTrustNative.WTD_STATEACTION_IGNORE,
                hWVTStateData = IntPtr.Zero,
                pwszURLReference = null,
                dwProvFlags = WinVerifyTrustNative.WTD_CACHE_ONLY_URL_RETRIEVAL | WinVerifyTrustNative.WTD_REVOCATION_CHECK_NONE,
                dwUIContext = 0
            };

            // Para catálogos de driver a ação correta é DRIVER_ACTION_VERIFY.
            // Não há fallback para a ação Authenticode genérica: ela não valida catálogos de driver
            // e aceitaria arquivos que o Windows rejeitaria.
            var actionId = WinVerifyTrustNative.DRIVER_ACTION_VERIFY;
            int result = WinVerifyTrustNative.WinVerifyTrust(IntPtr.Zero, ref actionId, ref trustData);

            if (result == 0)
            {
                return ExtractSignerInfo(filePath);
            }

            uint errorCode = (uint)result;
            string errorMessage = GetWinVerifyErrorMessage(errorCode);
            _logger.Warning("WinVerifyTrust rejeitou o arquivo {FilePath}. Código: 0x{ErrorCode:X8} - {Message}", filePath, errorCode, errorMessage);

            var status = errorCode == 0x800B0100 ? SignatureStatus.Unsigned : SignatureStatus.InvalidSignature;
            return SignatureVerificationResult.Invalid(status, errorMessage, errorCode);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Erro de execução ao validar assinatura de {FilePath}", filePath);
            return SignatureVerificationResult.Invalid(SignatureStatus.InvalidSignature, $"Falha interna de validação: {ex.Message}");
        }
        finally
        {
            if (pFileInfo != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pFileInfo);
            }
        }
    }

    public SignatureVerificationResult VerifyInfAndCatalog(string infPath, string? catalogPath = null)
    {
        if (!File.Exists(infPath))
        {
            return SignatureVerificationResult.Invalid(SignatureStatus.Unsigned, $"Arquivo INF não encontrado: {infPath}");
        }

        // Se o catálogo não foi passado explicitamente, procura arquivo .cat no mesmo diretório
        string? targetCatalog = catalogPath;
        if (string.IsNullOrWhiteSpace(targetCatalog) || !File.Exists(targetCatalog))
        {
            string dir = Path.GetDirectoryName(infPath) ?? string.Empty;
            string infBaseName = Path.GetFileNameWithoutExtension(infPath);
            string presumedCat = Path.Combine(dir, $"{infBaseName}.cat");

            if (File.Exists(presumedCat))
            {
                targetCatalog = presumedCat;
            }
            else
            {
                // Busca qualquer .cat no diretório
                var allCats = Directory.GetFiles(dir, "*.cat");
                if (allCats.Length > 0)
                    targetCatalog = allCats[0];
            }
        }

        if (string.IsNullOrWhiteSpace(targetCatalog) || !File.Exists(targetCatalog))
        {
            _logger.Warning("Nenhum arquivo de catálogo (.cat) encontrado para o INF {InfPath}. Rejeitando por ausência de assinatura.", infPath);
            return SignatureVerificationResult.Invalid(
                SignatureStatus.Unsigned,
                "Pacote rejeitado: arquivo de catálogo (.cat) assinado ausente.");
        }

        // Valida o catálogo .cat via WinVerifyTrust
        return VerifyFileSignature(targetCatalog);
    }

    /// <summary>
    /// Lê o certificado do signatário a partir do próprio catálogo (PKCS#7).
    /// X509Certificate2 não serve aqui: um .cat não é um certificado.
    /// </summary>
    private SignatureVerificationResult ExtractSignerInfo(string catalogPath)
    {
        try
        {
            var cms = new SignedCms();
            cms.Decode(File.ReadAllBytes(catalogPath));

            var signer = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0].Certificate : null;
            if (signer == null)
            {
                return SignatureVerificationResult.Invalid(
                    SignatureStatus.InvalidSignature,
                    "O catálogo não contém o certificado do signatário.");
            }

            string subject = signer.Subject;
            string issuer = signer.Issuer;
            string thumbprint = signer.Thumbprint;

            bool isWhql = false;
            foreach (var ext in signer.Extensions)
            {
                if (ext.Oid?.Value == WinVerifyTrustNative.OID_WHQL_CRYPTO ||
                    ext.Oid?.Value == WinVerifyTrustNative.OID_WHQL_WINDOWS ||
                    subject.Contains("Windows Hardware Compatibility", StringComparison.OrdinalIgnoreCase) ||
                    issuer.Contains("Microsoft Windows Hardware Compatibility", StringComparison.OrdinalIgnoreCase))
                {
                    isWhql = true;
                    break;
                }
            }

            _logger.Information("Assinatura validada com sucesso para {FilePath}. Signer: {Signer}, WHQL: {IsWhql}", catalogPath, subject, isWhql);
            return SignatureVerificationResult.Valid(subject, issuer, thumbprint, isWhql, catalogPath);
        }
        catch (Exception ex)
        {
            // WinVerifyTrust já aprovou o catálogo; se não conseguimos ler o signatário,
            // reportamos isso como erro em vez de inventar metadados.
            _logger.Error(ex, "Não foi possível ler o signatário do catálogo {Path}", catalogPath);
            return SignatureVerificationResult.Invalid(
                SignatureStatus.InvalidSignature,
                "Não foi possível ler o certificado do signatário do catálogo.");
        }
    }

    private static string GetWinVerifyErrorMessage(uint errorCode)
    {
        return errorCode switch
        {
            0x800B0100 => "TRUST_E_NOSIGNATURE: Nenhuma assinatura foi encontrada no assunto.",
            0x80096004 => "TRUST_E_CERT_SIGNATURE: A assinatura do certificado não pôde ser confirmada ou está corrompida.",
            0x800B0109 => "CERT_E_UNTRUSTEDROOT: A cadeia do certificado foi emitida por uma autoridade de certificação não confiável.",
            0x800B0101 => "CERT_E_EXPIRED: O certificado expirou ou não é válido no momento.",
            0x800B010E => "CERT_E_REVOKED: O certificado foi expressamente revogado pelo emissor.",
            0x80092026 => "CRYPT_E_SECURITY_SETTINGS: As configurações de segurança do sistema impedem a execução deste driver.",
            _ => $"0x{errorCode:X8}: Assinatura digital inválida segundo a política de segurança do Windows."
        };
    }
}
