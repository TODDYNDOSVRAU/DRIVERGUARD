using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o serviço de exportação do histórico em formato tabular CSV compatível com Microsoft Excel.
/// </summary>
public interface ICsvExportService
{
    /// <summary>
    /// Exporta os registros de histórico para um arquivo CSV formatado com delimitador e encoding UTF-8 com BOM.
    /// </summary>
    Task ExportToCsvAsync(IEnumerable<OperationHistoryRecord> records, string targetFilePath);
}
