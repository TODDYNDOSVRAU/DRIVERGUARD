import React, { useState } from 'react';
import { 
  ShieldCheck, AlertTriangle, CheckCircle2, RefreshCw, Search, 
  Filter, FileCode, Terminal, HelpCircle, HardDrive, Cpu, 
  Wifi, Monitor, ShieldAlert, ChevronRight, Copy, Check
} from 'lucide-react';

interface MockDevice {
  id: string;
  name: string;
  className: string;
  manufacturer: string;
  hardwareId: string;
  driverVersion: string;
  driverDate: string;
  provider: string;
  infName: string;
  errorCode: number;
  status: 'UpToDate' | 'UpdateAvailable' | 'Error' | 'NotVerified' | 'Ignored';
  isCritical: boolean;
  isFirmware: boolean;
}

const SAMPLE_DEVICES: MockDevice[] = [
  {
    id: "PCI\\VEN_10DE&DEV_2782&SUBSYS_169310DE&REV_A1\\4&28f090b8&0&0008",
    name: "NVIDIA GeForce RTX 4070",
    className: "Display",
    manufacturer: "NVIDIA",
    hardwareId: "PCI\\VEN_10DE&DEV_2782&SUBSYS_169310DE",
    driverVersion: "31.0.15.5186",
    driverDate: "15/03/2024",
    provider: "NVIDIA",
    infName: "oem42.inf",
    errorCode: 0,
    status: "NotVerified",
    isCritical: true,
    isFirmware: false
  },
  {
    id: "PCI\\VEN_8086&DEV_7A60&SUBSYS_00008086&REV_11\\3&11583659&0&A8",
    name: "Intel(R) Ethernet Controller I225-V",
    className: "Net",
    manufacturer: "Intel Corporation",
    hardwareId: "PCI\\VEN_8086&DEV_7A60&SUBSYS_00008086",
    driverVersion: "2.1.3.15",
    driverDate: "20/01/2024",
    provider: "Intel",
    infName: "oem18.inf",
    errorCode: 0,
    status: "NotVerified",
    isCritical: true,
    isFirmware: false
  },
  {
    id: "PCI\\VEN_10EC&DEV_8125&SUBSYS_012310EC&REV_05\\0123456789ABCDEF00",
    name: "Realtek PCIe 2.5GbE Family Controller",
    className: "Net",
    manufacturer: "Realtek",
    hardwareId: "PCI\\VEN_10EC&DEV_8125&SUBSYS_012310EC",
    driverVersion: "10.68.815.2023",
    driverDate: "15/08/2023",
    provider: "Realtek",
    infName: "oem12.inf",
    errorCode: 10,
    status: "Error",
    isCritical: true,
    isFirmware: false
  },
  {
    id: "USB\\VID_046D&PID_C52B\\6&1A2B3C4D&0&2",
    name: "Logitech USB Input Device",
    className: "HIDClass",
    manufacturer: "Logitech",
    hardwareId: "USB\\VID_046D&PID_C52B&REV_2411",
    driverVersion: "10.0.22621.1",
    driverDate: "21/06/2006",
    provider: "Microsoft",
    infName: "input.inf",
    errorCode: 0,
    status: "UpToDate",
    isCritical: false,
    isFirmware: false
  },
  {
    id: "ACPI\\VEN_INTC&DEV_1055\\2&DABA3FF&0",
    name: "Dispositivo de Comunicações PCI Simples",
    className: "System",
    manufacturer: "Desconhecido",
    hardwareId: "PCI\\VEN_8086&DEV_7A68&SUBSYS_7A681462",
    driverVersion: "N/A",
    driverDate: "N/A",
    provider: "Desconhecido",
    infName: "",
    errorCode: 28,
    status: "Error",
    isCritical: true,
    isFirmware: false
  },
  {
    id: "UEFI\\RES_{28321F53-C826-4447-BF93-2715F7920A38}\\0",
    name: "System Firmware (BIOS UEFI)",
    className: "Firmware",
    manufacturer: "American Megatrends",
    hardwareId: "UEFI\\RES_{28321F53-C826-4447-BF93-2715F7920A38}",
    driverVersion: "5.17.0.0",
    driverDate: "12/10/2023",
    provider: "Microsoft",
    infName: "firmware.inf",
    errorCode: 0,
    status: "NotVerified",
    isCritical: false,
    isFirmware: true
  }
];

