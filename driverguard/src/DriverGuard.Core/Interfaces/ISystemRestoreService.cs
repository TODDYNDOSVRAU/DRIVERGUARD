using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para interação com a API de Restauração do Sistema do Windows (System Restore).
/// </summary>
public interface ISystemRestoreService
{
    /// <summary>
    /// Verifica se a Restauração do Sistema está ativada na unidade do sistema operacional.
    /// </summary>
    bool IsSystemRestoreEnabled();

    /// <summary>
    /// Solicita a criação de um ponto de restauração antes da instalação de um driver.
    /// Trata limitações de frequência (24h) e status desativado sem falhar em silêncio.
    /// </summary>
    Task<RestorePointResult> CreateRestorePointAsync(string description, bool isSimulation = false);
}
