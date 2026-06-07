using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using ComboLab.Models;
using ComboLab.Services;

namespace ComboLab.Views;

public partial class InputNodeEditorWindow : Window, INotifyPropertyChanged
{
    private const int MinimumTimelineFrameCapacity = 180;
    private const double TimelineLaneHeight = 42d;
    private const double TimelineTopPadding = 30d;

    private readonly Func<KeyMapProfile> _getKeyMapProfile;
    private readonly Action _requestTestPlayback;
    private readonly InputNotationParser _parser = new();
    private Models.InputEvent? _selectedInputEvent;
    private InputNodeTimelineRow? _selectedInputNodeRow;
    private InputNodeWindowDragState? _inputTimelineDragState;
    private int _inputTimelineFrameCapacity = MinimumTimelineFrameCapacity;
    private double _pixelsPerFrame = 28d;
    private bool _isSynchronizingInputSelection;
    private bool _isInputTimelineDragPreviewVisible;
    private double _inputTimelineDragPreviewLeft;
    private double _inputTimelineDragPreviewTop;
    private double _inputTimelineDragPreviewLabelTop;
    private double _inputTimelineDragPreviewWidth;
    private string _inputTimelineDragPreviewText = string.Empty;
    private string _statusText = "ノードを選択、またはタイムラインをダブルクリックして追加できます。";

    public InputNodeEditorWindow(
        ActionDefinition actionDefinition,
        Func<KeyMapProfile> getKeyMapProfile,
        Action requestTestPlayback)
    {
        ActionDefinition = actionDefinition;
        _getKeyMapProfile = getKeyMapProfile;
        _requestTestPlayback = requestTestPlayback;
        InitializeComponent();
        DataContext = this;

        ActionDefinition.PropertyChanged += ActionDefinition_PropertyChanged;
        ActionDefinition.InputEvents.CollectionChanged += InputEvents_CollectionChanged;
        foreach (var inputEvent in ActionDefinition.InputEvents)
        {
            inputEvent.PropertyChanged += InputEvent_PropertyChanged;
        }

        EnsureInputTimelineCapacity(GetCurrentRequiredEndFrame());
        RefreshInputNodeRows();
        RefreshInputTimelineTicks();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ActionDefinition ActionDefinition { get; }

    public ObservableCollection<InputNodeTimelineRow> InputNodeRows { get; } = [];

    public ObservableCollection<InputTimelineTick> InputTimelineTicks { get; } = [];

    public ObservableCollection<InputTimelineLaneLine> InputTimelineLaneLines { get; } = [];

    public string ActionNameText => $"入力ノード編集: {ActionDefinition.ActionName}";

    public string StatusText
    {
        get => _statusText;
        private set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public double PixelsPerFrame
    {
        get => _pixelsPerFrame;
        set
        {
            _pixelsPerFrame = Math.Clamp(value, 12d, 64d);
            OnPropertyChanged();
            OnPropertyChanged(nameof(InputTimelineWidth));
            OnPropertyChanged(nameof(TimelineGridViewport));
            foreach (var row in InputNodeRows)
            {
                row.Refresh();
            }

            RefreshInputTimelineTicks();
            RefreshInputTimelineLaneLines();
        }
    }

    public double InputTimelineWidth =>
        _inputTimelineFrameCapacity * PixelsPerFrame;

    public double InputTimelineHeight =>
        TimelineTopPadding
        + Math.Max(8, InputNodeRows.Count) * TimelineLaneHeight
        + 30;

    public Rect TimelineGridViewport =>
        new(0, 0, PixelsPerFrame, TimelineLaneHeight);

    public Models.InputEvent? SelectedInputEvent
    {
        get => _selectedInputEvent;
        set
        {
            if (ReferenceEquals(_selectedInputEvent, value))
            {
                return;
            }

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

    public InputNodeTimelineRow? SelectedInputNodeRow
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
            OnPropertyChanged(nameof(SelectedNodeTitle));
            OnPropertyChanged(nameof(SelectedPhysicalPreview));
            OnPropertyChanged(nameof(SelectedWarningText));
            if (!_isSynchronizingInputSelection)
            {
                _isSynchronizingInputSelection = true;
                SelectedInputEvent = value?.InputEvent;
                _isSynchronizingInputSelection = false;
            }
        }
    }

    public string SelectedNodeTitle =>
        SelectedInputNodeRow?.DisplayTitle ?? "未選択";

    public string SelectedPhysicalPreview =>
        SelectedInputNodeRow?.PhysicalPreview ?? "物理: 未選択";

    public string SelectedWarningText =>
        SelectedInputNodeRow?.WarningText ?? string.Empty;

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

    public void SetEditingEnabled(bool enabled)
    {
        EditorToolbar.IsEnabled = enabled;
        InputTimelineCanvas.IsEnabled = enabled;
        InputEventGrid.IsEnabled = enabled;
        RightPane.IsEnabled = enabled;
    }

    private void AddInputEvent_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        var inputEvent = InputNodeEditorLogic.CreateNodeAfter(
            ActionDefinition.InputEvents,
            SelectedInputEvent);
        var insertIndex = SelectedInputEvent is null
            ? ActionDefinition.InputEvents.Count
            : ActionDefinition.InputEvents.IndexOf(SelectedInputEvent) + 1;
        ActionDefinition.InputEvents.Insert(
            Math.Clamp(insertIndex, 0, ActionDefinition.InputEvents.Count),
            inputEvent);
        SelectInputEvent(inputEvent);
        EnsureInputTimelineCapacity(inputEvent.Frame + (inputEvent.DurationFrames ?? 1));
        ScrollSelectedNodeIntoView();
        StatusText = $"{inputEvent.DisplayFrame}F にノードを追加しました。";
    }

    private void DeleteInputEvent_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        if (SelectedInputEvent is null)
        {
            return;
        }

        var index = ActionDefinition.InputEvents.IndexOf(SelectedInputEvent);
        ActionDefinition.InputEvents.Remove(SelectedInputEvent);
        SelectInputEvent(ActionDefinition.InputEvents.Count == 0
            ? null
            : ActionDefinition.InputEvents[
                Math.Min(index, ActionDefinition.InputEvents.Count - 1)]);
        StatusText = "ノードを削除しました。";
    }

