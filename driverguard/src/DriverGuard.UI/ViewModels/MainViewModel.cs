using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DriverGuard.Core.Enums;
using DriverGuard.Core.Interfaces;
using DriverGuard.Core.Models;
using DriverGuard.UI.Views.Dialogs;
using Microsoft.Win32;
using Serilog;
using Wpf.Ui.Appearance;

namespace DriverGuard.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly IDeviceInventoryService _inventoryService;
    private readonly IUpdateCheckService _updateCheckService;
    private readonly IFolderDriverParserService _folderParserService;
    private readonly IDriverInstallerService _installerService;
    private readonly IDriverRollbackService _rollbackService;
    private readonly IHistoryRepository _historyRepository;
    private readonly ICsvExportService _csvExportService;
    private readonly ILogger _logger = Log.ForContext<MainViewModel>();

    private readonly List<PnpDevice> _allDevices = new();
    private readonly List<OperationHistoryRecord> _allHistory = new();
    private readonly Dictionary<string, UpdateCandidate> _updateCandidates = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cancellationTokenSource;

    private bool _isBusy;
    // Começa em simulação: o primeiro clique não deve alterar o DriverStore sem o usuário ter escolhido o modo real
    private bool _isSimulationMode = true;
    private string _statusMessage = "Pronto para iniciar auditoria de dispositivos.";
    private string _currentProgressDevice = string.Empty;
    private int _progressCurrent;
    private int _progressTotal;
    private string _searchText = string.Empty;
    private string _selectedClass = "Todas";
    private string _selectedStatus = "Todos";
    private int _selectedTabIndex = 0;
    private PnpDevice? _selectedDevice;
    private ApplicationTheme _currentTheme = ApplicationTheme.Dark;

    // Campos da aba Histórico
    private string _historySearchText = string.Empty;
    private string _selectedHistoryStatus = "Todos";

    public ObservableCollection<PnpDevice> DisplayedDevices { get; } = new();
    public ObservableCollection<PnpDevice> ProblemDevices { get; } = new();
    public ObservableCollection<string> AvailableClasses { get; } = new() { "Todas" };
    public ObservableCollection<OperationHistoryRecord> DisplayedHistory { get; } = new();

    public ICommand RefreshInventoryCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand InspectFolderDriverCommand { get; }
    public ICommand InstallSelectedCommand { get; }
    public ICommand RollbackDeviceCommand { get; }
    public ICommand CancelOperationCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand ToggleIgnoredCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand ExportCsvCommand { get; }

    public MainViewModel(
        IDeviceInventoryService inventoryService,
        IUpdateCheckService updateCheckService,
        IFolderDriverParserService folderParserService,
        IDriverInstallerService installerService,
        IDriverRollbackService rollbackService,
        IHistoryRepository historyRepository,
        ICsvExportService csvExportService)
    {
        _inventoryService = inventoryService;
        _updateCheckService = updateCheckService;
        _folderParserService = folderParserService;
        _installerService = installerService;
        _rollbackService = rollbackService;
        _historyRepository = historyRepository;
        _csvExportService = csvExportService;

        RefreshInventoryCommand = new AsyncRelayCommand(LoadInventoryAsync, () => !IsBusy);
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, () => !IsBusy && _allDevices.Count > 0);
        InspectFolderDriverCommand = new RelayCommand(InspectFolderDriver, () => SelectedDevice != null && !IsBusy);
        InstallSelectedCommand = new AsyncRelayCommand(InstallSelectedDriverAsync, () => SelectedDevice != null && SelectedDeviceCandidate != null && !IsBusy);
        RollbackDeviceCommand = new AsyncRelayCommand(RollbackSelectedDriverAsync, () => SelectedDevice != null && !IsBusy);
        CancelOperationCommand = new RelayCommand(CancelOperation, () => IsBusy);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ToggleIgnoredCommand = new RelayCommand<PnpDevice>(ToggleIgnored);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        RefreshHistoryCommand = new AsyncRelayCommand(LoadHistoryAsync, () => !IsBusy);
        ExportCsvCommand = new AsyncRelayCommand(ExportHistoryToCsvAsync, () => DisplayedHistory.Count > 0 && !IsBusy);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                (RefreshInventoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (CheckUpdatesCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (InspectFolderDriverCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (InstallSelectedCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (RollbackDeviceCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (CancelOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RefreshHistoryCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (ExportCsvCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        set
        {
            if (SetField(ref _isSimulationMode, value))
            {
                _logger.Information("Modo Simulação alterado para: {Mode}", value);
                StatusMessage = value 
                    ? "MODO SIMULAÇÃO ATIVO: Operações serão executadas sem modificar o DriverStore."
                    : "Modo Real ativo: Alterações serão aplicadas diretamente no DriverStore do Windows.";
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string CurrentProgressDevice
    {
        get => _currentProgressDevice;
        set => SetField(ref _currentProgressDevice, value);
    }

    public int ProgressCurrent
    {
        get => _progressCurrent;
        set
        {
            if (SetField(ref _progressCurrent, value))
            {
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }
    }

    public int ProgressTotal
    {
        get => _progressTotal;
        set
        {
            if (SetField(ref _progressTotal, value))
            {
                OnPropertyChanged(nameof(ProgressPercentage));
            }
        }
    }

    public double ProgressPercentage => ProgressTotal > 0 ? ((double)ProgressCurrent / ProgressTotal) * 100 : 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedClass
    {
        get => _selectedClass;
        set
        {
            if (SetField(ref _selectedClass, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetField(ref _selectedStatus, value))
            {
                ApplyFilters();
            }
        }
    }

    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            if (SetField(ref _historySearchText, value))
            {
                ApplyHistoryFilters();
            }
        }
    }

    public string SelectedHistoryStatus
    {
        get => _selectedHistoryStatus;
        set
        {
            if (SetField(ref _selectedHistoryStatus, value))
            {
                ApplyHistoryFilters();
            }
        }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (SetField(ref _selectedTabIndex, value))
            {
                if (value == 2) // Aba Histórico
                {
                    _ = LoadHistoryAsync();
                }
            }
        }
    }

    public PnpDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetField(ref _selectedDevice, value))
            {
                (InspectFolderDriverCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (InstallSelectedCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (RollbackDeviceCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SelectedDeviceCandidate));
            }
        }
    }

    public UpdateCandidate? SelectedDeviceCandidate
    {
        get
        {
            if (SelectedDevice != null && _updateCandidates.TryGetValue(SelectedDevice.DeviceId, out var candidate))
            {
                return candidate;
            }
            return null;
        }
    }

    public int TotalDevicesCount => _allDevices.Count;
    public int ProblemsCount => _allDevices.Count(d => d.HasProblem);
    public int UpToDateCount => _allDevices.Count(d => d.Status == DeviceStatus.UpToDate);
    public int UpdateAvailableCount => _allDevices.Count(d => d.Status == DeviceStatus.UpdateAvailable);
    public int NotVerifiedCount => _allDevices.Count(d => d.Status == DeviceStatus.NotVerified);
    public int HistoryCount => _allHistory.Count;

    public async Task LoadInventoryAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        StatusMessage = "Auditando dispositivos PnP via CIM/WMI...";
        ProgressCurrent = 0;
        ProgressTotal = 0;
        CurrentProgressDevice = string.Empty;

        _cancellationTokenSource = new CancellationTokenSource();

        var progress = new Progress<(int current, int total, string currentDevice)>(p =>
        {
            ProgressCurrent = p.current;
            ProgressTotal = p.total;
            CurrentProgressDevice = p.currentDevice;
            StatusMessage = $"Auditando ({p.current}/{p.total}): {p.currentDevice}";
        });

        try
        {
            var result = await _inventoryService.GetDevicesAsync(progress, _cancellationTokenSource.Token);

            _allDevices.Clear();
            _allDevices.AddRange(result);
            _updateCandidates.Clear();

            UpdateAvailableClasses();
            ApplyFilters();
            RefreshCounters();

            StatusMessage = $"Auditoria concluída: {_allDevices.Count} dispositivos encontrados. {ProblemsCount} requerem atenção.";
            _logger.Information("Inventário atualizado. Total: {Total}, Problemas: {Problems}", _allDevices.Count, ProblemsCount);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Auditoria cancelada pelo usuário.";
            _logger.Warning("Carregamento do inventário cancelado.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha ao ler dispositivos: {ex.Message}";
            _logger.Error(ex, "Erro ao carregar inventário.");
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public async Task CheckUpdatesAsync()
    {
        if (IsBusy || _allDevices.Count == 0) return;

        IsBusy = true;
        StatusMessage = "Verificando fontes oficiais de atualização (WUA)...";
        ProgressCurrent = 0;
        ProgressTotal = _allDevices.Count;

        _cancellationTokenSource = new CancellationTokenSource();

        var progress = new Progress<(int current, int total, string deviceName, bool updateFound)>(p =>
        {
            ProgressCurrent = p.current;
            ProgressTotal = p.total;
            CurrentProgressDevice = p.deviceName;
            string foundText = p.updateFound ? "Atualização encontrada!" : "Sem atualização verificada";
            StatusMessage = $"Verificando ({p.current}/{p.total}): {p.deviceName} — {foundText}";
        });

        try
        {
            var results = await _updateCheckService.CheckUpdatesBatchAsync(_allDevices, progress, _cancellationTokenSource.Token);
            _updateCandidates.Clear();
            foreach (var kvp in results)
            {
                _updateCandidates[kvp.Key] = kvp.Value;
            }

            ApplyFilters();
            RefreshCounters();

            StatusMessage = $"Verificação concluída: {results.Count} atualizações com assinatura válida encontradas.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Verificação de atualizações cancelada pelo usuário.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha ao consultar atualizações: {ex.Message}";
            _logger.Error(ex, "Erro ao verificar atualizações.");
        }
        finally
        {
            IsBusy = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public async Task InstallSelectedDriverAsync()
    {
        if (SelectedDevice == null || SelectedDeviceCandidate == null || IsBusy) return;

        var candidate = SelectedDeviceCandidate;
        var dialog = new InstallConfirmDialog(candidate)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true || !dialog.UserConfirmed)
        {
            StatusMessage = "Instalação cancelada pelo usuário.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Instalando driver {candidate.NewVersion} para {candidate.TargetDevice.Name}...";

        try
        {
            var result = await _installerService.InstallDriverAsync(candidate, IsSimulationMode);

            System.Windows.MessageBox.Show(
                result.Message,
                result.Success ? (IsSimulationMode ? "Simulação Concluída" : "Instalação Bem-Sucedida") : "Falha na Instalação / Reversão Disparada",
                System.Windows.MessageBoxButton.OK,
                result.Success ? (result.NeedsReboot ? System.Windows.MessageBoxImage.Warning : System.Windows.MessageBoxImage.Information) : System.Windows.MessageBoxImage.Error);

            await LoadInventoryAsync();
            await LoadHistoryAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RollbackSelectedDriverAsync()
    {
        if (SelectedDevice == null || IsBusy) return;

        var dialog = new RollbackDialog(SelectedDevice, _rollbackService)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true || !dialog.UserConfirmed || dialog.SelectedBackup == null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = $"Revertendo {SelectedDevice.Name} para a versão {dialog.SelectedBackup.DriverVersion}...";

        try
        {
            var result = await _rollbackService.RollbackDriverAsync(SelectedDevice, dialog.SelectedBackup, IsSimulationMode);

            System.Windows.MessageBox.Show(
                result.Message,
                result.Success ? "Reversão Concluída" : "Falha na Reversão",
                System.Windows.MessageBoxButton.OK,
                result.Success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);

            await LoadInventoryAsync();
            await LoadHistoryAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadHistoryAsync()
    {
        try
        {
            var records = await _historyRepository.GetAllRecordsAsync();
            _allHistory.Clear();
            _allHistory.AddRange(records);
            ApplyHistoryFilters();
            OnPropertyChanged(nameof(HistoryCount));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Erro ao carregar histórico na UI.");
        }
    }

    public void ApplyHistoryFilters()
    {
        var filtered = _allHistory.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(HistorySearchText))
        {
            string q = HistorySearchText.Trim();
            filtered = filtered.Where(h =>
                h.DeviceName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                h.HardwareId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                h.OperationType.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                h.Source.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedHistoryStatus) && SelectedHistoryStatus != "Todos")
        {
            filtered = filtered.Where(h => string.Equals(h.ResultStatus, SelectedHistoryStatus, StringComparison.OrdinalIgnoreCase));
        }

        DisplayedHistory.Clear();
        foreach (var r in filtered)
        {
            DisplayedHistory.Add(r);
        }

        (ExportCsvCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public async Task ExportHistoryToCsvAsync()
    {
        if (DisplayedHistory.Count == 0) return;

        var saveDialog = new SaveFileDialog
        {
            Title = "Exportar Histórico de Auditoria para CSV (Excel)",
            Filter = "Arquivo CSV (*.csv)|*.csv",
            FileName = $"DriverGuard_Historico_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                await _csvExportService.ExportToCsvAsync(DisplayedHistory, saveDialog.FileName);
                System.Windows.MessageBox.Show(
                    $"Histórico exportado com sucesso para:\n{saveDialog.FileName}\n\nO arquivo está codificado em UTF-8 com BOM e delimitador compatível com o Microsoft Excel.",
                    "Exportação Concluída",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao salvar CSV: {ex.Message}", "Erro de Exportação", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void InspectFolderDriver()
    {
        if (SelectedDevice == null) return;

        var dialog = new FolderDriverDialog(SelectedDevice, _folderParserService)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        dialog.ShowDialog();
    }

    public void CancelOperation()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            StatusMessage = "Cancelando operação...";
            _cancellationTokenSource.Cancel();
        }
    }

    private void ToggleTheme()
    {
        _currentTheme = _currentTheme == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(_currentTheme);
        _logger.Information("Tema alterado para: {Theme}", _currentTheme);
    }

    private void ToggleIgnored(PnpDevice? device)
    {
        if (device == null) return;
        device.IsIgnored = !device.IsIgnored;
        device.Status = device.IsIgnored ? DeviceStatus.Ignored : (device.HasProblem ? DeviceStatus.Error : DeviceStatus.NotVerified);
        ApplyFilters();
        RefreshCounters();
    }

    private void UpdateAvailableClasses()
    {
        var classes = _allDevices
            .Select(d => d.ClassName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        AvailableClasses.Clear();
        AvailableClasses.Add("Todas");
        foreach (var cls in classes)
        {
            AvailableClasses.Add(cls);
        }
    }

    public void ApplyFilters()
    {
        var filtered = _allDevices.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string query = SearchText.Trim();
            filtered = filtered.Where(d =>
                d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.Manufacturer.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.PrimaryHardwareId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                d.HardwareIds.Any(h => h.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(SelectedClass) && SelectedClass != "Todas")
        {
            filtered = filtered.Where(d => string.Equals(d.ClassName, SelectedClass, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedStatus) && SelectedStatus != "Todos")
        {
            filtered = SelectedStatus switch
            {
                "Atualizados" => filtered.Where(d => d.Status == DeviceStatus.UpToDate),
                "Atualizações Disponíveis" => filtered.Where(d => d.Status == DeviceStatus.UpdateAvailable),
                "Com Problemas" => filtered.Where(d => d.Status == DeviceStatus.Error || d.HasProblem),
                "Não Verificados" => filtered.Where(d => d.Status == DeviceStatus.NotVerified),
                "Ignorados" => filtered.Where(d => d.IsIgnored),
                _ => filtered
            };
        }

        DisplayedDevices.Clear();
        foreach (var item in filtered)
        {
            DisplayedDevices.Add(item);
        }

        ProblemDevices.Clear();
        foreach (var prob in _allDevices.Where(d => d.HasProblem))
        {
            ProblemDevices.Add(prob);
        }
    }

    private void RefreshCounters()
    {
        OnPropertyChanged(nameof(TotalDevicesCount));
        OnPropertyChanged(nameof(ProblemsCount));
        OnPropertyChanged(nameof(UpToDateCount));
        OnPropertyChanged(nameof(UpdateAvailableCount));
        OnPropertyChanged(nameof(NotVerifiedCount));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
