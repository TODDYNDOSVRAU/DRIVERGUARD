using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o serviço de inspeção e leitura estruturada de pacotes de drivers em pastas locais (Funcionalidade 5).
/// </summary>
public interface IFolderDriverParserService
{
    /// <summary>
    /// Escaneia um diretório local em busca de arquivos .INF, extrai metadados, catálogo e Hardware IDs compatíveis.
    /// </summary>
    Task<IReadOnlyList<DriverPackage>> ParseFolderDriversAsync(string directoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Avalia se a pasta contém um pacote compatível com o dispositivo informado, checando assinatura e versão.
    /// </summary>
    Task<UpdateCandidate?> EvaluateFolderForDeviceAsync(PnpDevice device, string directoryPath, CancellationToken cancellationToken = default);
}
