namespace DriverGuard.Core.Enums;

/// <summary>
/// Mapeamento oficial dos códigos de erro do Configuration Manager (PnP / Gerenciador de Dispositivos do Windows).
/// </summary>
public static class ConfigManagerErrorCode
{
    public static string GetDescription(uint errorCode)
    {
        return errorCode switch
        {
            0 => "Este dispositivo está funcionando corretamente.",
            1 => "Código 1: Este dispositivo não está configurado corretamente.",
            2 => "Código 2: O Windows não pode carregar o driver para este dispositivo.",
            3 => "Código 3: O driver deste dispositivo pode estar corrompido ou o sistema está sem memória.",
            9 => "Código 9: O Windows não pode identificar este hardware porque o BIOS/firmware reportou ID inválido.",
            10 => "Código 10: Este dispositivo não pode ser iniciado. Um driver de dispositivo mais recente pode resolver o problema.",
            12 => "Código 12: Este dispositivo não encontrou recursos livres suficientes para usar.",
            14 => "Código 14: Este dispositivo não funcionará corretamente até que o computador seja reiniciado.",
            16 => "Código 16: O Windows não pode identificar todos os recursos que este dispositivo usa.",
            18 => "Código 18: Reinstale os drivers para este dispositivo.",
            19 => "Código 19: O Windows não pode iniciar este hardware porque suas informações de configuração no Registro estão incompletas ou danificadas.",
            21 => "Código 21: O Windows está removendo este dispositivo.",
            22 => "Código 22: Este dispositivo foi desabilitado pelo usuário ou pelo sistema.",
            24 => "Código 24: Este dispositivo não está presente, não está funcionando corretamente ou não tem todos os drivers instalados.",
            28 => "Código 28: Os drivers deste dispositivo não estão instalados.",
            29 => "Código 29: Este dispositivo foi desabilitado porque o firmware do dispositivo não forneceu os recursos necessários.",
            31 => "Código 31: Este dispositivo não está funcionando corretamente porque o Windows não pode carregar os drivers necessários.",
            32 => "Código 32: Um driver para este dispositivo foi desabilitado no Registro do Windows.",
            34 => "Código 34: O Windows não pode determinar as configurações para este dispositivo.",
            35 => "Código 35: O firmware/BIOS do computador não inclui informações suficientes para configurar e usar este dispositivo.",
            36 => "Código 36: Este dispositivo está solicitando uma interrupção PCI, mas está configurado para uma interrupção ISA.",
            37 => "Código 37: O Windows não pode inicializar o driver para este hardware.",
            38 => "Código 38: O Windows não pode carregar o driver porque uma instância anterior ainda está na memória.",
            39 => "Código 39: O Windows não pode carregar o driver deste dispositivo (arquivo ausente ou corrompido).",
            40 => "Código 40: O Windows não pode acessar este hardware porque as informações de serviço no Registro estão ausentes.",
            41 => "Código 41: O Windows carregou com êxito o driver, mas não encontrou o dispositivo de hardware.",
            42 => "Código 42: O Windows não pode carregar o driver porque já há um dispositivo duplicado em execução.",
            43 => "Código 43: O Windows interrompeu este dispositivo porque encontrou problemas no hardware ou no driver.",
            44 => "Código 44: Um aplicativo ou serviço desligou este dispositivo de hardware.",
            45 => "Código 45: No momento, este dispositivo de hardware não está conectado ao computador.",
            46 => "Código 46: O Windows não pode obter acesso a este dispositivo porque o sistema operacional está em processo de desligamento.",
            47 => "Código 47: O Windows não pode usar este dispositivo porque foi preparado para remoção segura.",
            48 => "Código 48: O software para este dispositivo foi impedido de iniciar porque tem problemas conhecidos com o Windows.",
            49 => "Código 49: O Windows não pode iniciar novos dispositivos de hardware porque a seção de sistema do Registro está cheia.",
            52 => "Código 52: O Windows não pode verificar a assinatura digital dos drivers necessários para este dispositivo.",
            _ => $"Código {errorCode}: Erro desconhecido reportado pelo Gerenciador de Dispositivos."
        };
    }
}
