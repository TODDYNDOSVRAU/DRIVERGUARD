using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o orquestrador de verificação de atualizações oficiais.
/// </summary>
public interface IUpdateCheckService
{
    /// <summary>
    /// Verifica se há atualizações oficiais para o dispositivo informado.
    /// Retorna UpdateCandidate com IsNewer=true e assinatura válida se encontrada, ou null se não houver fonte oficial.
    /// </summary>
    Task<UpdateCandidate?> CheckUpdateForDeviceAsync(PnpDevice device, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa a verificação em lote para todos os dispositivos não ignorados, reportando progresso individual.
    /// </summary>
    Task<IReadOnlyDictionary<string, UpdateCandidate>> CheckUpdatesBatchAsync(
        IReadOnlyList<PnpDevice> devices,
        IProgress<(int current, int total, string deviceName, bool updateFound)>? progress = null,
        CancellationToken cancellationToken = default);
}
