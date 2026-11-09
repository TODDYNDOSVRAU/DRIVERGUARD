using System.Runtime.InteropServices;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using Microsoft.Win32;
using Serilog;

namespace DriverGuard.Infrastructure.SystemServices;

/// <summary>
/// Serviço de integração com a API de Restauração do Sistema do Windows via srclient.dll.
/// </summary>
public class SystemRestoreService : ISystemRestoreService
{
    private readonly ILogger _logger = Log.ForContext<SystemRestoreService>();

    private const uint BEGIN_NESTED_SYSTEM_CHANGE = 102;
    private const uint APPLICATION_INSTALL = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RESTOREPOINTINFOW
    {
        public uint dwEventType;
        public uint dwRestorePtType;
        public long llSequenceNumber;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STATEMGRSTATUS
    {
        public uint nStatus;
        public long llSequenceNumber;
    }

    [DllImport("srclient.dll", CharSet = CharSet.Unicode)]
    private static extern int SRSetRestorePointW(ref RESTOREPOINTINFOW pRestorePointInit, out STATEMGRSTATUS pSMgrStatus);

    public bool IsSystemRestoreEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore");
            if (key != null)
            {
                var val = key.GetValue("DisableSR");
                if (val is int intVal && intVal == 1)
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Não foi possível verificar status de DisableSR no Registro.");
            return false;
        }
    }

    public async Task<RestorePointResult> CreateRestorePointAsync(string description, bool isSimulation = false)
    {
        if (isSimulation)
        {
            _logger.Information("[MODO SIMULAÇÃO] Ponto de restauração simulado: {Desc}", description);
            return RestorePointResult.Simulated();
        }

        return await Task.Run(() =>
        {
            if (!IsSystemRestoreEnabled())
            {
                _logger.Warning("A Restauração do Sistema está desativada no registro (DisableSR = 1).");
                return RestorePointResult.ThrottledOrDisabled(
                    "A proteção do sistema está desativada no Windows para esta unidade. O DriverGuard prosseguirá garantindo a reversão via backup do DriverStore.");
            }

            try
            {
                var rpInfo = new RESTOREPOINTINFOW
                {
                    dwEventType = BEGIN_NESTED_SYSTEM_CHANGE,
                    dwRestorePtType = APPLICATION_INSTALL,
                    llSequenceNumber = 0,
                    szDescription = $"DriverGuard: {description}"
                };

                int result = SRSetRestorePointW(ref rpInfo, out var status);

                if (result == 0 && status.nStatus == 0)
                {
                    _logger.Information("Ponto de restauração criado com sucesso. Sequence: {Seq}", status.llSequenceNumber);
                    return RestorePointResult.Created(rpInfo.szDescription);
                }

                _logger.Warning("SRSetRestorePointW retornou status 0x{Status:X8}. Possível limitação de frequência (24h) do Windows.", status.nStatus);
                return RestorePointResult.ThrottledOrDisabled(
                    "O Windows limitou a frequência de pontos de restauração (regra de 24 horas) ou o serviço está temporariamente indisponível. A segurança de recuperação está garantida pela exportação do DriverStore.");
            }
            catch (DllNotFoundException)
            {
                _logger.Warning("srclient.dll não encontrada neste ambiente.");
                return RestorePointResult.ThrottledOrDisabled("Módulo de restauração do sistema (srclient.dll) ausente.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Erro inesperado ao criar ponto de restauração.");
                return RestorePointResult.Failed(0xFFFFFFFF, ex.Message);
            }
        });
    }
}
