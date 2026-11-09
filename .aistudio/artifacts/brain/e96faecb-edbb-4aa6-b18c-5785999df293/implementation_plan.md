# DriverGuard — Arquitetura e Plano de Implementação (Etapa 0)

O **DriverGuard** é uma aplicação desktop nativa para Windows 10 (22H2) e Windows 11 (x64) desenvolvida em C# / .NET 8 e WPF com `Wpf.Ui` (Fluent Design). Seu objetivo é auditar o inventário Plug and Play (PnP), verificar atualizações legítimas de drivers, validar assinaturas digitais rigorosamente (WinVerifyTrust / WHQL) e executar instalações e reversões seguras através de `pnputil`, sempre sob demanda 100% manual e sem telemetria ou ações em segundo plano.

---

## Decisões Confirmadas e Alinhamentos Críticos

> [!IMPORTANT]
> **Decisões confirmadas com o usuário na Etapa 0:**
> 1. **Estratégia de Fontes de Atualização:** Desacoplar da dependência mandatória do Windows Update. O Windows Update Agent (WUA API) será acionado sob demanda como fonte complementar/necessária; suporte prioritário a pacotes verificados, repositórios de drivers locais/pasta e catálogos estruturados com allowlist estrita.
> 2. **Tratamento da Restauração do Sistema (System Restore):** Quando desativada ou com throttle (limitação de 24h do Windows), o DriverGuard **avisa com destaque** no diálogo de confirmação, mas **permite prosseguir** utilizando a exportação segura do DriverStore como garantia de reversão.
> 3. **Interface e Estilo Visual:** Adoção da biblioteca **WPF-UI** (`Wpf.Ui`), garantindo conformidade nativa com Fluent Design System do Windows 11/10, suporte completo a temas claro/escuro dinâmicos, tipografia Segoe UI Variable e controles acessíveis.

---

## 1. Resumo do Entendimento do Projeto

O DriverGuard atende a uma lacuna crítica no ecossistema Windows: ferramentas tradicionais de terceiros para atualização de drivers frequentemente instalam pacotes genéricos ou desnecessários e operam como caixas-pretas. O DriverGuard atua como uma ferramenta profissional de engenharia de sistemas:
- **Transparência Absoluta:** O usuário vê o Hardware ID completo, versão e data atual vs. nova, fornecedor, classe de dispositivo e código de erro PnP (ex.: códigos 10, 28, 43 explicados em português).
- **Segurança Criptográfica:** Nenhum driver é instalado sem assinatura digital válida comprovada via API nativa `WinVerifyTrust` (wintrust.dll) contra o catálogo WHQL (.cat) ou binário assinado (.sys / .dll).
- **Sem Falsos Positivos:** Se não houver fonte oficial ou pacote com assinatura válida, o status permanece *"Sem atualização verificada"*. Nunca infere nem adivinha versões.
- **Reversibilidade Garantida:** Antes de tocar em qualquer dispositivo, o driver ativo é exportado via `pnputil /export-driver` para um repositório isolado em `%ProgramData%\DriverGuard\Backups`. Se a instalação falhar ou o dispositivo entrar em estado de erro, a reversão automática é disparada imediatamente.
- **Bloqueio de Risco:** Firmware/BIOS é estritamente bloqueado (somente aviso informativo). Dispositivos de GPU, Chipset e Rede recebem classificação de **Risco Alto**, exigindo confirmação explícita adicional.

---

## 2. Riscos Técnicos, Inviabilidades e Alternativas

### Risco 1: Localizar a Fonte Oficial pelo Hardware ID
* **O Problema:** A Microsoft não oferece uma API REST pública documentada para o Microsoft Update Catalog (`catalog.update.microsoft.com`). A pesquisa web direta exige parsing de HTML/ASP.NET instável (ViewState, scripts dinâmicos de download). Fornecedores de chips (Realtek, AMD, NVIDIA, Intel) geralmente distribuem instaladores executáveis de centenas de megabytes (`setup.exe`) sem padronização de metadados para busca direta via API.
* **Inviabilidade/Fragilidade:** Raspagem de HTML do Update Catalog quebra a cada atualização visual da Microsoft. Executáveis proprietários (.exe) não são drivers limpos instaláveis via `pnputil`.
* **Alternativa Técnica Adotada:**
  1. Utilizar a **WUA API** (`IUpdateSearcher`) com critério `Type='Driver' and IsInstalled=0` apenas quando expressamente solicitado pelo usuário, pois ela é a única interface oficial da Microsoft que faz o matching exato entre Hardware ID e pacotes WHQL homologados.
  2. Implementar o modo robusto **"Instalar de Pasta" (Funcionalidade 5)**: o usuário aponta um diretório contendo o driver oficial (INF/CAT/SYS). O DriverGuard valida se os Hardware IDs do `.inf` são compatíveis com o dispositivo selecionado e checa a assinatura digital do `.cat` via `WinVerifyTrust` antes de permitir qualquer ação.
  3. Para catálogos OEM (Dell, Lenovo, HP), manter uma **Allowlist em JSON** restrita a domínios HTTPS oficiais (ex.: `*.dell.com`, `*.lenovo.com`, `*.hp.com`, `*.microsoft.com`) para conferência de hash SHA-256 e proveniência.

