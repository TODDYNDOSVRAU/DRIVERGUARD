using Dapper;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace DriverGuard.Infrastructure.Persistence;

/// <summary>
/// Implementação do repositório de histórico e auditoria utilizando SQLite e Dapper.
/// </summary>
public class SqliteHistoryRepository : IHistoryRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger = Log.ForContext<SqliteHistoryRepository>();

    public SqliteHistoryRepository()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string dbDir = Path.Combine(appData, "DriverGuard");
        Directory.CreateDirectory(dbDir);
        string dbPath = Path.Combine(dbDir, "driverguard.db");

        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS OperationHistory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    DeviceName TEXT NOT NULL,
                    HardwareId TEXT NOT NULL,
                    OperationType TEXT NOT NULL,
                    PreviousVersion TEXT NOT NULL,
                    NewVersion TEXT NOT NULL,
                    Source TEXT NOT NULL,
                    ResultStatus TEXT NOT NULL,
                    ErrorMessage TEXT,
                    IsSimulation INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_history_timestamp ON OperationHistory (Timestamp);
                CREATE INDEX IF NOT EXISTS idx_history_device ON OperationHistory (DeviceName);
            ";

            connection.Execute(createTableSql);
            _logger.Information("Banco de dados SQLite inicializado em: {Conn}", _connectionString);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao inicializar schema do banco SQLite.");
        }
    }

    public async Task AddRecordAsync(OperationHistoryRecord record)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string insertSql = @"
                INSERT INTO OperationHistory (
                    Timestamp, DeviceName, HardwareId, OperationType, PreviousVersion,
                    NewVersion, Source, ResultStatus, ErrorMessage, IsSimulation
                ) VALUES (
                    @Timestamp, @DeviceName, @HardwareId, @OperationType, @PreviousVersion,
                    @NewVersion, @Source, @ResultStatus, @ErrorMessage, @IsSimulation
                );
            ";

            await connection.ExecuteAsync(insertSql, new
            {
                Timestamp = record.Timestamp.ToString("o"),
                record.DeviceName,
                record.HardwareId,
                record.OperationType,
                record.PreviousVersion,
                record.NewVersion,
                record.Source,
                record.ResultStatus,
                record.ErrorMessage,
                IsSimulation = record.IsSimulation ? 1 : 0
            });

            _logger.Information("Registro de histórico adicionado: [{Op}] {Device} ({Status})", record.OperationType, record.DeviceName, record.ResultStatus);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Erro ao gravar registro de histórico no SQLite.");
        }
    }

    public async Task<IReadOnlyList<OperationHistoryRecord>> GetAllRecordsAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT Id, Timestamp, DeviceName, HardwareId, OperationType,
                       PreviousVersion, NewVersion, Source, ResultStatus, ErrorMessage, IsSimulation
                FROM OperationHistory
                ORDER BY Timestamp DESC;
            ";

            var rows = await connection.QueryAsync<dynamic>(selectSql);
            var list = new List<OperationHistoryRecord>();

            foreach (var r in rows)
            {
                list.Add(new OperationHistoryRecord
                {
                    Id = (int)r.Id,
                    Timestamp = DateTime.Parse((string)r.Timestamp),
                    DeviceName = (string)r.DeviceName,
                    HardwareId = (string)r.HardwareId,
                    OperationType = (string)r.OperationType,
                    PreviousVersion = (string)r.PreviousVersion,
                    NewVersion = (string)r.NewVersion,
                    Source = (string)r.Source,
                    ResultStatus = (string)r.ResultStatus,
                    ErrorMessage = r.ErrorMessage != null ? (string)r.ErrorMessage : string.Empty,
                    IsSimulation = ((long)r.IsSimulation) == 1
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao consultar histórico no SQLite.");
            return Array.Empty<OperationHistoryRecord>();
        }
    }

    public async Task<IReadOnlyList<OperationHistoryRecord>> GetFilteredRecordsAsync(
        string? searchQuery = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? resultStatus = null)
    {
        var all = await GetAllRecordsAsync();
        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string q = searchQuery.Trim();
            query = query.Where(r =>
                r.DeviceName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.HardwareId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.OperationType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Source.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (startDate.HasValue)
        {
            query = query.Where(r => r.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.Timestamp <= endDate.Value.AddDays(1).AddTicks(-1));
        }

        if (!string.IsNullOrWhiteSpace(resultStatus) && resultStatus != "Todos")
        {
            query = query.Where(r => string.Equals(r.ResultStatus, resultStatus, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }
}
