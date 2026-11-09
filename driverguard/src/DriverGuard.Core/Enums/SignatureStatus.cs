namespace DriverGuard.Core.Enums;

/// <summary>
/// Status da verificação criptográfica da assinatura digital do driver.
/// </summary>
public enum SignatureStatus
{
    /// <summary>
    /// Assinatura válida com certificação oficial Microsoft WHQL (Windows Hardware Quality Labs).
    /// </summary>
    ValidWhql,

    /// <summary>
    /// Assinado digitalmente com catálogo válido emitido por autoridade confiável, mas sem tag WHQL explícita.
    /// </summary>
    ValidCatalog,

    /// <summary>
    /// Pacote não assinado (ausência de catálogo ou assinatura incorporada). Rejeitado imediatamente.
    /// </summary>
    Unsigned,

    /// <summary>
    /// Assinatura presente, porém inválida, corrompida ou expirada/revogada.
    /// </summary>
    InvalidSignature,

    /// <summary>
    /// Assinado por um publisher que não consta na allowlist de autoridades/fabricantes confiáveis.
    /// </summary>
    UntrustedPublisher
}