### Risco 2: Reversão sem Backup Completo do Sistema (Full Image)
* **O Problema:** Se um driver de dispositivo crítico (ex.: placa de vídeo ou controladora de armazenamento) causar travamento do kernel (BSOD), a interface do usuário deixa de responder e a reversão programada em runtime não tem chance de ser executada.
* **Fragilidade de Pnputil em Falha Grave:** `pnputil` opera no espaço de usuário do Windows ativo. Se a máquina reiniciar em loop de BSOD, `pnputil` não pode ser acionado de dentro do app.
* **Alternativa Técnica Adotada:**
  1. **Exportação Atômica do DriverStore:** Antes da instalação, o driver atual é exportado via `pnputil /export-driver <oemXX.inf> <TargetDir>`.
  2. **Monitoramento Pós-Instalação:** Imediatamente após o `pnputil /install-driver`, o app aguarda o evento PnP de reinstanciação do dispositivo e consulta `ConfigManagerErrorCode` via CIM. Se o código for diferente de `0` (ex.: Erro 43, 10), o app executa imediatamente a reversão reinstalando o pacote exportado via `pnputil /add-driver <backup.inf> /install`.
  3. **Plano de Contingência Offline:** O app gera um script de recuperação (`rollback_emergency.cmd`) no diretório de backup com os comandos `pnputil /add-driver` para que o usuário possa reverter via prompt do Ambiente de Recuperação do Windows (WinRE) ou Modo de Segurança em caso de BSOD.

### Risco 3: Ponto de Restauração do Sistema (System Restore)
* **O Problema:** O serviço de Restauração do Sistema (`SystemRestore`) frequentemente vem desativado por padrão no Windows 10 e 11 em discos SSD/NVMe. Além disso, existe o throttling do Windows: o registro `SystemRestorePointCreationFrequency` limita a criação a no máximo 1 ponto a cada 24 horas.
* **Fragilidade de Falha Silenciosa:** Invocar a API `SRSetRestorePoint` (srclient.dll) pode falhar com códigos como `ERROR_SERVICE_DISABLED` (1058) ou retornar sucesso falso devido ao limite de frequência.
* **Alternativa Técnica Adotada:**
  1. Verificar via P/Invoke e WMI (`root\default:SystemRestore`) se a restauração está habilitada na unidade de sistema (`C:`).
  2. Chamar `SRSetRestorePointW` especificando `APPLICATION_INSTALL` e `BEGIN_NESTED_SYSTEM_CHANGE`.
  3. Se falhar ou estiver desativada: **nunca falhar em silêncio**. O app exibe aviso claro no diálogo de confirmação: *"A Restauração do Sistema do Windows está desativada ou limitada. A garantia de recuperação utilizará o backup interno do DriverStore. Deseja continuar?"*.

### Risco 4: Uso da WUA API em .NET (COM Interop)
* **O Problema:** `WUApiLib` (`wuapi.dll`) é uma API COM antiga de 32/64 bits. O método `IUpdateSearcher.Search()` é bloqueante, demorado (pode levar até 90 segundos) e dependente do serviço `wuauserv`. Se o serviço estiver parado ou desabilitado (comum em ambientes corporativos ou otimizados), a chamada estoura HRESULT `0x80070422` ou `0x80240024`.
* **Alternativa Técnica Adotada:**
  1. Isolar a WUA API em uma camada assíncrona desacoplada (`Task.Run` com `CancellationToken`), permitindo cancelar a busca a qualquer momento pela UI sem congelar o Dispatcher do WPF.
  2. Implementar detecção do estado do serviço `wuauserv` antes de invocar a API COM e tratar explicitamente códigos COM HRESULT com mensagens claras em português.

