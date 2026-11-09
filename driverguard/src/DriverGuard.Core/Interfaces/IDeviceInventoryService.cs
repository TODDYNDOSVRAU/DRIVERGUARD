using DriverGuard.Core.Models;

namespace DriverGuard.Core.Interfaces;

/// <summary>
/// Contrato para o serviço de inventário de dispositivos e drivers via CIM/WMI.
/// </summary>
public interface IDeviceInventoryService
{
    /// <summary>
    /// Enumera todos os dispositivos PnP do sistema correlacionando dados de hardware com o driver ativo.
    /// Operação assíncrona com suporte a cancelamento e relatório de progresso.
    /// </summary>
    Task<IReadOnlyList<PnpDevice>> GetDevicesAsync(
        IProgress<(int current, int total, string currentDevice)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta os detalhes atualizados de um único dispositivo pelo seu DeviceID.
    /// </summary>
    Task<PnpDevice?> GetDeviceByIdAsync(string deviceId, CancellationToken cancellationToken = default);
}
