using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ComboLab.Models;
using ComboLab.Services;
using ComboLab.Services.PresentSync;
using Microsoft.Win32;

namespace ComboLab.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public const double InputTimelinePixelsPerFrame = 28d;
    private const int InputTimelineMinimumFrames = 60;
    public const double InputTimelineLaneHeight = 42d;
    public const double InputTimelineTopPadding = 24d;

    private readonly IComboStorageService _storageService = new JsonComboStorageService();
    private readonly ComboLibraryMigrationService _migrationService = new();
    private readonly ShareTextService _shareTextService = new();
    private readonly RecentComboFileService _recentComboFileService = new();
    private readonly KeyMapProfileStorageService _keyMapStorageService = new();
    private readonly WindowsSendInputKeyboardSender _keyboardInputSender = new();
    private readonly PlaybackLogFileService _playbackLogFileService = new();
    private readonly ActionTestPlaybackService _testPlaybackService;
    private ComboLibrary _library = new();
    private ComboRecipe? _selectedCombo;
    private ComboStep? _selectedStep;
    private ActionDefinition? _selectedActionDefinition;
    private Models.InputEvent? _selectedInputEvent;
    private string? _selectedTag;
    private string? _currentFilePath;
    private bool _isTestPlaybackRunning;
    private CancellationTokenSource? _testPlaybackCancellation;
    private KeyboardSendMode _selectedKeyboardSendMode =
        KeyboardSendMode.ScanCode;
    private PressDurationKind _selectedPressDuration =
        PressDurationKind.Frames1;
    private int _selectedStartDelaySeconds = 3;
    private string _customPressMilliseconds = "16.666";
    private bool _isPresentSyncEnabled;
    private string _presentMonExePath = string.Empty;
    private string _presentProcessIdText = string.Empty;
    private string _presentProcessName = "StreetFighter6";
    private int _presentStartDelayFrames = 3;
    private string _inputPhaseOffsetText = "0.0";
    private int _requiredPresentSamples = 10;
    private PresentDropBehavior _selectedPresentDropBehavior =
        PresentDropBehavior.Continue;
    private KeyMapProfileLibrary _keyMapLibrary = KeyMapDefaults.CreateLibrary();
    private KeyMapProfile _activeKeyMapProfile =
        KeyMapDefaults.CreateLibrary().Profiles[0];
    private KeyMapBindingRow? _selectedKeyMapBindingRow;
    private InputNodeRow? _selectedInputNodeRow;
    private bool _isSynchronizingInputSelection;
    private InputTimelineDragState? _inputTimelineDragState;
    private InputNodeEditorWindow? _inputNodeEditorWindow;
    private bool _isInputTimelineDragPreviewVisible;
    private double _inputTimelineDragPreviewLeft;
    private double _inputTimelineDragPreviewTop;
    private double _inputTimelineDragPreviewLabelTop;
    private double _inputTimelineDragPreviewWidth;
    private string _inputTimelineDragPreviewText = string.Empty;
    private readonly List<string> _currentPlaybackLogLines = [];

    public MainWindow()
    {
        _testPlaybackService = new ActionTestPlaybackService(
            _keyboardInputSender);
        InitializeComponent();
        DataContext = this;
        Library = _migrationService.NormalizeAndMigrate(new ComboLibrary());
        SetStatus("新規登録するか、コンボを選択してください。");
        Loaded += MainWindow_Loaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> ActionNames { get; } = [];

    public ObservableCollection<KeyMapBindingRow> KeyMapBindingRows { get; } = [];

    public ObservableCollection<string> KeyMapPreviewLines { get; } = [];

    public ObservableCollection<InputNodeRow> InputNodeRows { get; } = [];

    public ObservableCollection<InputTimelineTick> InputTimelineTicks { get; } = [];

    public string InputEventSummaryText
    {
        get
        {
            if (SelectedActionDefinition is null)
            {
                return "アクションを選択してください。";
            }

            if (SelectedActionDefinition.InputEvents.Count == 0)
            {
                return "入力イベントはまだありません。ノード編集画面で追加できます。";
            }

            var preview = SelectedActionDefinition.InputEvents
                .OrderBy(item => item.Frame)
                .Take(5)
                .Select(item =>
                    $"{item.DisplayFrame}F:{InputNodeEditorLogic.FormatNode(item)}"
                    + $"({item.DurationFrames ?? 1}F)")
                .ToArray();
            var suffix = SelectedActionDefinition.InputEvents.Count > preview.Length
                ? $" ほか{SelectedActionDefinition.InputEvents.Count - preview.Length}件"
                : string.Empty;
            return $"{SelectedActionDefinition.InputEvents.Count}件 / "
                   + string.Join("、", preview)
                   + suffix;
        }
    }

    public double InputTimelineWidth =>
        Math.Max(
            InputTimelineMinimumFrames,
            (SelectedActionDefinition?.InputEvents.Count == 0
                ? 0
                : SelectedActionDefinition?.InputEvents.Max(item =>
                    item.Frame + (item.DurationFrames ?? 1) + 4) ?? 0))
        * InputTimelinePixelsPerFrame;

    public double InputTimelineHeight =>
        InputTimelineTopPadding
        + Math.Max(4, InputNodeRows.Count) * InputTimelineLaneHeight
        + 20;

    public bool IsInputTimelineDragPreviewVisible
    {
        get => _isInputTimelineDragPreviewVisible;
        private set
        {
            _isInputTimelineDragPreviewVisible = value;
            OnPropertyChanged();
        }
    }

    public double InputTimelineDragPreviewLeft
    {
        get => _inputTimelineDragPreviewLeft;
        private set
        {
            _inputTimelineDragPreviewLeft = value;
            OnPropertyChanged();
        }
    }

    public double InputTimelineDragPreviewTop
    {
        get => _inputTimelineDragPreviewTop;
        private set
        {
            _inputTimelineDragPreviewTop = value;
            OnPropertyChanged();
        }
    }

    public double InputTimelineDragPreviewLabelTop
    {
        get => _inputTimelineDragPreviewLabelTop;
        private set
        {
            _inputTimelineDragPreviewLabelTop = value;
            OnPropertyChanged();
        }
    }

    public double InputTimelineDragPreviewWidth
    {
        get => _inputTimelineDragPreviewWidth;
        private set
        {
            _inputTimelineDragPreviewWidth = value;
            OnPropertyChanged();
        }
    }

    public string InputTimelineDragPreviewText
    {
        get => _inputTimelineDragPreviewText;
        private set
        {
            _inputTimelineDragPreviewText = value;
            OnPropertyChanged();
        }
    }

    public static double InputTimelineFrameToLeft(int frame) =>
        Math.Max(0, frame) * InputTimelinePixelsPerFrame;

    public static double InputTimelineFramesToWidth(int frames) =>
        Math.Max(1, frames) * InputTimelinePixelsPerFrame;

    public KeyMapBindingRow? SelectedKeyMapBindingRow
    {
        get => _selectedKeyMapBindingRow;
        set
        {
            _selectedKeyMapBindingRow = value;
            OnPropertyChanged();
        }
    }

    public InputNodeRow? SelectedInputNodeRow
    {
        get => _selectedInputNodeRow;
        set
        {
            if (ReferenceEquals(_selectedInputNodeRow, value))
            {
                return;
            }

            _selectedInputNodeRow = value;
            OnPropertyChanged();
            if (!_isSynchronizingInputSelection)
            {
                _isSynchronizingInputSelection = true;
                SelectedInputEvent = value?.InputEvent;
                _isSynchronizingInputSelection = false;
            }
        }
    }

    public IReadOnlyList<KeyboardSendModeOption> KeyboardSendModeOptions { get; } =
    [
        new("VirtualKey方式", KeyboardSendMode.VirtualKey),
        new("ScanCode方式", KeyboardSendMode.ScanCode)
    ];

    public IReadOnlyList<PressDurationOption> PressDurationOptions { get; } =
    [
        new("1F", PressDurationKind.Frames1),
        new("2F", PressDurationKind.Frames2),
        new("3F", PressDurationKind.Frames3),
        new("5F", PressDurationKind.Frames5),
        new("ms指定", PressDurationKind.CustomMilliseconds)
    ];

    public IReadOnlyList<StartDelayOption> StartDelayOptions { get; } =
    [
        new("0秒", 0),
        new("1秒", 1),
        new("2秒", 2),
        new("3秒", 3),
        new("5秒", 5)
    ];

    public IReadOnlyList<PresentDropBehaviorOption>
        PresentDropBehaviorOptions { get; } =
    [
        new("続行", PresentDropBehavior.Continue),
        new("停止", PresentDropBehavior.Stop),
        new("再同期", PresentDropBehavior.Resynchronize)
    ];

    public KeyMapProfile ActiveKeyMapProfile
    {
        get => _activeKeyMapProfile;
        set
        {
            _activeKeyMapProfile = value;
            OnPropertyChanged();
            RefreshKeyMapRows();
            RefreshActiveKeyMapResolver();
        }
    }

    public bool IsPresentSyncEnabled
    {
        get => _isPresentSyncEnabled;
        set
        {
            _isPresentSyncEnabled = value;
            OnPropertyChanged();
            if (IsLoaded)
            {
                PresentSyncStatusText.Text = value
                    ? "Sync: Not Locked"
                    : "Sync: OFF";
            }
        }
    }

    public string PresentMonExePath
    {
        get => _presentMonExePath;
        set
        {
            _presentMonExePath = value;
            OnPropertyChanged();
        }
    }

    public string PresentProcessIdText
    {
        get => _presentProcessIdText;
        set
        {
            _presentProcessIdText = value;
            OnPropertyChanged();
        }
    }

    public string PresentProcessName
    {
        get => _presentProcessName;
        set
        {
            _presentProcessName = value;
            OnPropertyChanged();
        }
    }

    public int PresentStartDelayFrames
    {
        get => _presentStartDelayFrames;
        set
        {
            _presentStartDelayFrames = Math.Clamp(value, 0, 120);
            OnPropertyChanged();
        }
    }

    public string InputPhaseOffsetText
    {
        get => _inputPhaseOffsetText;
        set
        {
            _inputPhaseOffsetText = value;
            OnPropertyChanged();
        }
    }

    public int RequiredPresentSamples
    {
        get => _requiredPresentSamples;
        set
        {
            _requiredPresentSamples = Math.Clamp(value, 3, 120);
            OnPropertyChanged();
        }
    }

    public PresentDropBehavior SelectedPresentDropBehavior
    {
        get => _selectedPresentDropBehavior;
        set
        {
            _selectedPresentDropBehavior = value;
            OnPropertyChanged();
        }
    }

    public PressDurationKind SelectedPressDuration
    {
        get => _selectedPressDuration;
        set
        {
            if (_isTestPlaybackRunning || _selectedPressDuration == value)
            {
                return;
            }

            _selectedPressDuration = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomPressDuration));
        }
    }

    public int SelectedStartDelaySeconds
    {
        get => _selectedStartDelaySeconds;
        set
        {
            if (_isTestPlaybackRunning || _selectedStartDelaySeconds == value)
            {
                return;
            }

            _selectedStartDelaySeconds = value;
            OnPropertyChanged();
        }
    }

    public string CustomPressMilliseconds
    {
        get => _customPressMilliseconds;
        set
        {
            _customPressMilliseconds = value;
            OnPropertyChanged();
        }
    }

    public bool IsCustomPressDuration =>
        SelectedPressDuration == PressDurationKind.CustomMilliseconds;

    public KeyboardSendMode SelectedKeyboardSendMode
    {
        get => _selectedKeyboardSendMode;
        set
        {
            if (_isTestPlaybackRunning)
            {
                return;
            }

            if (_selectedKeyboardSendMode == value)
            {
                return;
            }

            _selectedKeyboardSendMode = value;
            OnPropertyChanged();
            SetStatus($"送信方式を{GetKeyboardSendModeDisplay(value)}に変更しました。");
        }
    }

    public ComboLibrary Library
    {
        get => _library;
        private set
        {
            DetachLibraryEvents(_library);
            _library = value;
            AttachLibraryEvents(_library);
            RefreshActionNames();
            OnPropertyChanged();
        }
    }

    public ComboRecipe? SelectedCombo
    {
        get => _selectedCombo;
        set
        {
            if (ReferenceEquals(_selectedCombo, value))
            {
                return;
            }

            _selectedCombo = value;
            SelectedStep = null;
            SelectedTag = null;
            OnPropertyChanged();
            EditorPanel.IsEnabled = value is not null;

            if (value is not null)
            {
                SetStatus($"{value.ComboName} を編集中");
            }
        }
    }

    public ComboStep? SelectedStep
    {
        get => _selectedStep;
        set
        {
            _selectedStep = value;
            OnPropertyChanged();
        }
    }

    public ActionDefinition? SelectedActionDefinition
    {
        get => _selectedActionDefinition;
        set
        {
            if (ReferenceEquals(_selectedActionDefinition, value))
            {
                return;
            }

            DetachSelectedActionInputEvents();
            CloseInputNodeEditorWindow();
            _selectedActionDefinition = value;
            SelectedInputEvent = null;
            AttachSelectedActionInputEvents();
            RefreshInputNodeRows();
            OnPropertyChanged();
            OnPropertyChanged(nameof(InputEventSummaryText));
            ActionEditorPanel.IsEnabled = value is not null;

            if (value is not null)
            {
                SetStatus($"アクション「{value.ActionName}」を編集中");
            }
        }
    }

    public Models.InputEvent? SelectedInputEvent
    {
        get => _selectedInputEvent;
        set
        {
            _selectedInputEvent = value;
            OnPropertyChanged();
            if (!_isSynchronizingInputSelection)
            {
                _isSynchronizingInputSelection = true;
                SelectedInputNodeRow = InputNodeRows.FirstOrDefault(row =>
                    ReferenceEquals(row.InputEvent, value));
                _isSynchronizingInputSelection = false;
            }
        }
    }

    public string? SelectedTag
    {
        get => _selectedTag;
        set
        {
            _selectedTag = value;
            OnPropertyChanged();
        }
    }

    private void NewCombo_Click(object sender, RoutedEventArgs e)
    {
        var combo = new ComboRecipe
        {
            GameName = "Street Fighter 6"
        };

        Library.Combos.Add(combo);
        SelectedCombo = combo;
        ComboList.ScrollIntoView(combo);
        SetStatus("新しいコンボを登録しました。");
    }

    private void DeleteCombo_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null)
        {
            SetStatus("削除するコンボを選択してください。");
            return;
        }

        var result = MessageBox.Show(
            this,
            $"「{SelectedCombo.ComboName}」を削除しますか？\n"
            + "このコンボを呼び出すノードは参照切れになります。",
            "コンボの削除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var index = Library.Combos.IndexOf(SelectedCombo);
        Library.Combos.Remove(SelectedCombo);
        SelectedCombo = Library.Combos.Count == 0
            ? null
            : Library.Combos[Math.Min(index, Library.Combos.Count - 1)];
        SetStatus("コンボを削除しました。");
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        var dataDirectory = AppDataPathService.EnsureDataDirectory();
        var dialog = new OpenFileDialog
        {
            Title = "コンボJSONを開く",
            Filter = "コンボJSON (*.json)|*.json|すべてのファイル (*.*)|*.*",
            InitialDirectory = dataDirectory,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!AppDataPathService.IsInDataDirectory(dialog.FileName))
        {
            MessageBox.Show(
                this,
                $"JSONファイルは次の保存フォルダーから選択してください。\n\n{dataDirectory}",
                "JSONを開く",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            await LoadFromFileAsync(dialog.FileName, rememberFile: true);
        }
        catch (Exception exception)
        {
            ShowError("JSONを読み込めませんでした。", exception);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        try
        {
            await LoadKeyMapsAsync();
            var startupFile =
                await _recentComboFileService.FindStartupFileAsync();
            if (startupFile is null)
            {
                return;
            }

            await LoadFromFileAsync(startupFile, rememberFile: true);
            SetStatus(
                $"{Path.GetFileName(startupFile)} を自動で読み込みました。");
        }
        catch (Exception exception)
        {
            ShowError(
                "前回使用したJSONを自動で読み込めませんでした。"
                + "空の状態で起動します。",
                exception);
        }
    }

    private async Task LoadKeyMapsAsync()
    {
        _keyMapLibrary = await _keyMapStorageService.LoadOrCreateAsync();
        ActiveKeyMapProfile = _keyMapLibrary.Profiles.FirstOrDefault(
                profile => profile.Id == _keyMapLibrary.ActiveProfileId)
            ?? _keyMapLibrary.Profiles[0];
        SetStatus("キーマップ設定を読み込みました。");
    }

    private async Task LoadFromFileAsync(
        string filePath,
        bool rememberFile)
    {
        var loaded = await _storageService.LoadAsync(filePath);
        if (loaded.SchemaVersion > 7)
        {
            throw new InvalidDataException(
                $"未対応の保存形式です (schemaVersion: {loaded.SchemaVersion})。");
        }

        Library = _migrationService.NormalizeAndMigrate(loaded);
        _currentFilePath = filePath;
        SelectedCombo = Library.Combos.FirstOrDefault();
        SelectedActionDefinition = Library.ActionDefinitions.FirstOrDefault();

        if (rememberFile)
        {
            await _recentComboFileService.RememberAsync(filePath);
        }

        SetStatus(
            $"{Path.GetFileName(filePath)} からコンボ{Library.Combos.Count}件、"
            + $"アクション定義{Library.ActionDefinitions.Count}件を読み込みました。");
    }

    private async void OverwriteSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            await SaveAsAsync();
            return;
        }

        await SaveToFileAsync(_currentFilePath, isOverwrite: true);
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e) =>
        await SaveAsAsync();

    private async Task SaveAsAsync()
    {
        var dataDirectory = AppDataPathService.EnsureDataDirectory();
        var dialog = new SaveFileDialog
        {
            Title = "コンボJSONに名前を付けて保存",
            Filter = "コンボJSON (*.json)|*.json",
            DefaultExt = ".json",
            AddExtension = true,
            FileName = _currentFilePath is null
                ? "sf6-combos.json"
                : Path.GetFileName(_currentFilePath),
            InitialDirectory = dataDirectory,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (!AppDataPathService.IsInDataDirectory(dialog.FileName))
        {
            MessageBox.Show(
                this,
                $"JSONファイルは次の保存フォルダー内へ保存してください。\n\n{dataDirectory}",
                "名前を付けて保存",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await SaveToFileAsync(dialog.FileName, isOverwrite: false);
    }

    private async Task SaveToFileAsync(string filePath, bool isOverwrite)
    {
        try
        {
            CommitPendingEdits();
            RefreshCalledComboNames();
            await _storageService.SaveAsync(filePath, Library);
            _currentFilePath = filePath;
            await _recentComboFileService.RememberAsync(filePath);
            SetStatus(
                $"{Path.GetFileName(filePath)} を"
                + (isOverwrite ? "上書き保存" : "保存")
                + "しました。");
        }
        catch (Exception exception)
        {
            ShowError("JSONを保存できませんでした。", exception);
        }
    }

    private void CommitPendingEdits()
    {
        TimelineGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        TimelineGrid.CommitEdit(DataGridEditingUnit.Row, true);
        CommitInputEventEditing();
    }

    private void CopyShareText_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null)
        {
            SetStatus("共有するコンボを選択してください。");
            return;
        }

        try
        {
            TimelineGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            Clipboard.SetText(_shareTextService.Create(SelectedCombo, Library));
            SetStatus("共有文をクリップボードにコピーしました。");
        }
        catch (Exception exception)
        {
            ShowError("共有文をコピーできませんでした。", exception);
        }
    }

    private void AddTag_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null)
        {
            return;
        }

        var tag = TagInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(tag))
        {
            SetStatus("追加するタグ名を入力してください。");
            return;
        }

        if (SelectedCombo.Tags.Contains(tag, StringComparer.CurrentCultureIgnoreCase))
        {
            SetStatus("同じタグがすでに付いています。");
            return;
        }

        SelectedCombo.Tags.Add(tag);
        TagInput.Clear();
        SetStatus($"タグ「{tag}」を追加しました。");
    }

    private void RemoveTag_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null || string.IsNullOrWhiteSpace(SelectedTag))
        {
            SetStatus("削除するタグを選択してください。");
            return;
        }

        var tag = SelectedTag;
        SelectedCombo.Tags.Remove(tag);
        SelectedTag = null;
        SetStatus($"タグ「{tag}」を削除しました。");
    }

    private void AddActionNode_Click(object sender, RoutedEventArgs e) =>
        AddNode(new ComboStep
        {
            NodeType = TimelineNodeType.Action,
            InputFrame = GetNextInputFrame(),
            ActionName = SelectedActionDefinition?.ActionName
                ?? ActionNames.FirstOrDefault()
                ?? string.Empty
        });

    private void NewActionDefinition_Click(object sender, RoutedEventArgs e)
    {
        var definition = new ActionDefinition();
        Library.ActionDefinitions.Add(definition);
        SelectedActionDefinition = definition;
        ActionDefinitionList.ScrollIntoView(definition);
        SetStatus("新しいアクション定義を作成しました。");
    }

    private void DeleteActionDefinition_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedActionDefinition is null)
        {
            SetStatus("削除するアクション定義を選択してください。");
            return;
        }

        var actionName = SelectedActionDefinition.ActionName;
        var usedCount = Library.Combos
            .SelectMany(combo => combo.Steps)
            .Count(step => step.NodeType == TimelineNodeType.Action
                           && string.Equals(
                               step.ActionName,
                               actionName,
                               StringComparison.CurrentCultureIgnoreCase));
        var message = usedCount == 0
            ? $"アクション定義「{actionName}」を削除しますか？"
            : $"アクション定義「{actionName}」を削除しますか？\n"
              + $"この名前はコンボタイムラインで{usedCount}件使用されています。"
              + "タイムラインの名前は残り、未登録として扱われます。";
        if (MessageBox.Show(
                this,
                message,
                "アクション定義の削除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var index = Library.ActionDefinitions.IndexOf(SelectedActionDefinition);
        Library.ActionDefinitions.Remove(SelectedActionDefinition);
        SelectedActionDefinition = Library.ActionDefinitions.Count == 0
            ? null
            : Library.ActionDefinitions[
                Math.Min(index, Library.ActionDefinitions.Count - 1)];
        SetStatus("アクション定義を削除しました。");
    }

    private void OpenInputNodeEditor_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedActionDefinition is null)
        {
            SetStatus("入力ノードを編集するアクションを選択してください。");
            return;
        }

        if (_inputNodeEditorWindow is not null)
        {
            _inputNodeEditorWindow.Activate();
            return;
        }

        _inputNodeEditorWindow = new InputNodeEditorWindow(
            SelectedActionDefinition,
            () => ActiveKeyMapProfile,
            RequestTestPlaybackFromInputNodeEditor)
        {
            Owner = this
        };
        _inputNodeEditorWindow.Closed += (_, _) =>
        {
            _inputNodeEditorWindow = null;
            CommitInputEventEditing();
            RefreshInputNodeRows();
            OnPropertyChanged(nameof(InputEventSummaryText));
        };
        _inputNodeEditorWindow.SetEditingEnabled(!_isTestPlaybackRunning);
        _inputNodeEditorWindow.Show();
    }

    private void CloseInputNodeEditorWindow()
    {
        if (_inputNodeEditorWindow is null)
        {
            return;
        }

        var window = _inputNodeEditorWindow;
        _inputNodeEditorWindow = null;
        window.Close();
    }

    private void RequestTestPlaybackFromInputNodeEditor()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(RequestTestPlaybackFromInputNodeEditor);
            return;
        }

        TestPlayback_Click(this, new RoutedEventArgs());
    }

    private void AddInputEvent_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedActionDefinition is null)
        {
            return;
        }

        var inputEvent = InputNodeEditorLogic.CreateNodeAfter(
            SelectedActionDefinition.InputEvents,
            SelectedInputEvent);
        var insertIndex = SelectedInputEvent is null
            ? SelectedActionDefinition.InputEvents.Count
            : SelectedActionDefinition.InputEvents.IndexOf(SelectedInputEvent) + 1;
        SelectedActionDefinition.InputEvents.Insert(
            Math.Clamp(insertIndex, 0, SelectedActionDefinition.InputEvents.Count),
            inputEvent);
        SelectInputEvent(inputEvent);
    }

    private void DeleteInputEvent_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedActionDefinition is null || SelectedInputEvent is null)
        {
            return;
        }

        var index = SelectedActionDefinition.InputEvents.IndexOf(SelectedInputEvent);
        SelectedActionDefinition.InputEvents.Remove(SelectedInputEvent);
        SelectInputEvent(SelectedActionDefinition.InputEvents.Count == 0
            ? null
            : SelectedActionDefinition.InputEvents[
                Math.Min(index, SelectedActionDefinition.InputEvents.Count - 1)]);
    }

    private void DuplicateInputNode_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedActionDefinition is null || SelectedInputEvent is null)
        {
            return;
        }

        var inputEvent = InputNodeEditorLogic.DuplicateNode(
            SelectedActionDefinition.InputEvents,
            SelectedInputEvent);
        var insertIndex =
            SelectedActionDefinition.InputEvents.IndexOf(SelectedInputEvent) + 1;
        SelectedActionDefinition.InputEvents.Insert(insertIndex, inputEvent);
        SelectInputEvent(inputEvent);
    }

    private void SortInputNodesByFrame_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedActionDefinition is null)
        {
            return;
        }

        var selected = SelectedInputEvent;
        var sorted = SelectedActionDefinition.InputEvents
            .OrderBy(item => item.Frame)
            .ThenBy(item => SelectedActionDefinition.InputEvents.IndexOf(item))
            .ToArray();
        SelectedActionDefinition.InputEvents.Clear();
        foreach (var inputEvent in sorted)
        {
            SelectedActionDefinition.InputEvents.Add(inputEvent);
        }

        SelectInputEvent(selected);
    }

    private void ClearInputNode_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInputEvent is null)
        {
            return;
        }

        InputNodeEditorLogic.Clear(SelectedInputEvent);
        RefreshInputNodeRows();
    }

    private void SetInputNodeDirection_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInputEvent is null
            || sender is not Button { Tag: string direction })
        {
            return;
        }

        InputNodeEditorLogic.SetDirection(SelectedInputEvent, direction);
        RefreshInputNodeRows();
    }

    private void ToggleInputNodeAttack_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInputEvent is null
            || sender is not Button { Tag: string attack })
        {
            return;
        }

        InputNodeEditorLogic.ToggleAttack(SelectedInputEvent, attack);
        RefreshInputNodeRows();
    }

    private void InputTimelineCanvas_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
        {
            return;
        }

        if (SelectedActionDefinition is null)
        {
            return;
        }

        var frame = Math.Max(
            0,
            (int)Math.Round(
                e.GetPosition(InputTimelineCanvas).X
                / InputTimelinePixelsPerFrame));
        var inputEvent = InputNodeEditorLogic.CreateNode(frame);
        SelectedActionDefinition.InputEvents.Add(inputEvent);
        SelectInputEvent(inputEvent);
        e.Handled = true;
    }

    private void InputTimelineNode_SelectOnly(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: InputNodeRow row })
        {
            UpdateFocusedBindingSource();
            SelectInputEvent(row.InputEvent);
        }

        e.Handled = false;
    }

    private void InputTimelineNode_DragStarted(
        object sender,
        DragStartedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InputNodeRow row })
        {
            return;
        }

        CommitInputEventEditing(refreshNodes: false);
        SelectInputEvent(row.InputEvent);
        var cursorX = Mouse.GetPosition(InputTimelineCanvas).X;
        _inputTimelineDragState = new InputTimelineDragState(
            row,
            cursorX,
            row.InputEvent.Frame,
            row.InputEvent.DurationFrames ?? GetCurrentHoldDurationFrames(),
            false);
        ShowInputTimelineDragPreview(_inputTimelineDragState);
    }

    private void InputTimelineResize_DragStarted(
        object sender,
        DragStartedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InputNodeRow row })
        {
            return;
        }

        CommitInputEventEditing(refreshNodes: false);
        SelectInputEvent(row.InputEvent);
        var cursorX = Mouse.GetPosition(InputTimelineCanvas).X;
        _inputTimelineDragState = new InputTimelineDragState(
            row,
            cursorX,
            row.InputEvent.Frame,
            row.InputEvent.DurationFrames ?? GetCurrentHoldDurationFrames(),
            true);
        ShowInputTimelineDragPreview(_inputTimelineDragState);
    }

    private void InputTimelineNode_DragDelta(
        object sender,
        DragDeltaEventArgs e)
    {
        if (_inputTimelineDragState is null)
        {
            return;
        }

        var cursorX = Mouse.GetPosition(InputTimelineCanvas).X;
        _inputTimelineDragState.TargetFrame =
            CalculateFrameFromCursor(
                cursorX,
                _inputTimelineDragState.GrabOffsetX);
        ShowInputTimelineDragPreview(_inputTimelineDragState);
    }

    private void InputTimelineResize_DragDelta(
        object sender,
        DragDeltaEventArgs e)
    {
        if (_inputTimelineDragState is null)
        {
            return;
        }

        var cursorX = Mouse.GetPosition(InputTimelineCanvas).X;
        _inputTimelineDragState.TargetDurationFrames =
            CalculateDurationFromCursor(
                cursorX,
                _inputTimelineDragState.StartFrame);
        ShowInputTimelineDragPreview(_inputTimelineDragState);
    }

    private void ShowInputTimelineDragPreview(InputTimelineDragState state)
    {
        InputTimelineDragPreviewLeft =
            InputTimelineFrameToLeft(state.TargetFrame);
        InputTimelineDragPreviewTop = state.Row.TimelineTop;
        InputTimelineDragPreviewLabelTop =
            Math.Max(0, state.Row.TimelineTop - 24);
        InputTimelineDragPreviewWidth =
            InputTimelineFramesToWidth(state.TargetDurationFrames);
        InputTimelineDragPreviewText = state.IsResize
            ? $"保持 {state.TargetDurationFrames}F"
            : $"{state.TargetFrame + 1}F";
        IsInputTimelineDragPreviewVisible = true;
    }

    private void HideInputTimelineDragPreview() =>
        IsInputTimelineDragPreviewVisible = false;

    private static int CalculateFrameFromCursor(
        double cursorX,
        double grabOffsetX) =>
        Math.Max(
            0,
            (int)Math.Round(
                (cursorX - grabOffsetX) / InputTimelinePixelsPerFrame,
                MidpointRounding.AwayFromZero));

    private static int CalculateDurationFromCursor(
        double cursorX,
        int startFrame) =>
        Math.Max(
            1,
            (int)Math.Round(
                (cursorX - InputTimelineFrameToLeft(startFrame))
                / InputTimelinePixelsPerFrame,
                MidpointRounding.AwayFromZero));

    private void InputTimelineNode_DragCompleted(
        object sender,
        DragCompletedEventArgs e)
    {
        if (_inputTimelineDragState is not null)
        {
            if (_inputTimelineDragState.IsResize)
            {
                _inputTimelineDragState.Row.DurationFrames =
                    _inputTimelineDragState.TargetDurationFrames;
            }
            else
            {
                _inputTimelineDragState.Row.InternalFrame =
                    _inputTimelineDragState.TargetFrame;
            }
        }

        _inputTimelineDragState = null;
        HideInputTimelineDragPreview();
        RefreshInputNodeRows();
    }

    private void MoveInputEventUp_Click(object sender, RoutedEventArgs e) =>
        MoveSelectedInputEvent(-1);

    private void MoveInputEventDown_Click(object sender, RoutedEventArgs e) =>
        MoveSelectedInputEvent(1);

    private void MoveSelectedInputEvent(int offset)
    {
        if (SelectedActionDefinition is null || SelectedInputEvent is null)
        {
            return;
        }

        var oldIndex = SelectedActionDefinition.InputEvents.IndexOf(SelectedInputEvent);
        var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= SelectedActionDefinition.InputEvents.Count)
        {
            return;
        }

        SelectedActionDefinition.InputEvents.Move(oldIndex, newIndex);
        SelectInputEvent(SelectedInputEvent);
    }

    private void SelectInputEvent(Models.InputEvent? inputEvent)
    {
        SelectedInputEvent = inputEvent;
        if (inputEvent is null)
        {
            return;
        }

        InputEventGrid.ScrollIntoView(inputEvent);
        InputNodeList.ScrollIntoView(SelectedInputNodeRow);
        InputNodeList.Focus();
    }

    private async void TestPlayback_Click(object sender, RoutedEventArgs e)
    {
        if (_isTestPlaybackRunning)
        {
            SetStatus("テスト再生はすでに実行中です。");
            return;
        }

        if (SelectedActionDefinition is null)
        {
            SetStatus("再生するアクション定義を選択してください。");
            return;
        }

        CommitInputEventEditing();
        SelectedKeyboardSendMode = KeyboardSendMode.ScanCode;
        SelectedPressDuration = PressDurationKind.Frames1;
        IsPresentSyncEnabled = false;

        var actionDefinition = CreatePlaybackSnapshot(SelectedActionDefinition);
        var keyMapResolver = new KeyMapResolver(
            CreateKeyMapSnapshot(ActiveKeyMapProfile));
        var unsupportedNames =
            ActionTestPlaybackService.FindUnsupportedKeyNames(
                actionDefinition,
                keyMapResolver);
        if (unsupportedNames.Count > 0)
        {
            MessageBox.Show(
                this,
                "入力表記またはキーマップに問題があります。\n\n"
                + string.Join(Environment.NewLine, unsupportedNames),
                "テスト再生",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            SetStatus("入力表記またはキーマップを確認してください。");
            return;
        }

        if (unsupportedNames.Count > 0)
        {
            MessageBox.Show(
                this,
                "次の入力は物理キー名として認識できません。\n\n"
                + string.Join(", ", unsupportedNames)
                + "\n\nA～Z、0～9、Space、Enter、方向キー名などを入力してください。",
                "テスト再生",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            SetStatus("認識できない物理キー名があるため再生しませんでした。");
            return;
        }

        var sendMode = SelectedKeyboardSendMode;
        var startDelaySeconds = SelectedStartDelaySeconds;
        if (!TryGetPressDuration(out var pressDuration))
        {
            MessageBox.Show(
                this,
                "保持時間のms指定には、0より大きい数値を入力してください。",
                "テスト再生",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _currentPlaybackLogLines.Clear();
        TestPlaybackLogText.Clear();
        var playbackPlan = ActionTestPlaybackService.BuildPlaybackPlan(
            actionDefinition,
            pressDuration,
            keyMapResolver);
        AppendTestPlaybackLog(
            $"[Start] time={DateTime.Now:O} "
            + $"action={actionDefinition.ActionName} "
            + $"sender={sendMode} "
            + $"presentSync={IsPresentSyncEnabled} "
            + $"delay={startDelaySeconds}秒 "
            + $"frameMs={ActionTestPlaybackService.MillisecondsPerFrame:0.###} "
            + $"holdMs={pressDuration.Value.TotalMilliseconds:0.###}");
        AppendTestPlaybackLog("[InputNodes]");
        foreach (var line in FormatInputNodeLogLines(actionDefinition))
        {
            AppendTestPlaybackLog(line);
        }

        AppendTestPlaybackLog(string.Empty);
        AppendTestPlaybackLog("[InputEvents]");
        foreach (var line in ActionTestPlaybackService.FormatInputEvents(
                     actionDefinition))
        {
            AppendTestPlaybackLog(line);
        }

        AppendTestPlaybackLog(string.Empty);
        AppendTestPlaybackLog("[Resolve]");
        foreach (var line in ActionTestPlaybackService.FormatResolvedInputEvents(
                     actionDefinition,
                     keyMapResolver))
        {
            AppendTestPlaybackLog(line);
        }

        AppendTestPlaybackLog(string.Empty);
        AppendTestPlaybackLog("[PlaybackPlan]");
        foreach (var line in ActionTestPlaybackService.FormatPlaybackPlan(
                     playbackPlan))
        {
            AppendTestPlaybackLog(line);
        }

        AppendTestPlaybackLog(string.Empty);
        AppendTestPlaybackLog("[Send]");

        try
        {
            _isTestPlaybackRunning = true;
            _testPlaybackCancellation = new CancellationTokenSource();
            SetPlaybackControlsEnabled(false);
            _keyboardInputSender.SendMode = sendMode;
            if (IsPresentSyncEnabled)
            {
                try
                {
                    await PlayWithPresentSyncAsync(
                        actionDefinition,
                        pressDuration,
                        _testPlaybackCancellation.Token);
                }
                catch (Exception presentException)
                    when (presentException is not OperationCanceledException)
                {
                    AppendTestPlaybackLog(
                        $"[PresentSync Error] {presentException}");
                    var fallback = MessageBox.Show(
                        this,
                        "Present同期を開始できませんでした。\n\n"
                        + presentException.Message
                        + "\n\n通常タイマー再生へ切り替えますか？",
                        "Present同期",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (fallback != MessageBoxResult.Yes)
                    {
                        throw;
                    }

                    AppendTestPlaybackLog(
                        "[PresentSync] 通常タイマー再生へフォールバックします。");
                    await RunTestPlaybackCountdownAsync(
                        sendMode,
                        startDelaySeconds,
                        _testPlaybackCancellation.Token);
                    await _testPlaybackService.PlayAsync(
                        actionDefinition,
                        pressDuration,
                        _testPlaybackCancellation.Token,
                        AppendTestPlaybackLog);
                }
            }
            else
            {
                await RunTestPlaybackCountdownAsync(
                    sendMode,
                    startDelaySeconds,
                    _testPlaybackCancellation.Token);
                await _testPlaybackService.PlayAsync(
                    actionDefinition,
                    pressDuration,
                    _testPlaybackCancellation.Token,
                    AppendTestPlaybackLog);
            }

            TestPlaybackStatusText.Text =
                $"{GetKeyboardSendModeDisplay(sendMode)}で再生が完了しました。";
            SetStatus(TestPlaybackStatusText.Text);
        }
        catch (OperationCanceledException)
        {
            AppendTestPlaybackLog("[Cancel] 再生をキャンセルしました。");
            TestPlaybackStatusText.Text = "再生をキャンセルしました。";
            SetStatus("テスト再生をキャンセルしました。");
        }
        catch (Exception exception)
        {
            AppendTestPlaybackLog(
                $"[Error] {exception.GetType().FullName}: "
                + $"{exception.Message}{Environment.NewLine}"
                + exception.StackTrace);
            TestPlaybackStatusText.Text = "再生に失敗しました。";
            ShowError("テスト再生に失敗しました。", exception);
        }
        finally
        {
            try
            {
                _testPlaybackService.ReleaseAllKeys(AppendTestPlaybackLog);
            }
            catch (Exception releaseException)
            {
                AppendTestPlaybackLog(
                    $"[Error] 全キー解放に失敗しました: "
                    + $"{releaseException}{Environment.NewLine}");
            }

            AppendTestPlaybackLog("[End] テスト再生処理を終了しました。");
            try
            {
                var logPath = await _playbackLogFileService.WriteAsync(
                    actionDefinition.ActionName,
                    _currentPlaybackLogLines);
                AppendTestPlaybackLog($"[Log] {logPath}");
            }
            catch (Exception logException)
            {
                AppendTestPlaybackLog(
                    $"[Error] ログ保存に失敗しました: {logException.Message}");
            }

            _testPlaybackCancellation?.Dispose();
            _testPlaybackCancellation = null;
            _isTestPlaybackRunning = false;
            SetPlaybackControlsEnabled(true);
        }
    }

    private async Task RunTestPlaybackCountdownAsync(
        KeyboardSendMode sendMode,
        int startDelaySeconds,
        CancellationToken cancellationToken)
    {
        var modeDisplay = GetKeyboardSendModeDisplay(sendMode);
        for (var seconds = startDelaySeconds; seconds >= 1; seconds--)
        {
            var message =
                $"あと{seconds}秒で再生します。"
                + "対象ウィンドウを前面にしてください。";
            TestPlaybackStatusText.Text = message;
            SetStatus(message);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        TestPlaybackStatusText.Text =
            $"{modeDisplay}で再生中です。";
        SetStatus(TestPlaybackStatusText.Text);
    }

    private async Task PlayWithPresentSyncAsync(
        ActionDefinition actionDefinition,
        KeyPressDuration holdDuration,
        CancellationToken cancellationToken)
    {
        if (!double.TryParse(
                InputPhaseOffsetText,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var phaseOffsetMs)
            || phaseOffsetMs is < -20 or > 20)
        {
            throw new InvalidOperationException(
                "入力位相補正msは-20.0～+20.0の範囲で入力してください。");
        }

        int? processId = null;
        if (!string.IsNullOrWhiteSpace(PresentProcessIdText))
        {
            if (!int.TryParse(PresentProcessIdText, out var parsedProcessId)
                || parsedProcessId <= 0)
            {
                throw new InvalidOperationException(
                    "対象PIDには1以上の整数を入力してください。");
            }

            processId = parsedProcessId;
        }

        var estimator = new PresentClockEstimator(
            RequiredPresentSamples);
        await using IPresentFrameSource source =
            new PresentMonConsolePresentFrameSource();
        string? captureError = null;
        var resyncRequested = 0;
        PresentFrameEvent? latestFrame = null;

        source.CaptureError += (_, message) =>
        {
            captureError = message;
            AppendTestPlaybackLog($"[PresentMon] {message}");
        };
        source.FramePresented += (_, frame) =>
        {
            latestFrame = frame;
            estimator.AddPresent(frame.PresentQpc);
            if (estimator.LastPresentWasIrregular)
            {
                AppendTestPlaybackLog(
                    $"[PresentSync] 不規則Presentを検出: "
                    + $"delta={estimator.LatestDeltaMs:0.###}ms "
                    + $"estimated={estimator.EstimatedFrameMs:0.###}ms");
                switch (SelectedPresentDropBehavior)
                {
                    case PresentDropBehavior.Stop:
                        _testPlaybackCancellation?.Cancel();
                        break;
                    case PresentDropBehavior.Resynchronize:
                        Interlocked.Exchange(ref resyncRequested, 1);
                        break;
                }
            }

            Dispatcher.BeginInvoke(() =>
            {
                PresentSyncStatusText.Text = estimator.IsLocked
                    ? "Sync: Locked"
                    : "Sync: Not Locked";
                PresentSyncStatusText.Foreground = estimator.IsLocked
                    ? Brushes.ForestGreen
                    : Brushes.DarkOrange;
                PresentSyncDetailText.Text =
                    $"fps={(estimator.EstimatedFrameMs > 0 ? 1000 / estimator.EstimatedFrameMs : 0):0.##} "
                    + $"frame={estimator.EstimatedFrameMs:0.###}ms "
                    + $"delta={estimator.LatestDeltaMs:0.###}ms "
                    + $"PID={frame.ProcessId} "
                    + (string.IsNullOrWhiteSpace(frame.PresentMode)
                        ? string.Empty
                        : $"mode={frame.PresentMode}");
            });
        };

        var captureOptions = new PresentCaptureOptions(
            processId,
            string.IsNullOrWhiteSpace(PresentProcessName)
                ? null
                : PresentProcessName.Trim(),
            PresentMonExePath.Trim(),
            "SF6ComboDevApp");
        PresentSyncStatusText.Text = "Sync: Starting";
        TestPlaybackStatusText.Text =
            "PresentMonを起動し、同期ロックを待っています。";
        await source.StartAsync(captureOptions, cancellationToken);

        try
        {
            var lockTimeout = Stopwatch.StartNew();
            while (!estimator.IsLocked)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(captureError)
                    && captureError.Contains(
                        "QPCTime",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(captureError);
                }

                if (lockTimeout.Elapsed > TimeSpan.FromSeconds(15))
                {
                    throw new TimeoutException(
                        "15秒以内にPresent同期をロックできませんでした。"
                        + "対象プロセス、PresentMonの権限、QPCTime列を確認してください。"
                        + (string.IsNullOrWhiteSpace(captureError)
                            ? string.Empty
                            : Environment.NewLine + captureError));
                }

                await Task.Delay(20, cancellationToken);
            }

            var macroBasePresentIndex =
                estimator.LatestPresentIndex + PresentStartDelayFrames;
            AppendTestPlaybackLog(
                $"[PresentSync] Locked "
                + $"basePresent=#{macroBasePresentIndex} "
                + $"frameMs={estimator.EstimatedFrameMs:0.###} "
                + $"fps={1000 / estimator.EstimatedFrameMs:0.###} "
                + $"phaseOffsetMs={phaseOffsetMs:0.###} "
                + $"PID={latestFrame?.ProcessId} "
                + $"PresentMode={latestFrame?.PresentMode ?? "不明"}");
            TestPlaybackStatusText.Text =
                $"Present同期で再生中です。開始Present #{macroBasePresentIndex}";

            await _testPlaybackService.PlayPresentSynchronizedAsync(
                actionDefinition,
                holdDuration,
                estimator,
                new PresentSyncPlaybackOptions(
                    macroBasePresentIndex,
                    PresentStartDelayFrames,
                    phaseOffsetMs,
                    () => Interlocked.Exchange(
                        ref resyncRequested,
                        0) == 1),
                cancellationToken,
                AppendTestPlaybackLog);
        }
        finally
        {
            await source.StopAsync(CancellationToken.None);
        }
    }

    private void BrowsePresentMon_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "PresentMonの実行ファイルを選択",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            PresentMonExePath = dialog.FileName;
        }
    }

    private void CancelTestPlayback_Click(object sender, RoutedEventArgs e)
    {
        if (!_isTestPlaybackRunning || _testPlaybackCancellation is null)
        {
            return;
        }

        CancelTestPlaybackButton.IsEnabled = false;
        TestPlaybackStatusText.Text = "キャンセルしています。";
        _testPlaybackCancellation.Cancel();
    }

    private void AppendTestPlaybackLog(string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendTestPlaybackLog(message));
            return;
        }

        _currentPlaybackLogLines.Add(message);
        TestPlaybackLogText.AppendText(message + Environment.NewLine);
        TestPlaybackLogText.ScrollToEnd();
    }

    private bool TryGetPressDuration(out KeyPressDuration duration)
    {
        duration = SelectedPressDuration switch
        {
            PressDurationKind.Frames1 => KeyPressDuration.FromFrames(1),
            PressDurationKind.Frames2 => KeyPressDuration.FromFrames(2),
            PressDurationKind.Frames3 => KeyPressDuration.FromFrames(3),
            PressDurationKind.Frames5 => KeyPressDuration.FromFrames(5),
            _ => default
        };

        if (SelectedPressDuration != PressDurationKind.CustomMilliseconds)
        {
            return true;
        }

        if (!double.TryParse(
                CustomPressMilliseconds,
                NumberStyles.Float,
                CultureInfo.CurrentCulture,
                out var milliseconds)
            || milliseconds <= 0)
        {
            return false;
        }

        duration = KeyPressDuration.FromMilliseconds(milliseconds);
        return true;
    }

    private int GetCurrentHoldDurationFrames()
    {
        return TryGetPressDuration(out var duration)
            ? Math.Max(
                1,
                (int)Math.Round(
                    duration.Value.TotalSeconds
                    * ActionTestPlaybackService.FramesPerSecond))
            : 1;
    }

    private void SetPlaybackControlsEnabled(bool enabled)
    {
        TestPlaybackButton.IsEnabled =
            enabled && SelectedActionDefinition is not null;
        CancelTestPlaybackButton.IsEnabled = !enabled;
        KeyboardSendModeComboBox.IsEnabled = enabled;
        PressDurationComboBox.IsEnabled = enabled;
        CustomPressMillisecondsTextBox.IsEnabled =
            enabled && IsCustomPressDuration;
        StartDelayComboBox.IsEnabled = enabled;
        PresentSyncEnabledCheckBox.IsEnabled = enabled;
        PresentSyncSettingsPanel.IsEnabled = enabled;
        InputEventEditButtonsPanel.IsEnabled = enabled;
        InputEventGrid.IsEnabled = enabled;
        InputNodeEditorPanel.IsEnabled = enabled;
        _inputNodeEditorWindow?.SetEditingEnabled(enabled);
    }

    private void CommitInputEventEditing(bool refreshNodes = true)
    {
        UpdateFocusedBindingSource();
        InputEventGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        InputEventGrid.CommitEdit(DataGridEditingUnit.Row, true);
        UpdateFocusedBindingSource();
        if (refreshNodes)
        {
            RefreshInputNodeRows();
        }
    }

    private static void UpdateFocusedBindingSource()
    {
        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
        {
            return;
        }

        if (focusedElement is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            return;
        }

        if (focusedElement is ComboBox comboBox)
        {
            comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();
            comboBox.GetBindingExpression(Selector.SelectedValueProperty)
                ?.UpdateSource();
            comboBox.GetBindingExpression(Selector.SelectedItemProperty)
                ?.UpdateSource();
        }
    }

    private static ActionDefinition CreatePlaybackSnapshot(
        ActionDefinition source) =>
        new()
        {
            Id = source.Id,
            ActionName = source.ActionName,
            Description = source.Description,
            InputEvents = new ObservableCollection<Models.InputEvent>(
                source.InputEvents.Select(CloneInputEvent))
        };

    private static Models.InputEvent CloneInputEvent(
        Models.InputEvent source) =>
        new()
        {
            Frame = source.Frame,
            DurationFrames = source.DurationFrames,
            LogicalInputs = new ObservableCollection<string>(
                source.LogicalInputs)
        };

    private static KeyMapProfile CreateKeyMapSnapshot(KeyMapProfile source)
    {
        var bindings =
            new ObservableDictionary<string, ObservableCollection<string>>();
        foreach (var binding in source.Bindings)
        {
            bindings[binding.Key] = new ObservableCollection<string>(
                binding.Value);
        }

        return new KeyMapProfile
        {
            Id = source.Id,
            Name = source.Name,
            Bindings = bindings
        };
    }

    private void RefreshActiveKeyMapResolver()
    {
        _testPlaybackService.SetKeyMapResolver(
            new KeyMapResolver(ActiveKeyMapProfile));
        RefreshKeyMapPreview();
        RefreshInputNodeRows();
    }

    private void AttachSelectedActionInputEvents()
    {
        if (SelectedActionDefinition is null)
        {
            return;
        }

        SelectedActionDefinition.InputEvents.CollectionChanged +=
            InputEvents_CollectionChanged;
        foreach (var inputEvent in SelectedActionDefinition.InputEvents)
        {
            inputEvent.PropertyChanged += InputEvent_PropertyChanged;
        }
    }

    private void DetachSelectedActionInputEvents()
    {
        if (_selectedActionDefinition is null)
        {
            return;
        }

        _selectedActionDefinition.InputEvents.CollectionChanged -=
            InputEvents_CollectionChanged;
        foreach (var inputEvent in _selectedActionDefinition.InputEvents)
        {
            inputEvent.PropertyChanged -= InputEvent_PropertyChanged;
        }
    }

    private void InputEvents_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (Models.InputEvent inputEvent in e.OldItems)
            {
                inputEvent.PropertyChanged -= InputEvent_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (Models.InputEvent inputEvent in e.NewItems)
            {
                inputEvent.PropertyChanged += InputEvent_PropertyChanged;
            }
        }

        RefreshInputNodeRows();
    }

    private void InputEvent_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Models.InputEvent.Frame)
            or nameof(Models.InputEvent.DisplayFrame)
            or nameof(Models.InputEvent.DurationFrames)
            or nameof(Models.InputEvent.DisplayDurationFrames)
            or nameof(Models.InputEvent.LogicalInputs)
            or nameof(Models.InputEvent.LogicalInputsText))
        {
            if (_inputTimelineDragState is not null)
            {
                RefreshInputTimelineMetrics();
                return;
            }

            RefreshInputNodeRows();
        }
    }

    private void RefreshInputNodeRows()
    {
        var selected = SelectedInputEvent;
        foreach (var row in InputNodeRows)
        {
            row.Dispose();
        }

        InputNodeRows.Clear();
        if (SelectedActionDefinition is not null)
        {
            foreach (var inputEvent in SelectedActionDefinition.InputEvents)
            {
                InputNodeRows.Add(new InputNodeRow(
                    inputEvent,
                    InputNodeRows.Count,
                    () => ActiveKeyMapProfile));
            }
        }

        _isSynchronizingInputSelection = true;
        SelectedInputNodeRow = InputNodeRows.FirstOrDefault(row =>
            ReferenceEquals(row.InputEvent, selected));
        _isSynchronizingInputSelection = false;
        RefreshInputTimelineMetrics();
    }

    private void RefreshInputTimelineMetrics()
    {
        RefreshInputTimelineTicks();
        OnPropertyChanged(nameof(InputTimelineWidth));
        OnPropertyChanged(nameof(InputTimelineHeight));
        OnPropertyChanged(nameof(InputEventSummaryText));
    }

    private void RefreshInputTimelineTicks()
    {
        InputTimelineTicks.Clear();
        var frameCount = (int)Math.Ceiling(
            InputTimelineWidth / InputTimelinePixelsPerFrame);
        for (var frame = 0; frame <= frameCount; frame++)
        {
            var displayFrame = frame + 1;
            InputTimelineTicks.Add(new InputTimelineTick(
                InputTimelineFrameToLeft(frame),
                displayFrame == 1 || displayFrame % 5 == 0
                    ? $"{displayFrame}F"
                    : string.Empty));
        }
    }

    private static IReadOnlyList<string> FormatInputNodeLogLines(
        ActionDefinition actionDefinition) =>
        (actionDefinition.InputEvents ?? [])
            .OrderBy(item => item.Frame)
            .Select(item =>
                $"{item.DisplayFrame}F display / {item.Frame}F internal: "
                + InputNodeEditorLogic.FormatNode(item)
                + $" / hold={(item.DurationFrames ?? 1).ToString(CultureInfo.InvariantCulture)}F")
            .ToArray();

    private void RefreshKeyMapRows()
    {
        KeyMapBindingRows.Clear();
        foreach (var action in ActiveKeyMapProfile.Bindings.Keys
                     .OrderBy(GetKeyMapSortOrder)
                     .ThenBy(action => action, StringComparer.OrdinalIgnoreCase))
        {
            ActiveKeyMapProfile.Bindings.TryGetValue(action, out var bindings);
            KeyMapBindingRows.Add(new KeyMapBindingRow(
                action,
                string.Join(", ", bindings ?? [])));
        }

        RefreshKeyMapPreview();
    }

    private void RefreshKeyMapPreview()
    {
        KeyMapPreviewLines.Clear();
        var resolver = new KeyMapResolver(ActiveKeyMapProfile);
        foreach (var line in resolver.FormatPreviewDirections())
        {
            KeyMapPreviewLines.Add(line);
        }
    }

    private async void SaveKeyMap_Click(object sender, RoutedEventArgs e)
    {
        var invalidKeys = new List<string>();
        var invalidActions = new List<string>();
        var duplicateActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in KeyMapBindingRows)
        {
            var action = KeyMapDefaults.NormalizeActionName(row.Action);
            if (string.IsNullOrWhiteSpace(action)
                || KeyMapDefaults.GeneratedActions.Contains(
                    action,
                    StringComparer.OrdinalIgnoreCase))
            {
                invalidActions.Add(row.Action);
            }

            if (!seenActions.Add(action))
            {
                duplicateActions.Add(action);
            }

            var keys = ParseKeyMapKeys(row.KeysText);
            invalidKeys.AddRange(keys.Where(key =>
                !PhysicalKeyNameParser.IsNeutral(key)
                && !PhysicalKeyNameParser.TryParse(key, out _)));
        }

        if (invalidActions.Count > 0 || duplicateActions.Count > 0)
        {
            MessageBox.Show(
                this,
                "入力項目に問題があります。\n\n"
                + (invalidActions.Count == 0
                    ? string.Empty
                    : "空欄、または自動生成される 1/3/5/7/9 は保存できません。\n"
                      + string.Join(", ", invalidActions))
                + (duplicateActions.Count == 0
                    ? string.Empty
                    : "\n重複: "
                      + string.Join(", ", duplicateActions)),
                "キーマップ設定",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (invalidKeys.Count > 0)
        {
            MessageBox.Show(
                this,
                "キーマップに認識できない物理キーがあります。\n\n"
                + string.Join(", ", invalidKeys.Distinct(StringComparer.OrdinalIgnoreCase)),
                "キーマップ設定",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        ActiveKeyMapProfile.Bindings.Clear();
        foreach (var row in KeyMapBindingRows)
        {
            ActiveKeyMapProfile.Bindings[KeyMapDefaults.NormalizeActionName(row.Action)] =
                new ObservableCollection<string>(ParseKeyMapKeys(row.KeysText));
        }

        _keyMapLibrary.ActiveProfileId = ActiveKeyMapProfile.Id;
        await _keyMapStorageService.SaveAsync(_keyMapLibrary);
        RefreshKeyMapRows();
        RefreshActiveKeyMapResolver();
        SetStatus("キーマップ設定を保存しました。");
    }

    private async void ResetKeyMap_Click(object sender, RoutedEventArgs e)
    {
        ActiveKeyMapProfile.Bindings = KeyMapDefaults.CreateDefaultBindings();
        RefreshKeyMapRows();
        await _keyMapStorageService.SaveAsync(_keyMapLibrary);
        RefreshActiveKeyMapResolver();
        SetStatus("キーマップ設定をデフォルトに戻しました。");
    }

    private void AddKeyMapBinding_Click(object sender, RoutedEventArgs e)
    {
        var row = new KeyMapBindingRow(CreateNewKeyMapActionName(), string.Empty);
        KeyMapBindingRows.Add(row);
        SelectedKeyMapBindingRow = row;
        KeyMapGrid.ScrollIntoView(row);
        KeyMapGrid.Focus();
    }

    private void DeleteKeyMapBinding_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedKeyMapBindingRow is null)
        {
            return;
        }

        var index = KeyMapBindingRows.IndexOf(SelectedKeyMapBindingRow);
        KeyMapBindingRows.Remove(SelectedKeyMapBindingRow);
        SelectedKeyMapBindingRow = KeyMapBindingRows.Count == 0
            ? null
            : KeyMapBindingRows[Math.Min(index, KeyMapBindingRows.Count - 1)];
    }

    private void KeyMapKeyTextBox_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (sender is not TextBox textBox
            || !TryGetPhysicalKeyName(e, out var keyName))
        {
            return;
        }

        textBox.Text = keyName;
        textBox.CaretIndex = textBox.Text.Length;
        e.Handled = true;
    }

    private string CreateNewKeyMapActionName()
    {
        for (var index = 1; index < 1000; index++)
        {
            var candidate = $"input{index}";
            if (!KeyMapBindingRows.Any(row => string.Equals(
                    row.Action,
                    candidate,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"input{DateTime.Now:HHmmss}";
    }

    private static IReadOnlyList<string> ParseKeyMapKeys(string? text) =>
        (text ?? string.Empty)
            .Split(new[] { ',', '、' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int GetKeyMapSortOrder(string action)
    {
        var index = KeyMapDefaults.DefaultActions
            .Select((item, itemIndex) => new { item, itemIndex })
            .FirstOrDefault(item => string.Equals(
                item.item,
                action,
                StringComparison.OrdinalIgnoreCase))
            ?.itemIndex;
        return index ?? 1000;
    }

    private static bool TryGetPhysicalKeyName(
        KeyEventArgs e,
        out string keyName)
    {
        keyName = string.Empty;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl)
        {
            keyName = "Ctrl";
            return true;
        }

        if (key is Key.LeftShift or Key.RightShift)
        {
            keyName = "Shift";
            return true;
        }

        if (key is Key.LeftAlt or Key.RightAlt)
        {
            keyName = "Alt";
            return true;
        }

        if (key is >= Key.A and <= Key.Z)
        {
            keyName = key.ToString();
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            keyName = ((int)(key - Key.D0)).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (key is >= Key.F1 and <= Key.F24)
        {
            keyName = key.ToString();
            return true;
        }

        keyName = key switch
        {
            Key.Space => "Space",
            Key.Enter or Key.Return => "Enter",
            Key.Escape => "Esc",
            Key.Tab => "Tab",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(keyName);
    }

    private static string GetKeyMapActionDisplay(string action) =>
        action switch
        {
            "2" => "2 下",
            "4" => "4 左",
            "6" => "6 右",
            "8" => "8 上",
            "lp" => "lp 弱P",
            "mp" => "mp 中P",
            "hp" => "hp 強P",
            "lk" => "lk 弱K",
            "mk" => "mk 中K",
            "hk" => "hk 強K",
            _ => action
        };

    private static string GetKeyboardSendModeDisplay(
        KeyboardSendMode sendMode) =>
        sendMode == KeyboardSendMode.ScanCode
            ? "ScanCode方式"
            : "VirtualKey方式";

    private void AddNoteNode_Click(object sender, RoutedEventArgs e) =>
        AddNode(new ComboStep
        {
            NodeType = TimelineNodeType.Note,
            InputFrame = GetNextInputFrame()
        });

    private void AddComboCallNode_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null)
        {
            return;
        }

        var candidates = Library.Combos
            .Where(combo => combo.Id != SelectedCombo.Id)
            .ToArray();
        if (candidates.Length == 0)
        {
            SetStatus("呼び出せる別のコンボがありません。");
            return;
        }

        var dialog = new ComboPickerDialog(candidates)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true || dialog.SelectedCombo is null)
        {
            return;
        }

        if (TimelineService.HasReferenceCycle(
                Library,
                SelectedCombo.Id,
                dialog.SelectedCombo.Id))
        {
            MessageBox.Show(
                this,
                "この呼び出しを追加するとコンボ参照がループするため、追加できません。",
                "ループ参照",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        AddNode(new ComboStep
        {
            NodeType = TimelineNodeType.ComboCall,
            InputFrame = GetNextInputFrame(),
            CalledComboId = dialog.SelectedCombo.Id,
            CalledComboName = dialog.SelectedCombo.ComboName
        });
    }

    private void AddNode(ComboStep node)
    {
        if (SelectedCombo is null)
        {
            return;
        }

        SelectedCombo.Steps.Add(node);
        SelectedStep = node;
        TimelineGrid.ScrollIntoView(node);
        TimelineGrid.Focus();
    }

    private void ShowExpandedTimeline_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null)
        {
            return;
        }

        var dialog = new TextPreviewDialog(
            _shareTextService.Create(SelectedCombo, Library))
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private int GetNextInputFrame() =>
        SelectedCombo is null
            ? 0
            : TimelineService.GetNextInputFrame(SelectedCombo.Steps);

    private void DeleteNode_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null || SelectedStep is null)
        {
            return;
        }

        var index = SelectedCombo.Steps.IndexOf(SelectedStep);
        SelectedCombo.Steps.Remove(SelectedStep);
        SelectedStep = SelectedCombo.Steps.Count == 0
            ? null
            : SelectedCombo.Steps[Math.Min(index, SelectedCombo.Steps.Count - 1)];
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e) =>
        MoveSelectedNode(-1);

    private void MoveStepDown_Click(object sender, RoutedEventArgs e) =>
        MoveSelectedNode(1);

    private void MoveSelectedNode(int offset)
    {
        if (SelectedCombo is null || SelectedStep is null)
        {
            return;
        }

        var oldIndex = SelectedCombo.Steps.IndexOf(SelectedStep);
        var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= SelectedCombo.Steps.Count)
        {
            return;
        }

        SelectedCombo.Steps.Move(oldIndex, newIndex);
        TimelineGrid.SelectedItem = SelectedStep;
        TimelineGrid.ScrollIntoView(SelectedStep);
    }

    private void AttachLibraryEvents(ComboLibrary library)
    {
        library.Combos.CollectionChanged += Combos_CollectionChanged;
        library.ActionDefinitions.CollectionChanged +=
            ActionDefinitions_CollectionChanged;
        foreach (var combo in library.Combos)
        {
            combo.PropertyChanged += Combo_PropertyChanged;
        }

        foreach (var definition in library.ActionDefinitions)
        {
            definition.PropertyChanged += ActionDefinition_PropertyChanged;
        }
    }

    private void DetachLibraryEvents(ComboLibrary library)
    {
        library.Combos.CollectionChanged -= Combos_CollectionChanged;
        library.ActionDefinitions.CollectionChanged -=
            ActionDefinitions_CollectionChanged;
        foreach (var combo in library.Combos)
        {
            combo.PropertyChanged -= Combo_PropertyChanged;
        }

        foreach (var definition in library.ActionDefinitions)
        {
            definition.PropertyChanged -= ActionDefinition_PropertyChanged;
        }
    }

    private void Combos_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ComboRecipe combo in e.OldItems)
            {
                combo.PropertyChanged -= Combo_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ComboRecipe combo in e.NewItems)
            {
                combo.PropertyChanged += Combo_PropertyChanged;
            }
        }
    }

    private void ActionDefinitions_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ActionDefinition definition in e.OldItems)
            {
                definition.PropertyChanged -= ActionDefinition_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ActionDefinition definition in e.NewItems)
            {
                definition.PropertyChanged += ActionDefinition_PropertyChanged;
            }
        }

        RefreshActionNames();
    }

    private void ActionDefinition_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActionDefinition.ActionName))
        {
            RefreshActionNames();
        }
    }

    private void RefreshActionNames()
    {
        var names = Library.ActionDefinitions
            .Select(definition => definition.ActionName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        ActionNames.Clear();
        foreach (var name in names)
        {
            ActionNames.Add(name!);
        }

        TimelineGrid?.Items.Refresh();
    }

    private void Combo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ComboRecipe combo
            && e.PropertyName == nameof(ComboRecipe.ComboName))
        {
            foreach (var node in Library.Combos
                         .SelectMany(item => item.Steps)
                         .Where(node => node.CalledComboId == combo.Id))
            {
                node.CalledComboName = combo.ComboName;
            }
        }
    }

    private void RefreshCalledComboNames()
    {
        var names = Library.Combos.ToDictionary(
            combo => combo.Id,
            combo => combo.ComboName);
        foreach (var node in Library.Combos.SelectMany(combo => combo.Steps))
        {
            if (node.CalledComboId is Guid calledId
                && names.TryGetValue(calledId, out var name))
            {
                node.CalledComboName = name;
            }
        }
    }

    private void ComboList_PreviewMouseRightButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(
            e.OriginalSource as DependencyObject);
        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void SetStatus(string message) => StatusText.Text = message;

    private void ShowError(string message, Exception exception)
    {
        MessageBox.Show(
            this,
            $"{message}\n\n{exception.Message}",
            "エラー",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        SetStatus(message);
    }

    protected override void OnClosed(EventArgs e)
    {
        _testPlaybackCancellation?.Cancel();
        CloseInputNodeEditorWindow();
        try
        {
            _testPlaybackService.ReleaseAllKeys();
        }
        catch
        {
            // 終了処理では、キー解放失敗による二次クラッシュを防ぎます。
        }
        base.OnClosed(e);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record KeyboardSendModeOption(
    string DisplayName,
    KeyboardSendMode Value);

public sealed record InputTimelineTick(double Left, string Label);

public sealed class InputTimelineDragState
{
    public InputTimelineDragState(
        InputNodeRow row,
        double startX,
        int startFrame,
        int startDurationFrames,
        bool isResize)
    {
        Row = row;
        StartX = startX;
        StartFrame = startFrame;
        StartDurationFrames = startDurationFrames;
        GrabOffsetX = Math.Max(
            0,
            startX - MainWindow.InputTimelineFrameToLeft(startFrame));
        TargetFrame = startFrame;
        TargetDurationFrames = startDurationFrames;
        IsResize = isResize;
    }

    public InputNodeRow Row { get; }

    public double StartX { get; }

    public double GrabOffsetX { get; }

    public int StartFrame { get; }

    public int StartDurationFrames { get; }

    public int TargetFrame { get; set; }

    public int TargetDurationFrames { get; set; }

    public bool IsResize { get; }

}

public sealed class InputNodeRow : INotifyPropertyChanged, IDisposable
{
    private readonly Func<KeyMapProfile> _getKeyMapProfile;
    private readonly InputNotationParser _parser = new();

    public InputNodeRow(
        Models.InputEvent inputEvent,
        int lane,
        Func<KeyMapProfile> getKeyMapProfile)
    {
        InputEvent = inputEvent;
        Lane = lane;
        _getKeyMapProfile = getKeyMapProfile;
        InputEvent.PropertyChanged += InputEvent_PropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Models.InputEvent InputEvent { get; }

    public int Lane { get; }

    public int DisplayFrame
    {
        get => InputEvent.DisplayFrame;
        set
        {
            InputEvent.DisplayFrame = value;
            Refresh();
        }
    }

    public int InternalFrame
    {
        get => InputEvent.Frame;
        set
        {
            InputEvent.Frame = Math.Max(0, value);
            Refresh();
        }
    }

    public int DurationFrames
    {
        get => InputEvent.DurationFrames ?? 1;
        set
        {
            InputEvent.DurationFrames = Math.Max(1, value);
            Refresh();
        }
    }

    public string InputsText
    {
        get => InputEvent.LogicalInputsText;
        set
        {
            InputEvent.LogicalInputsText = value;
            Refresh();
        }
    }

    public string DisplayTitle =>
        $"[{DisplayFrame}F] {InputNodeEditorLogic.FormatNode(InputEvent)}";

    public double TimelineLeft =>
        MainWindow.InputTimelineFrameToLeft(InputEvent.Frame);

    public double TimelineWidth =>
        MainWindow.InputTimelineFramesToWidth(DurationFrames);

    public double TimelineTop =>
        MainWindow.InputTimelineTopPadding
        + Lane * MainWindow.InputTimelineLaneHeight;

    public double TimelineLabelWidth =>
        Math.Max(
            TimelineWidth,
            24 + TimelineText.Length * 9);

    public string TimelineText =>
        InputNodeEditorLogic.FormatNode(InputEvent);

    public string PhysicalPreview
    {
        get
        {
            var parseResult = _parser.ParseMany(InputEvent.LogicalInputs);
            var resolveResult =
                new KeyMapResolver(_getKeyMapProfile()).Resolve(
                    parseResult.Tokens);
            if (parseResult.Errors.Count > 0 || resolveResult.Errors.Count > 0)
            {
                return "物理: 未設定";
            }

            return resolveResult.Keys.Count == 0
                ? "物理: なし"
                : "物理: "
                  + string.Join(
                      " + ",
                      resolveResult.Keys.Select(key => key.DisplayName));
        }
    }

    public string WarningText
    {
        get
        {
            var parseResult = _parser.ParseMany(InputEvent.LogicalInputs);
            var resolveResult =
                new KeyMapResolver(_getKeyMapProfile()).Resolve(
                    parseResult.Tokens);
            var warnings = parseResult.Errors
                .Select(error => error.Message)
                .Concat(resolveResult.Errors.Select(error => error.Message))
                .ToArray();
            return warnings.Length == 0
                ? string.Empty
                : "警告: " + string.Join(" / ", warnings);
        }
    }

    public void Dispose() =>
        InputEvent.PropertyChanged -= InputEvent_PropertyChanged;

    public void Refresh()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(DisplayFrame)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(InputsText)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(DurationFrames)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(DisplayTitle)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(TimelineLeft)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(TimelineWidth)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(TimelineTop)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(TimelineLabelWidth)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(TimelineText)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(PhysicalPreview)));
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(WarningText)));
    }

    private void InputEvent_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e) =>
        Refresh();
}

