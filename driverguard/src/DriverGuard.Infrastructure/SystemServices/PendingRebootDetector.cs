using DriverGuard.Core.Interfaces;
using Microsoft.Win32;
using Serilog;

namespace DriverGuard.Infrastructure.SystemServices;

/// <summary>
/// Detecta reinicializações pendentes no Windows consultando chaves de registro do CBS, Windows Update e Session Manager.
/// </summary>
public class PendingRebootDetector : IPendingRebootDetector
{
    private readonly ILogger _logger = Log.ForContext<PendingRebootDetector>();

    public bool IsRebootPending(out string pendingReason)
    {
        pendingReason = string.Empty;

        try
        {
            // 1. Component Based Servicing (CBS)
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
            {
                if (key != null)
                {
                    pendingReason = "O Windows possui atualizações de componentes (CBS) que exigem reinicialização.";
                    _logger.Information("Reinicialização pendente detectada via CBS.");
                    return true;
                }
            }

            // 2. Windows Update Auto Update
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"))
            {
                if (key != null)
                {
                    pendingReason = "O Windows Update baixou atualizações do sistema que requerem reinicialização imediata.";
                    _logger.Information("Reinicialização pendente detectada via Windows Update.");
                    return true;
                }
            }

            // 3. PendingFileRenameOperations
            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
            {
                var val = key?.GetValue("PendingFileRenameOperations");
                if (val is string[] array && array.Length > 0)
                {
                    pendingReason = "Existem operações pendentes de renomeação/substituição de arquivos bloqueados pelo kernel.";
                    _logger.Information("Reinicialização pendente detectada via PendingFileRenameOperations.");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Erro ao consultar chaves de registro de reinício pendente. Continuando com cautela.");
        }

        return false;
    }
}