---

## 3. Arquitetura da Solução e Estrutura de Pastas

A solução segue o padrão de **Clean Architecture / Arquitetura Hexagonal** para aplicações desktop .NET, com injeção de dependência (`Microsoft.Extensions.DependencyInjection`), separação estrita de responsabilidades e total isolamento das APIs nativas (PnP, COM, P/Invoke) atrás de interfaces testáveis com mocks.

```
DriverGuard/
├── DriverGuard.sln
├── src/
│   ├── DriverGuard.Core/                        # Domínio puro e regras de negócio
│   │   ├── Models/
│   │   │   ├── PnpDevice.cs                     # Dados do dispositivo PnP (HardwareID, driver ativo, erro)
│   │   │   ├── DriverPackage.cs                 # Pacote de driver (INF, CAT, versão, data, provider)
│   │   │   ├── UpdateCandidate.cs               # Proposta de atualização comparada com a versão atual
│   │   │   ├── VerificationResult.cs            # Resultado de assinatura, hash e procedência
│   │   │   ├── OperationHistoryRecord.cs        # Registro de log e auditoria para o SQLite
│   │   │   └── RestorePointResult.cs            # Status da criação do ponto de restauração
│   │   ├── Enums/
│   │   │   ├── DeviceStatus.cs                  # UpToDate, UpdateAvailable, Error, NotVerified, Ignored
│   │   │   ├── RiskLevel.cs                     # Low, Moderate, High (GPU, Chipset, Rede)
│   │   │   ├── SignatureStatus.cs               # ValidWhql, ValidCatalog, Untrusted, Unsigned
│   │   │   └── OperationType.cs                 # Inventory, Verify, Install, Rollback, ManualFolderInstall
│   │   └── Interfaces/
│   │       ├── IDeviceInventoryService.cs       # Enumeração e monitoramento de dispositivos CIM/WMI
│   │       ├── IDriverSourceService.cs          # Contrato para consulta de atualizações
│   │       ├── ISignatureVerificationService.cs # WinVerifyTrust e validação de catálogo
│   │       ├── ISystemRestoreService.cs         # P/Invoke srclient.dll
│   │       ├── IDriverBackupService.cs          # Exportação e gestão do DriverStore
│   │       ├── IDriverInstallerService.cs       # Execução do pnputil e monitoramento de instalação
│   │       ├── IDriverRollbackService.cs        # Reversão automática e manual
│   │       ├── IHistoryRepository.cs            # Persistência em SQLite
│   │       └── INotificationService.cs          # Toasts do Windows 10/11
│   │
│   ├── DriverGuard.Infrastructure/              # Implementações de SO, P/Invoke, WMI e persistência
│   │   ├── Pnp/
│   │   │   ├── CimDeviceInventoryService.cs     # Microsoft.Management.Infrastructure (Win32_PnPEntity / SignedDriver)
│   │   │   ├── PnpDeviceWatcher.cs              # CIM Event Watcher (__InstanceOperationEvent)
│   │   │   └── PnpUtilProcessRunner.cs          # Wrapper seguro e assíncrono para pnputil.exe
│   │   ├── Security/
│   │   │   ├── WinVerifyTrustNative.cs          # P/Invoke wintrust.dll (WINTRUST_DATA, GUIDs de catálogo/driver)
│   │   │   ├── Crypt32Native.cs                 # P/Invoke crypt32.dll (certificados, publishers)
│   │   │   └── InfMetadataParser.cs             # Leitura estruturada de arquivos .INF (Hardware IDs, Version, Class)
│   │   ├── WindowsServices/
│   │   │   ├── SystemRestoreService.cs          # Chamada a SRSetRestorePointW com verificação de registro
│   │   │   ├── PendingRebootDetector.cs         # Checagem de CBS RebootRequired e PendingFileRenameOperations
│   │   │   └── DriverStoreBackupManager.cs      # Gestão de pastas de exportação em %ProgramData%
│   │   ├── Sources/
│   │   │   ├── WuaDriverSourceService.cs        # Integração COM com WUApiLib (IUpdateSearcher)
│   │   │   ├── FolderDriverSourceService.cs     # Validador para "Instalar de Pasta"
│   │   │   └── AllowlistValidator.cs            # Validador de domínios permitidos via JSON
│   │   └── Persistence/
│   │       ├── SqliteHistoryRepository.cs       # SQLite (Dapper / Microsoft.Data.Sqlite)
│   │       └── CsvExportService.cs              # Exportação tabular via CsvHelper
│   │
│   └── DriverGuard.UI/                          # Aplicação WPF (.NET 8, x64)
│       ├── App.xaml / App.xaml.cs               # Host de injeção de dependência, Serilog, temas Wpf.Ui
│       ├── app.manifest                         # requireAdministrator, DPI-aware (PerMonitorV2), Windows 10/11
│       ├── ViewModels/
│       │   ├── MainViewModel.cs                 # Navegação de abas, estado global, cancelamento
│       │   ├── DashboardViewModel.cs            # Lista de dispositivos, filtros, busca, ações
│       │   ├── ProblemsViewModel.cs             # Aba "Problemas" (dispositivos com erro PnP traduzidos)
│       │   ├── InstallConfirmViewModel.cs       # Diálogo modal de riscos, ponto de restauração e confirmação
│       │   ├── FolderInstallViewModel.cs        # Modal "Instalar de Pasta"
│       │   └── HistoryViewModel.cs              # Histórico com filtros e botão exportar CSV
│       ├── Views/
│       │   ├── MainWindow.xaml                  # Janela principal Fluent com NavigationView
│       │   ├── DashboardView.xaml
│       │   ├── ProblemsView.xaml
│       │   ├── HistoryView.xaml
│       │   └── Dialogs/
│       │       ├── InstallConfirmDialog.xaml
│       │       └── FolderInstallDialog.xaml
│       └── Assets/ & Config/
│           └── allowlist.json                   # Domínios e publishers oficiais permitidos
│
└── tests/
    └── DriverGuard.Tests.Unit/                  # Testes automatizados xUnit
        ├── VersionComparisonTests.cs            # Comparação estrita de DriverVersion e DriverDate
        ├── SignatureValidationTests.cs          # Testes de certificados e rejeição de binários não assinados
        ├── RiskAssessmentTests.cs               # Classificação correta de GPU/Chipset/Rede como Alto Risco
        └── RollbackOrchestrationTests.cs        # Acionamento de reversão em caso de erro simulado
```

