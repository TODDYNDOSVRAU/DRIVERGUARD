using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o repositório de histórico persistido em banco SQLite local.
/// </summary>
public interface IHistoryRepository
{
    /// <summary>
    /// Grava uma nova operação no histórico de auditoria.
    /// </summary>
    Task AddRecordAsync(OperationHistoryRecord record);

    /// <summary>
    /// Consulta o histórico completo ordenado da operação mais recente para a mais antiga.
    /// </summary>
    Task<IReadOnlyList<OperationHistoryRecord>> GetAllRecordsAsync();

    /// <summary>
    /// Consulta o histórico aplicando filtros por termo de busca, intervalo de datas e status de resultado.
    /// </summary>
    Task<IReadOnlyList<OperationHistoryRecord>> GetFilteredRecordsAsync(
        string? searchQuery = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? resultStatus = null);
}
