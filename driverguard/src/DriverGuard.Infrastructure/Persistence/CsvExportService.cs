using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Serilog;

namespace DriverGuard.Infrastructure.Persistence;

/// <summary>
/// Exporta o histórico de operações em formato CSV compatível com o Microsoft Excel (padrão Brasil: separador ponto-e-vírgula e UTF-8 com BOM).
/// </summary>
public class CsvExportService : ICsvExportService
{
    private readonly ILogger _logger = Log.ForContext<CsvExportService>();

    public async Task ExportToCsvAsync(IEnumerable<OperationHistoryRecord> records, string targetFilePath)
    {
        await Task.Run(() =>
        {
            try
            {
                var directory = Path.GetDirectoryName(targetFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // Configuração personalizada para Excel no Brasil (ponto-e-vírgula como delimitador e UTF-8 com BOM)
                var config = new CsvConfiguration(new CultureInfo("pt-BR"))
                {
                    Delimiter = ";",
                    HasHeaderRecord = true,
                    Encoding = new UTF8Encoding(true) // Adiciona Byte Order Mark (BOM) para o Excel abrir sem distorcer caracteres
                };

                using var writer = new StreamWriter(targetFilePath, false, new UTF8Encoding(true));
                using var csv = new CsvWriter(writer, config);

                // Mapeamento dos registros
                var exportDtos = records.Select(r => new
                {
                    DataHora = r.FormattedTimestamp,
                    Dispositivo = r.DeviceName,
                    HardwareID = r.HardwareId,
                    Operacao = r.OperationType,
                    VersaoAnterior = r.PreviousVersion,
                    VersaoNova = r.NewVersion,
                    Fonte = r.Source,
                    Resultado = r.ResultStatus,
                    Simulado = r.IsSimulation ? "Sim" : "Não",
                    DetalhesErro = r.ErrorMessage
                });

                csv.WriteRecords(exportDtos);
                writer.Flush();

                _logger.Information("Histórico exportado com sucesso para CSV em: {Path}", targetFilePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Falha ao exportar histórico para CSV.");
                throw new InvalidOperationException($"Não foi possível exportar para CSV: {ex.Message}", ex);
            }
        });
    }
}