public enum PressDurationKind
{
    Frames1,
    Frames2,
    Frames3,
    Frames5,
    CustomMilliseconds
}

public sealed record PressDurationOption(
    string DisplayName,
    PressDurationKind Value);

public sealed record StartDelayOption(
    string DisplayName,
    int Seconds);

public sealed class KeyMapBindingRow : INotifyPropertyChanged
{
    private string _action;
    private string _keysText;

    public KeyMapBindingRow(
        string action,
        string keysText)
    {
        _action = action;
        _keysText = keysText;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Action
    {
        get => _action;
        set
        {
            if (_action == value)
            {
                return;
            }

            _action = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(Action)));
        }
    }

    public string KeysText
    {
        get => _keysText;
        set
        {
            if (_keysText == value)
            {
                return;
            }

            _keysText = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(KeysText)));
        }
    }
}

public sealed record PresentDropBehaviorOption(
    string DisplayName,
    PresentDropBehavior Value);

public sealed class UnregisteredActionWarningConverter : IMultiValueConverter
{
    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (values.Length < 2
            || values[0] is not ComboStep
            {
                NodeType: TimelineNodeType.Action
            } step
            || string.IsNullOrWhiteSpace(step.ActionName)
            || values[1] is not IEnumerable<ActionDefinition> definitions)
        {
            return string.Empty;
        }

        return definitions.Any(definition => string.Equals(
            definition.ActionName?.Trim(),
            step.ActionName.Trim(),
            StringComparison.CurrentCultureIgnoreCase))
            ? string.Empty
            : "未登録";
    }

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}