    private void DuplicateInputNode_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        if (SelectedInputEvent is null)
        {
            return;
        }

        var inputEvent = InputNodeEditorLogic.DuplicateNode(
            ActionDefinition.InputEvents,
            SelectedInputEvent);
        var insertIndex = ActionDefinition.InputEvents.IndexOf(SelectedInputEvent) + 1;
        ActionDefinition.InputEvents.Insert(insertIndex, inputEvent);
        SelectInputEvent(inputEvent);
        EnsureInputTimelineCapacity(inputEvent.Frame + (inputEvent.DurationFrames ?? 1));
        ScrollSelectedNodeIntoView();
        StatusText = $"{inputEvent.DisplayFrame}F に複製しました。";
    }

    private void ClearInputNode_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        if (SelectedInputEvent is null)
        {
            return;
        }

        InputNodeEditorLogic.Clear(SelectedInputEvent);
        RefreshInputNodeRows();
        StatusText = "入力をクリアしました。";
    }

    private void SortInputNodesByFrame_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        var selected = SelectedInputEvent;
        var sorted = ActionDefinition.InputEvents
            .OrderBy(item => item.Frame)
            .ThenBy(item => ActionDefinition.InputEvents.IndexOf(item))
            .ToArray();
        ActionDefinition.InputEvents.Clear();
        foreach (var inputEvent in sorted)
        {
            ActionDefinition.InputEvents.Add(inputEvent);
        }

        SelectInputEvent(selected);
        ScrollSelectedNodeIntoView();
        StatusText = "F順に並べ替えました。";
    }

    private void TestPlayback_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        _requestTestPlayback();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        Close();
    }

    private void SetInputNodeDirection_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing(refreshNodes: false);
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
        CommitInputEventEditing(refreshNodes: false);
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

        CommitInputEventEditing();
        var frame = Math.Max(
            0,
            (int)Math.Round(
                e.GetPosition(InputTimelineCanvas).X / PixelsPerFrame,
                MidpointRounding.AwayFromZero));
        var inputEvent = InputNodeEditorLogic.CreateNode(frame);
        ActionDefinition.InputEvents.Add(inputEvent);
        SelectInputEvent(inputEvent);
        EnsureInputTimelineCapacity(frame + (inputEvent.DurationFrames ?? 1));
        ScrollSelectedNodeIntoView();
        e.Handled = true;
    }

    private void InputTimelineNode_SelectOnly(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: InputNodeTimelineRow row })
        {
            CommitInputEventEditing(refreshNodes: false);
            SelectInputEvent(row.InputEvent);
        }

        e.Handled = false;
    }

    private void InputTimelineNode_DragStarted(
        object sender,
        DragStartedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InputNodeTimelineRow row })
        {
            return;
        }

        CommitInputEventEditing(refreshNodes: false);
        SelectInputEvent(row.InputEvent);
        var cursorX = Mouse.GetPosition(InputTimelineCanvas).X;
        _inputTimelineDragState = new InputNodeWindowDragState(
            row,
            cursorX,
            row.InputEvent.Frame,
            row.InputEvent.DurationFrames ?? 1,
            false,
            PixelsPerFrame);
        ShowInputTimelineDragPreview(_inputTimelineDragState);
    }

    private void InputTimelineResize_DragStarted(
        object sender,
        DragStartedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: InputNodeTimelineRow row })
        {
            return;
        }

        CommitInputEventEditing(refreshNodes: false);
        SelectInputEvent(row.InputEvent);
        var cursorX = Mouse.GetPosition(InputTimelineCanvas).X;
        _inputTimelineDragState = new InputNodeWindowDragState(
            row,
            cursorX,
            row.InputEvent.Frame,
            row.InputEvent.DurationFrames ?? 1,
            true,
            PixelsPerFrame);
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
        EnsureInputTimelineCapacity(
            _inputTimelineDragState.TargetFrame
            + _inputTimelineDragState.TargetDurationFrames);
        ShowInputTimelineDragPreview(_inputTimelineDragState);
        ScrollTimelineNearViewportEdge();
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
        EnsureInputTimelineCapacity(
            _inputTimelineDragState.StartFrame
            + _inputTimelineDragState.TargetDurationFrames);
        ShowInputTimelineDragPreview(_inputTimelineDragState);
        ScrollTimelineNearViewportEdge();
    }

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
                StatusText = $"保持 {_inputTimelineDragState.TargetDurationFrames}F に変更しました。";
            }
            else
            {
                _inputTimelineDragState.Row.InternalFrame =
                    _inputTimelineDragState.TargetFrame;
                StatusText = $"{_inputTimelineDragState.TargetFrame + 1}F へ移動しました。";
            }
        }

        _inputTimelineDragState = null;
        HideInputTimelineDragPreview();
        RefreshInputNodeRows();
    }

    private void TimelineScrollViewer_RequestBringIntoView(
        object sender,
        RequestBringIntoViewEventArgs e)
    {
        if (_inputTimelineDragState is not null)
        {
            e.Handled = true;
        }
    }

    private void ShowInputTimelineDragPreview(InputNodeWindowDragState state)
    {
        InputTimelineDragPreviewLeft = FrameToLeft(state.TargetFrame);
        InputTimelineDragPreviewTop = state.Row.TimelineTop;
        InputTimelineDragPreviewLabelTop = Math.Max(0, state.Row.TimelineTop - 24);
        InputTimelineDragPreviewWidth = FramesToWidth(state.TargetDurationFrames);
        InputTimelineDragPreviewText = state.IsResize
            ? $"保持 {state.TargetDurationFrames}F"
            : $"{state.TargetFrame + 1}F";
        IsInputTimelineDragPreviewVisible = true;
    }

    private void HideInputTimelineDragPreview() =>
        IsInputTimelineDragPreviewVisible = false;

    private void ScrollTimelineNearViewportEdge()
    {
        if (_inputTimelineDragState is null)
        {
            return;
        }

        var cursor = Mouse.GetPosition(TimelineScrollViewer);
        const double edge = 36d;
        const double step = 24d;

        if (cursor.X < edge)
        {
            TimelineScrollViewer.ScrollToHorizontalOffset(
                Math.Max(0, TimelineScrollViewer.HorizontalOffset - step));
        }
        else if (cursor.X > TimelineScrollViewer.ViewportWidth - edge)
        {
            TimelineScrollViewer.ScrollToHorizontalOffset(
                TimelineScrollViewer.HorizontalOffset + step);
        }

        if (cursor.Y < edge)
        {
            TimelineScrollViewer.ScrollToVerticalOffset(
                Math.Max(0, TimelineScrollViewer.VerticalOffset - step));
        }
        else if (cursor.Y > TimelineScrollViewer.ViewportHeight - edge)
        {
            TimelineScrollViewer.ScrollToVerticalOffset(
                TimelineScrollViewer.VerticalOffset + step);
        }
    }

    private int CalculateFrameFromCursor(double cursorX, double grabOffsetX) =>
        Math.Max(
            0,
            (int)Math.Round(
                (cursorX - grabOffsetX) / PixelsPerFrame,
                MidpointRounding.AwayFromZero));

    private int CalculateDurationFromCursor(double cursorX, int startFrame) =>
        Math.Max(
            1,
            (int)Math.Round(
                (cursorX - FrameToLeft(startFrame)) / PixelsPerFrame,
                MidpointRounding.AwayFromZero));

    private double FrameToLeft(int frame) =>
        Math.Max(0, frame) * PixelsPerFrame;

    private double FramesToWidth(int frames) =>
        Math.Max(1, frames) * PixelsPerFrame;

    private void SelectInputEvent(Models.InputEvent? inputEvent)
    {
        SelectedInputEvent = inputEvent;
        if (inputEvent is null)
        {
            return;
        }

        InputEventGrid.ScrollIntoView(inputEvent);
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
            comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
            comboBox.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateSource();
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

        EnsureInputTimelineCapacity(GetCurrentRequiredEndFrame());
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
            if (_inputTimelineDragState is null)
            {
                EnsureInputTimelineCapacity(GetCurrentRequiredEndFrame());
                RefreshInputNodeRows();
            }
        }
    }

    private void ActionDefinition_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ActionDefinition.ActionName))
        {
            OnPropertyChanged(nameof(ActionNameText));
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
        foreach (var inputEvent in ActionDefinition.InputEvents)
        {
            InputNodeRows.Add(new InputNodeTimelineRow(
                inputEvent,
                InputNodeRows.Count,
                () => PixelsPerFrame,
                () => _getKeyMapProfile()));
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
        RefreshInputTimelineLaneLines();
        OnPropertyChanged(nameof(InputTimelineWidth));
        OnPropertyChanged(nameof(InputTimelineHeight));
    }

    private void RefreshInputTimelineTicks()
    {
        InputTimelineTicks.Clear();
        for (var frame = 0; frame <= _inputTimelineFrameCapacity; frame++)
        {
            var displayFrame = frame + 1;
            InputTimelineTicks.Add(new InputTimelineTick(
                FrameToLeft(frame),
                displayFrame == 1 || displayFrame % 5 == 0
                    ? $"{displayFrame}F"
                    : string.Empty));
        }
    }

    private void RefreshInputTimelineLaneLines()
    {
        InputTimelineLaneLines.Clear();
        var laneCount = Math.Max(8, InputNodeRows.Count);
        for (var lane = 0; lane <= laneCount; lane++)
        {
            InputTimelineLaneLines.Add(new InputTimelineLaneLine(
                TimelineTopPadding + lane * TimelineLaneHeight));
        }
    }

    private int GetCurrentRequiredEndFrame() =>
        ActionDefinition.InputEvents.Count == 0
            ? MinimumTimelineFrameCapacity
            : ActionDefinition.InputEvents.Max(item =>
                item.Frame + (item.DurationFrames ?? 1));

    private void EnsureInputTimelineCapacity(int requiredEndFrame)
    {
        var desired = Math.Max(
            MinimumTimelineFrameCapacity,
            requiredEndFrame + 30);
        if (desired <= _inputTimelineFrameCapacity)
        {
            return;
        }

        _inputTimelineFrameCapacity = desired;
        RefreshInputTimelineMetrics();
    }

    private void ScrollSelectedNodeIntoView()
    {
        if (SelectedInputEvent is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            var left = FrameToLeft(SelectedInputEvent.Frame);
            TimelineScrollViewer.ScrollToHorizontalOffset(
                Math.Max(0, left - 80));
        });
    }

    protected override void OnClosed(EventArgs e)
    {
        ActionDefinition.PropertyChanged -= ActionDefinition_PropertyChanged;
        ActionDefinition.InputEvents.CollectionChanged -= InputEvents_CollectionChanged;
        foreach (var inputEvent in ActionDefinition.InputEvents)
        {
            inputEvent.PropertyChanged -= InputEvent_PropertyChanged;
        }

        foreach (var row in InputNodeRows)
        {
            row.Dispose();
        }

        base.OnClosed(e);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class InputNodeWindowDragState
{
    public InputNodeWindowDragState(
        InputNodeTimelineRow row,
        double startX,
        int startFrame,
        int startDurationFrames,
        bool isResize,
        double pixelsPerFrame)
    {
        Row = row;
        StartFrame = startFrame;
        StartDurationFrames = startDurationFrames;
        GrabOffsetX = Math.Max(0, startX - startFrame * pixelsPerFrame);
        TargetFrame = startFrame;
        TargetDurationFrames = startDurationFrames;
        IsResize = isResize;
    }

    public InputNodeTimelineRow Row { get; }

    public int StartFrame { get; }

    public int StartDurationFrames { get; }

    public double GrabOffsetX { get; }

    public int TargetFrame { get; set; }

    public int TargetDurationFrames { get; set; }

    public bool IsResize { get; }
}

public sealed record InputTimelineLaneLine(double Top);

public sealed class InputNodeTimelineRow : INotifyPropertyChanged, IDisposable
{
    private readonly Func<double> _getPixelsPerFrame;
    private readonly Func<KeyMapProfile> _getKeyMapProfile;
    private readonly InputNotationParser _parser = new();

    public InputNodeTimelineRow(
        Models.InputEvent inputEvent,
        int lane,
        Func<double> getPixelsPerFrame,
        Func<KeyMapProfile> getKeyMapProfile)
    {
        InputEvent = inputEvent;
        Lane = lane;
        _getPixelsPerFrame = getPixelsPerFrame;
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
        $"[{DisplayFrame}F / 保持{DurationFrames}F] {InputNodeEditorLogic.FormatNode(InputEvent)}";

    public double TimelineLeft =>
        Math.Max(0, InputEvent.Frame) * _getPixelsPerFrame();

    public double TimelineWidth =>
        Math.Max(1, DurationFrames) * _getPixelsPerFrame();

    public double TimelineTop =>
        30d + Lane * 42d;

    public double TimelineLabelWidth =>
        Math.Max(TimelineWidth, 24 + TimelineText.Length * 9);

    public string TimelineText =>
        InputNodeEditorLogic.FormatNode(InputEvent);

    public string PhysicalPreview
    {
        get
        {
            var parseResult = _parser.ParseMany(InputEvent.LogicalInputs);
            var resolveResult =
                new KeyMapResolver(_getKeyMapProfile()).Resolve(parseResult.Tokens);
            if (parseResult.Errors.Count > 0 || resolveResult.Errors.Count > 0)
            {
                return "物理: 未設定";
            }

            return resolveResult.Keys.Count == 0
                ? "物理: なし"
                : "物理: " + string.Join(
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
                new KeyMapResolver(_getKeyMapProfile()).Resolve(parseResult.Tokens);
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayFrame)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InternalFrame)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationFrames)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InputsText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimelineLeft)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimelineWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimelineTop)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimelineLabelWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimelineText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PhysicalPreview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WarningText)));
    }

    private void InputEvent_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e) =>
        Refresh();
}