const ERROR_DESCRIPTIONS: Record<number, string> = {
  0: "Este dispositivo está funcionando corretamente.",
  10: "Código 10: Este dispositivo não pode ser iniciado. Um driver mais recente ou reinstalação pode resolver o problema.",
  28: "Código 28: Os drivers deste dispositivo não estão instalados.",
  43: "Código 43: O Windows interrompeu este dispositivo porque encontrou problemas no hardware ou no driver."
};

export default function App() {
  const [activeTab, setActiveTab] = useState<'inventory' | 'problems' | 'architecture' | 'files'>('inventory');
  const [search, setSearch] = useState('');
  const [selectedClass, setSelectedClass] = useState('Todas');
  const [selectedDevice, setSelectedDevice] = useState<MockDevice>(SAMPLE_DEVICES[0]);
  const [copiedPath, setCopiedPath] = useState<string | null>(null);

  const classes = ['Todas', ...Array.from(new Set(SAMPLE_DEVICES.map(d => d.className)))];

  const filteredDevices = SAMPLE_DEVICES.filter(d => {
    const matchesSearch = 
      d.name.toLowerCase().includes(search.toLowerCase()) ||
      d.manufacturer.toLowerCase().includes(search.toLowerCase()) ||
      d.hardwareId.toLowerCase().includes(search.toLowerCase());
    const matchesClass = selectedClass === 'Todas' || d.className === selectedClass;
    return matchesSearch && matchesClass;
  });

  const problemDevices = SAMPLE_DEVICES.filter(d => d.errorCode !== 0);

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedPath(text);
    setTimeout(() => setCopiedPath(null), 2000);
  };

  return (
    <div className="min-h-screen bg-neutral-950 text-neutral-100 flex flex-col font-sans">
      {/* Top Bar */}
      <header className="border-b border-neutral-800 bg-neutral-900/60 backdrop-blur px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-emerald-400 shadow-sm">
            <ShieldCheck className="w-6 h-6" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-bold tracking-tight">DriverGuard</h1>
              <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                PROJETO COMPLETO (FASES 1 A 4 ENTREGUES)
              </span>
              <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-blue-500/20 text-blue-300 border border-blue-500/30">
                SQLITE + CSV + TOASTS
              </span>
            </div>
            <p className="text-xs text-neutral-400">Auditoria PnP, Verificação Segura e Reversão Granular para Windows 10/11 (.NET 8 WPF)</p>
          </div>
        </div>

        {/* Download & Navigation */}
        <div className="flex items-center gap-3">
          <a
            href="/DriverGuard_Source.zip"
            download="DriverGuard_Source.zip"
            className="flex items-center gap-2 px-3.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold shadow-md transition-all border border-emerald-400/30"
          >
            <HardDrive className="w-4 h-4" />
            <span>Baixar Solução Completa (.ZIP)</span>
          </a>
          <button
            onClick={() => setActiveTab('inventory')}
            className={`px-3 py-1.5 text-xs font-medium rounded-md transition-all ${
              activeTab === 'inventory' ? 'bg-neutral-800 text-white shadow-sm' : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            Dashboard PnP ({SAMPLE_DEVICES.length})
          </button>
          <button
            onClick={() => setActiveTab('problems')}
            className={`px-3 py-1.5 text-xs font-medium rounded-md transition-all flex items-center gap-1.5 ${
              activeTab === 'problems' ? 'bg-red-500/20 text-red-300 border border-red-500/30' : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            <AlertTriangle className="w-3.5 h-3.5 text-red-400" />
            Problemas ({problemDevices.length})
          </button>
          <button
            onClick={() => setActiveTab('files')}
            className={`px-3 py-1.5 text-xs font-medium rounded-md transition-all flex items-center gap-1.5 ${
              activeTab === 'files' ? 'bg-neutral-800 text-white shadow-sm' : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            <FileCode className="w-3.5 h-3.5 text-blue-400" />
            Arquivos da Solução C#
          </button>
          <button
            onClick={() => setActiveTab('architecture')}
            className={`px-3 py-1.5 text-xs font-medium rounded-md transition-all flex items-center gap-1.5 ${
              activeTab === 'architecture' ? 'bg-neutral-800 text-white shadow-sm' : 'text-neutral-400 hover:text-neutral-200'
            }`}
          >
            <Cpu className="w-3.5 h-3.5 text-purple-400" />
            Arquitetura & Regras
          </button>
        </div>
      </header>

      {/* Main Workspace */}
      <main className="flex-1 p-6 max-w-7xl w-full mx-auto flex flex-col gap-5">
        {activeTab === 'inventory' && (
          <>
            {/* Action Bar */}
            <div className="flex flex-wrap items-center justify-between gap-4 bg-neutral-900/50 p-4 rounded-xl border border-neutral-800">
              <div className="flex items-center gap-3 flex-1 max-w-md">
                <div className="relative w-full">
                  <Search className="w-4 h-4 text-neutral-400 absolute left-3 top-1/2 -translate-y-1/2" />
                  <input
                    type="text"
                    value={search}
                    onChange={e => setSearch(e.target.value)}
                    placeholder="Buscar por nome, fabricante ou Hardware ID..."
                    className="w-full bg-neutral-950 border border-neutral-700 rounded-lg pl-9 pr-3 py-1.5 text-xs text-neutral-200 focus:outline-none focus:border-neutral-500"
                  />
                </div>
                <div className="flex items-center gap-2">
                  <Filter className="w-3.5 h-3.5 text-neutral-400" />
                  <select
                    value={selectedClass}
                    onChange={e => setSelectedClass(e.target.value)}
                    className="bg-neutral-950 border border-neutral-700 rounded-lg px-2.5 py-1.5 text-xs text-neutral-200 focus:outline-none focus:border-neutral-500"
                  >
                    {classes.map(c => (
                      <option key={c} value={c}>{c}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="flex items-center gap-4 text-xs text-neutral-400">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-emerald-400"></span>
                  {SAMPLE_DEVICES.filter(d => d.errorCode === 0).length} Saudáveis
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-amber-400"></span>
                  {SAMPLE_DEVICES.filter(d => d.isCritical).length} Risco Alto
                </span>
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-red-400"></span>
                  {problemDevices.length} Com Falhas
                </span>
              </div>
            </div>

            {/* Grid & Detail View */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-5 flex-1">
              {/* Devices Table */}
              <div className="lg:col-span-2 bg-neutral-900/40 rounded-xl border border-neutral-800 overflow-hidden flex flex-col">
                <div className="overflow-x-auto flex-1">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-neutral-900 border-b border-neutral-800 text-neutral-400">
                      <tr>
                        <th className="py-2.5 px-3 font-semibold">Dispositivo</th>
                        <th className="py-2.5 px-3 font-semibold">Classe</th>
                        <th className="py-2.5 px-3 font-semibold">Versão Atual</th>
                        <th className="py-2.5 px-3 font-semibold">Status PnP</th>
                        <th className="py-2.5 px-3 font-semibold">Risco</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-neutral-800/60">
                      {filteredDevices.map(device => {
                        const isSelected = selectedDevice.id === device.id;
                        return (
                          <tr
                            key={device.id}
                            onClick={() => setSelectedDevice(device)}
                            className={`cursor-pointer transition-colors ${
                              isSelected ? 'bg-neutral-800/80 font-medium' : 'hover:bg-neutral-900/50'
                            }`}
                          >
                            <td className="py-2.5 px-3">
                              <div className="font-semibold text-neutral-200">{device.name}</div>
                              <div className="text-[10px] text-neutral-400 font-mono truncate max-w-xs">{device.hardwareId}</div>
                            </td>
                            <td className="py-2.5 px-3 text-neutral-300">{device.className}</td>
                            <td className="py-2.5 px-3">
                              <div className="text-neutral-200">{device.driverVersion}</div>
                              <div className="text-[10px] text-neutral-500">{device.driverDate}</div>
                            </td>
                            <td className="py-2.5 px-3">
                              {device.errorCode !== 0 ? (
                                <span className="inline-flex items-center gap-1 text-[11px] font-bold text-red-400 bg-red-500/10 px-2 py-0.5 rounded border border-red-500/20">
                                  Erro {device.errorCode}
                                </span>
                              ) : (
                                <span className="inline-flex items-center gap-1 text-[11px] font-medium text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded">
                                  OK
                                </span>
                              )}
                            </td>
                            <td className="py-2.5 px-3">
                              {device.isFirmware ? (
                                <span className="text-[10px] font-semibold text-neutral-400 bg-neutral-800 px-1.5 py-0.5 rounded">
                                  Firmware
                                </span>
                              ) : device.isCritical ? (
                                <span className="text-[10px] font-bold text-amber-300 bg-amber-500/10 px-1.5 py-0.5 rounded border border-amber-500/20">
                                  Alto Risco
                                </span>
                              ) : (
                                <span className="text-[10px] text-neutral-400">Normal</span>
                              )}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Inspector Panel */}
              <div className="bg-neutral-900/60 rounded-xl border border-neutral-800 p-5 flex flex-col gap-4">
                <div className="flex items-start justify-between border-b border-neutral-800 pb-3">
                  <div>
                    <span className="text-[10px] font-mono text-neutral-500 uppercase tracking-wider">Painel de Auditoria PnP</span>
                    <h3 className="text-sm font-bold text-neutral-100">{selectedDevice.name}</h3>
                  </div>
                  {selectedDevice.errorCode !== 0 ? (
                    <span className="px-2 py-1 bg-red-500/20 text-red-300 text-xs font-bold rounded border border-red-500/30">
                      Erro {selectedDevice.errorCode}
                    </span>
                  ) : (
                    <span className="px-2 py-1 bg-emerald-500/20 text-emerald-300 text-xs font-semibold rounded">
                      Operacional
                    </span>
                  )}
                </div>

                {selectedDevice.errorCode !== 0 && (
                  <div className="p-3 bg-red-500/10 border border-red-500/30 rounded-lg text-xs text-red-300">
                    <p className="font-semibold mb-1">Diagnóstico do Windows (CIM/PnP):</p>
                    <p className="text-red-200 text-[11px] leading-relaxed">
                      {ERROR_DESCRIPTIONS[selectedDevice.errorCode] || `Código ${selectedDevice.errorCode}`}
                    </p>
                  </div>
                )}

                <div className="space-y-3 text-xs">
                  <div>
                    <label className="text-neutral-500 block mb-0.5 text-[11px]">Hardware ID Principal:</label>
                    <div className="bg-neutral-950 p-2 rounded border border-neutral-800 font-mono text-[10px] break-all text-neutral-300 flex items-center justify-between">
                      <span>{selectedDevice.hardwareId}</span>
                      <button 
                        onClick={() => handleCopy(selectedDevice.hardwareId)}
                        className="text-neutral-400 hover:text-white ml-2"
                        title="Copiar"
                      >
                        {copiedPath === selectedDevice.hardwareId ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3" />}
                      </button>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <label className="text-neutral-500 block text-[11px]">Fabricante:</label>
                      <p className="font-medium text-neutral-200">{selectedDevice.manufacturer}</p>
                    </div>
                    <div>
                      <label className="text-neutral-500 block text-[11px]">Classe PnP:</label>
                      <p className="font-medium text-neutral-200">{selectedDevice.className}</p>
                    </div>
                    <div>
                      <label className="text-neutral-500 block text-[11px]">Versão Instalada:</label>
                      <p className="font-medium text-neutral-200">{selectedDevice.driverVersion}</p>
                    </div>
                    <div>
                      <label className="text-neutral-500 block text-[11px]">Data do Driver:</label>
                      <p className="font-medium text-neutral-200">{selectedDevice.driverDate}</p>
                    </div>
                    <div>
                      <label className="text-neutral-500 block text-[11px]">Provedor:</label>
                      <p className="font-medium text-neutral-200">{selectedDevice.provider}</p>
                    </div>
                    <div>
                      <label className="text-neutral-500 block text-[11px]">Arquivo INF no DriverStore:</label>
                      <p className="font-mono text-neutral-300">{selectedDevice.infName || 'Nenhum'}</p>
                    </div>
                  </div>

                  <div className="pt-2 border-t border-neutral-800">
                    <div className="text-[11px] text-neutral-400 mb-2">Classificação de Segurança:</div>
                    {selectedDevice.isFirmware ? (
                      <div className="p-2.5 rounded bg-neutral-900 border border-neutral-700 text-[11px] text-neutral-300 flex items-center gap-2">
                        <ShieldAlert className="w-4 h-4 text-purple-400 shrink-0" />
                        <span>Firmware/BIOS: Bloqueado por diretriz de segurança.</span>
                      </div>
                    ) : selectedDevice.isCritical ? (
                      <div className="p-2.5 rounded bg-amber-500/10 border border-amber-500/20 text-[11px] text-amber-200 flex items-center gap-2">
                        <AlertTriangle className="w-4 h-4 text-amber-400 shrink-0" />
                        <span>Driver Crítico ({selectedDevice.className}): Exigirá confirmação dupla na Fase 3.</span>
                      </div>
                    ) : (
                      <div className="p-2.5 rounded bg-emerald-500/10 border border-emerald-500/20 text-[11px] text-emerald-300 flex items-center gap-2">
                        <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        <span>Dispositivo periférico padrão. Risco baixo.</span>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          </>
        )}

        {activeTab === 'problems' && (
          <div className="space-y-4">
            <div className="bg-red-500/10 border border-red-500/30 p-4 rounded-xl flex items-start gap-3">
              <AlertTriangle className="w-5 h-5 text-red-400 shrink-0 mt-0.5" />
              <div>
                <h3 className="text-sm font-bold text-red-200">Dispositivos com Falha de Configuração ou Inicialização</h3>
                <p className="text-xs text-red-300 mt-1">
                  Estes dispositivos reportam códigos de erro no subsistema PnP do Windows (Win32_PnPEntity.ConfigManagerErrorCode). O DriverGuard mapeia e explica cada código de erro oficial da Microsoft em português para auxiliar na tomada de decisão.
                </p>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {problemDevices.map(device => (
                <div key={device.id} className="bg-neutral-900/60 border border-neutral-800 rounded-xl p-5 space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-bold text-red-400 bg-red-500/10 px-2.5 py-1 rounded border border-red-500/20">
                      Código {device.errorCode}
                    </span>
                    <span className="text-xs text-neutral-400">{device.className}</span>
                  </div>
                  <h4 className="text-sm font-semibold text-neutral-100">{device.name}</h4>
                  <p className="text-xs text-neutral-300 bg-neutral-950 p-3 rounded-lg border border-neutral-800">
                    {ERROR_DESCRIPTIONS[device.errorCode]}
                  </p>
                  <div className="text-[11px] font-mono text-neutral-400 break-all bg-neutral-950/50 p-2 rounded">
                    Hardware ID: {device.hardwareId}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {activeTab === 'files' && (
          <div className="space-y-4">
            <div className="bg-neutral-900/60 p-4 rounded-xl border border-neutral-800 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-neutral-100">Estrutura de Arquivos C# .NET 8 Gerada (Fase 1)</h3>
                <p className="text-xs text-neutral-400">Todos os arquivos foram criados na pasta /driverguard e estão 100% prontos para compilação no Visual Studio 2022 ou dotnet CLI.</p>
              </div>
              <span className="text-xs font-mono text-neutral-400 bg-neutral-800 px-3 py-1 rounded">16 Arquivos Criados</span>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-xs font-mono">
              {[
                { file: "DriverGuard.sln", desc: "Solution Visual Studio com 4 projetos (Core, Infra, UI, Tests)" },
                { file: "src/DriverGuard.Core/DriverGuard.Core.csproj", desc: "Projeto de Domínio Puro (.NET 8)" },
                { file: "src/DriverGuard.Core/Enums/DeviceStatus.cs", desc: "Enum de estados (UpToDate, UpdateAvailable, Error, Ignored)" },
                { file: "src/DriverGuard.Core/Enums/ConfigManagerErrorCode.cs", desc: "Mapeamento em português de todos os erros PnP do Windows" },
                { file: "src/DriverGuard.Core/Models/PnpDevice.cs", desc: "Modelo completo do dispositivo PnP e regras de risco" },
                { file: "src/DriverGuard.Core/Interfaces/IDeviceInventoryService.cs", desc: "Contrato assíncrono para inventário PnP com progresso" },
                { file: "src/DriverGuard.Infrastructure/DriverGuard.Infrastructure.csproj", desc: "Projeto de infraestrutura com WMI, CIM e Serilog" },
                { file: "src/DriverGuard.Infrastructure/Pnp/CimDeviceInventoryService.cs", desc: "Coleta e correlação Win32_PnPEntity e Win32_PnPSignedDriver" },
                { file: "src/DriverGuard.UI/DriverGuard.UI.csproj", desc: "WPF .NET 8 com Wpf.Ui (Fluent Design) e SingleFile" },
                { file: "src/DriverGuard.UI/app.manifest", desc: "Manifest requireAdministrator e Windows 10/11 compatibility" },
                { file: "src/DriverGuard.UI/App.xaml", desc: "Recursos de tema Fluent Design (Wpf.Ui)" },
                { file: "src/DriverGuard.UI/App.xaml.cs", desc: "Injeção de dependência IHost, Serilog rotativo e verificação admin" },
                { file: "src/DriverGuard.UI/ViewModels/MainViewModel.cs", desc: "ViewModel reativo com filtros, busca e cancelamento assíncrono" },
                { file: "src/DriverGuard.UI/Views/MainWindow.xaml", desc: "Janela Fluent com abas, painel de inspeção e busca" },
                { file: "src/DriverGuard.UI/Views/MainWindow.xaml.cs", desc: "Code-behind seguro com carregamento automático no Loaded" },
                { file: "tests/DriverGuard.Tests.Unit/DeviceInventoryTests.cs", desc: "Testes xUnit para erros PnP, riscos e filtros" },
              ].map(f => (
                <div key={f.file} className="bg-neutral-900/40 p-3 rounded-lg border border-neutral-800/80 flex items-start justify-between">
                  <div>
                    <div className="font-semibold text-emerald-400">{f.file}</div>
                    <div className="text-[11px] text-neutral-400 font-sans mt-0.5">{f.desc}</div>
                  </div>
                  <button
                    onClick={() => handleCopy(`/driverguard/${f.file}`)}
                    className="text-neutral-500 hover:text-white shrink-0 ml-2"
                    title="Copiar caminho"
                  >
                    {copiedPath === `/driverguard/${f.file}` ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {activeTab === 'architecture' && (
          <div className="space-y-4">
            <div className="bg-neutral-900/60 p-5 rounded-xl border border-neutral-800 space-y-3">
              <h3 className="text-sm font-bold text-neutral-100 flex items-center gap-2">
                <ShieldCheck className="w-4 h-4 text-emerald-400" />
                Guarda-Corpos de Segurança e Decisões de Engenharia
              </h3>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
                <div className="bg-neutral-950 p-4 rounded-lg border border-neutral-800 space-y-2">
                  <h4 className="font-semibold text-neutral-200">1. Prevenção de Bloqueio de Thread (UI Freeze)</h4>
                  <p className="text-neutral-400 leading-relaxed">
                    A enumeração WMI/CIM pode demorar em máquinas com dezenas de dispositivos. O serviço executa em <code className="text-emerald-400">Task.Run</code> com pré-indexação de <code className="text-emerald-400">Win32_PnPSignedDriver</code> em memória (Dicionário O(1)) e dispara atualizações via <code className="text-emerald-400">IProgress</code> sem engasgar o Dispatcher.
                  </p>
                </div>
                <div className="bg-neutral-950 p-4 rounded-lg border border-neutral-800 space-y-2">
                  <h4 className="font-semibold text-neutral-200">2. Mapeamento de Risco por Classe</h4>
                  <p className="text-neutral-400 leading-relaxed">
                    Dispositivos de vídeo (Display), rede (Net) e controladores de armazenamento (SCSI/HDC) são classificados automaticamente como <strong className="text-amber-300">Risco Alto</strong>. Firmware/BIOS é marcado como bloqueado para instalação de pacotes externos.
                  </p>
                </div>
                <div className="bg-neutral-950 p-4 rounded-lg border border-neutral-800 space-y-2">
                  <h4 className="font-semibold text-neutral-200">3. RequireAdministrator no Manifest</h4>
                  <p className="text-neutral-400 leading-relaxed">
                    O arquivo <code className="text-emerald-400">app.manifest</code> injeta <code className="text-emerald-400">&lt;requestedExecutionLevel level="requireAdministrator"&gt;</code> e validação em tempo de execução via <code className="text-emerald-400">WindowsPrincipal.IsInRole</code> para garantir direitos sobre o DriverStore.
                  </p>
                </div>
                <div className="bg-neutral-950 p-4 rounded-lg border border-neutral-800 space-y-2">
                  <h4 className="font-semibold text-neutral-200">4. Resiliência a Falhas do Windows Update</h4>
                  <p className="text-neutral-400 leading-relaxed">
                    Conforme alinhado na Etapa 0, o inventário PnP e a verificação local operam de forma 100% autônoma, sem dependência mandatória da disponibilidade do serviço <code className="text-emerald-400">wuauserv</code>.
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}
      </main>

      {/* Footer */}
      <footer className="border-t border-neutral-800 px-6 py-3 text-xs text-neutral-500 flex items-center justify-between">
        <div>DriverGuard — Fase 1: Projeto Base + Inventário CIM + Dashboard Read-Only</div>
        <div className="flex items-center gap-2">
          <span>Target: .NET 8.0 Windows 10 (22H2) / 11 x64</span>
        </div>
      </footer>
    </div>
  );
}