---

## 4. Diagrama de Fluxo e Arquitetura

```
┌────────────────────────────────────────────────────────────────────────┐
│                        DRIVERGUARD UI (WPF)                           │
│  [Dashboard]      [Aba Problemas]      [Histórico]      [De Pasta]     │
│       │                  │                  │                │         │
│  (MVVM Commands com CancellationToken - Interface 100% Responsiva)     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        DRIVERGUARD.CORE                                │
│  - Regras de Risco (GPU/Rede/Chipset = Alto Risco)                    │
│  - Comparação Estrita (Data/Versão/HardwareID)                         │
│  - Orquestrador de Instalação e Reversão Atômica                       │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
        ┌───────────────────────────┼───────────────────────────┐
        ▼                           ▼                           ▼
┌─────────────────┐       ┌───────────────────┐       ┌───────────────────┐
│ INVENTÁRIO CIM  │       │ SEGURANÇA & ASSIN.│       │ PNPUTIL & RESTORE │
│ Win32_PnPEntity │       │  WinVerifyTrust   │       │  P/Invoke srclient│
│   Win32_PnP-    │       │   (wintrust.dll)  │       │  pnputil /export  │
│  SignedDriver   │       │ Validação de .CAT │       │  pnputil /install │
└─────────────────┘       └───────────────────┘       └───────────────────┘
```

---

## 5. Próximos Passos (Divisão por Fases)

- **Fase 1 (Próxima):**
  - Configuração do projeto base (.NET 8 WPF, `app.manifest` com `requireAdministrator`, injeção de dependência e tema claro/escuro via `Wpf.Ui`).
  - Implementação do Inventário PnP via CIM (`Microsoft.Management.Infrastructure`): listagem completa, extração de Hardware IDs, versões, datas, classes e códigos de erro de ConfigManager com descrições em português.
  - Dashboard somente leitura: busca instantânea, filtros por classe e status, aba de "Problemas" e responsividade garantida sem travar a interface.
- **Fase 2:** Verificação de fontes + validação criptográfica de assinatura (WinVerifyTrust) + modo "Instalar de Pasta".
- **Fase 3:** Ponto de restauração + instalação controlada via `pnputil` + reversão automática e manual + modo simulação.
- **Fase 4:** Histórico em SQLite + exportação CSV via CsvHelper + notificações Toast do Windows.
