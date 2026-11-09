using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o orquestrador de instalação atômica via pnputil com reversão automática integrada.
/// </summary>
public interface IDriverInstallerService
{
    /// <summary>
    /// Instala um pacote de driver aprovado para o dispositivo, executando ponto de restauração,
    /// backup do driver atual, monitoramento pós-instalação e reversão automática se houver falha.
    /// </summary>
    Task<InstallationResult> InstallDriverAsync(
        UpdateCandidate candidate,
        bool isSimulation = false,
        CancellationToken cancellationToken = default);
}
