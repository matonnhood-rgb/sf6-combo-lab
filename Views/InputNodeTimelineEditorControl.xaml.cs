using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ComboLab.Models;
using ComboLab.Services;

namespace ComboLab.Views;

public partial class InputNodeTimelineEditorControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty ActionDefinitionProperty =
        DependencyProperty.Register(
            nameof(ActionDefinition),
            typeof(ActionDefinition),
            typeof(InputNodeTimelineEditorControl),
            new PropertyMetadata(null, OnActionDefinitionChanged));

    public static readonly DependencyProperty ActiveKeyMapProfileProperty =
        DependencyProperty.Register(
            nameof(ActiveKeyMapProfile),
            typeof(KeyMapProfile),
            typeof(InputNodeTimelineEditorControl),
            new PropertyMetadata(null, OnKeyMapProfileChanged));

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(
            nameof(IsCompact),
            typeof(bool),
            typeof(InputNodeTimelineEditorControl),
            new PropertyMetadata(false, OnCompactChanged));

    private const int MinimumTimelineFrameCapacity = 180;
    private const double TimelineLaneHeight = 42d;
    private const double TimelineTopPadding = 30d;

    private readonly InputNodeLaneLayoutService _laneLayoutService = new();
    private readonly InputNodePresetStorageService _presetStorageService = new();
    private bool _isSynchronizingInputSelection;
    private SharedInputNodeDragState? _inputTimelineDragState;
    private int _inputTimelineFrameCapacity = MinimumTimelineFrameCapacity;
    private double _pixelsPerFrame = 28d;
    private InputNodeTimelineRow? _selectedInputNodeRow;
    private Models.InputEvent? _selectedInputEvent;
    private InputNodePreset? _selectedPreset;
    private bool _isInputTimelineDragPreviewVisible;
    private double _inputTimelineDragPreviewLeft;
    private double _inputTimelineDragPreviewTop;
    private double _inputTimelineDragPreviewLabelTop;
    private double _inputTimelineDragPreviewWidth;
    private string _inputTimelineDragPreviewText = string.Empty;

    public InputNodeTimelineEditorControl()
    {
        InitializeComponent();
        Presets = _presetStorageService.Load();
        RefreshInputTimelineTicks();
        RefreshInputTimelineLaneLines();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? TestPlaybackRequested;

    public ActionDefinition? ActionDefinition
    {
        get => (ActionDefinition?)GetValue(ActionDefinitionProperty);
        set => SetValue(ActionDefinitionProperty, value);
    }

    public KeyMapProfile? ActiveKeyMapProfile
    {
        get => (KeyMapProfile?)GetValue(ActiveKeyMapProfileProperty);
        set => SetValue(ActiveKeyMapProfileProperty, value);
    }

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public Visibility FullVisibility =>
        IsCompact ? Visibility.Collapsed : Visibility.Visible;

    public double TimelineViewportHeight =>
        IsCompact ? 170d : double.NaN;

    public ObservableCollection<InputNodeTimelineRow> InputNodeRows { get; } = [];

    public ObservableCollection<InputTimelineTick> InputTimelineTicks { get; } = [];

    public ObservableCollection<InputTimelineLaneLine> InputTimelineLaneLines { get; } = [];

    public ObservableCollection<InputNodePreset> Presets { get; }

    public InputNodePreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value;
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
        + Math.Max(IsCompact ? 3 : 8, GetLaneCount()) * TimelineLaneHeight
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

    public void CommitEditing() =>
        CommitInputEventEditing();

    public void SetEditingEnabled(bool enabled)
    {
        EditorToolbar.IsEnabled = enabled;
        InputTimelineCanvas.IsEnabled = enabled;
        InputEventGrid.IsEnabled = enabled;
        RightPane.IsEnabled = enabled;
    }

    private static void OnActionDefinitionChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (InputNodeTimelineEditorControl)dependencyObject;
        control.DetachActionDefinition((ActionDefinition?)e.OldValue);
        control.AttachActionDefinition((ActionDefinition?)e.NewValue);
        control.SelectedInputEvent = null;
        control.EnsureInputTimelineCapacity(control.GetCurrentRequiredEndFrame());
        control.RefreshInputNodeRows();
    }

    private static void OnKeyMapProfileChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (InputNodeTimelineEditorControl)dependencyObject;
        foreach (var row in control.InputNodeRows)
        {
            row.Refresh();
        }
    }

    private static void OnCompactChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        var control = (InputNodeTimelineEditorControl)dependencyObject;
        control.OnPropertyChanged(nameof(FullVisibility));
        control.OnPropertyChanged(nameof(TimelineViewportHeight));
        control.OnPropertyChanged(nameof(InputTimelineHeight));
    }

    private void AttachActionDefinition(ActionDefinition? actionDefinition)
    {
        if (actionDefinition is null)
        {
            return;
        }

        actionDefinition.InputEvents.CollectionChanged += InputEvents_CollectionChanged;
        foreach (var inputEvent in actionDefinition.InputEvents)
        {
            inputEvent.PropertyChanged += InputEvent_PropertyChanged;
        }
    }

    private void DetachActionDefinition(ActionDefinition? actionDefinition)
    {
        if (actionDefinition is null)
        {
            return;
        }

        actionDefinition.InputEvents.CollectionChanged -= InputEvents_CollectionChanged;
        foreach (var inputEvent in actionDefinition.InputEvents)
        {
            inputEvent.PropertyChanged -= InputEvent_PropertyChanged;
        }
    }

    private void AddInputEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ActionDefinition is null)
        {
            return;
        }

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
    }

    private void DeleteInputEvent_Click(object sender, RoutedEventArgs e)
    {
        if (ActionDefinition is null)
        {
            return;
        }

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
    }

    private void DuplicateInputNode_Click(object sender, RoutedEventArgs e)
    {
        if (ActionDefinition is null || SelectedInputEvent is null)
        {
            return;
        }

        CommitInputEventEditing();
        var inputEvent = InputNodeEditorLogic.DuplicateNode(
            ActionDefinition.InputEvents,
            SelectedInputEvent);
        var insertIndex = ActionDefinition.InputEvents.IndexOf(SelectedInputEvent) + 1;
        ActionDefinition.InputEvents.Insert(insertIndex, inputEvent);
        SelectInputEvent(inputEvent);
        EnsureInputTimelineCapacity(inputEvent.Frame + (inputEvent.DurationFrames ?? 1));
        ScrollSelectedNodeIntoView();
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
    }

    private void SortInputNodesByFrame_Click(object sender, RoutedEventArgs e)
    {
        if (ActionDefinition is null)
        {
            return;
        }

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
    }

    private void TestPlayback_Click(object sender, RoutedEventArgs e)
    {
        CommitInputEventEditing();
        TestPlaybackRequested?.Invoke(this, EventArgs.Empty);
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

    private void AddPresetAtCurrentFrame_Click(object sender, RoutedEventArgs e)
    {
        if (ActionDefinition is null || SelectedPreset is null)
        {
            return;
        }

        var frame = SelectedInputEvent?.Frame
            ?? (ActionDefinition.InputEvents.Count == 0
                ? 0
                : ActionDefinition.InputEvents.Max(item =>
                    item.Frame + (item.DurationFrames ?? 1)));
        var inputEvent = CreateInputEventFromPreset(SelectedPreset, frame);
        ActionDefinition.InputEvents.Add(inputEvent);
        SelectInputEvent(inputEvent);
        EnsureInputTimelineCapacity(frame + (inputEvent.DurationFrames ?? 1));
        ScrollSelectedNodeIntoView();
    }

    private void SaveSelectedNodeAsPreset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedInputEvent is null)
        {
            return;
        }

        var preset = new InputNodePreset
        {
            Name = string.IsNullOrWhiteSpace(SelectedInputEvent.DisplayLabel)
                ? InputNodeEditorLogic.FormatNode(SelectedInputEvent)
                : SelectedInputEvent.DisplayLabel,
            DurationFrames = SelectedInputEvent.DurationFrames ?? 1,
            DisplayLabel = SelectedInputEvent.DisplayLabel,
            ColorTag = SelectedInputEvent.ColorTag,
            LogicalInputs = new ObservableCollection<string>(
                SelectedInputEvent.LogicalInputs),
            Markers = new ObservableCollection<InputEventMarker>(
                SelectedInputEvent.Markers.Select(CloneMarker))
        };
        Presets.Add(preset);
        SelectedPreset = preset;
        _presetStorageService.Save(Presets);
    }

    private void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPreset is null)
        {
            return;
        }

        Presets.Remove(SelectedPreset);
        SelectedPreset = Presets.FirstOrDefault();
        _presetStorageService.Save(Presets);
    }

    private static Models.InputEvent CreateInputEventFromPreset(
        InputNodePreset preset,
        int frame) =>
        new()
        {
            Frame = Math.Max(0, frame),
            DurationFrames = preset.DurationFrames,
            DisplayLabel = preset.DisplayLabel,
            ColorTag = preset.ColorTag,
            LogicalInputs = new ObservableCollection<string>(preset.LogicalInputs),
            Markers = new ObservableCollection<InputEventMarker>(
                preset.Markers.Select(CloneMarker))
        };

    private static InputEventMarker CloneMarker(InputEventMarker marker) =>
        new()
        {
            OffsetFrame = marker.OffsetFrame,
            Label = marker.Label,
            Kind = marker.Kind
        };

    private void InputTimelineCanvas_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (ActionDefinition is null || e.ClickCount < 2)
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
        _inputTimelineDragState = new SharedInputNodeDragState(
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
        _inputTimelineDragState = new SharedInputNodeDragState(
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
            CalculateFrameFromCursor(cursorX, _inputTimelineDragState.GrabOffsetX);
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
            CalculateDurationFromCursor(cursorX, _inputTimelineDragState.StartFrame);
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

    private void TimelineScrollViewer_RequestBringIntoView(
        object sender,
        RequestBringIntoViewEventArgs e)
    {
        if (_inputTimelineDragState is not null)
        {
            e.Handled = true;
        }
    }

    private void ShowInputTimelineDragPreview(SharedInputNodeDragState state)
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
        if (inputEvent is null || IsCompact)
        {
            return;
        }

        InputEventGrid.ScrollIntoView(inputEvent);
    }

    private void CommitInputEventEditing(bool refreshNodes = true)
    {
        UpdateFocusedBindingSource();
        if (!IsCompact)
        {
            InputEventGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            InputEventGrid.CommitEdit(DataGridEditingUnit.Row, true);
        }

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
        if (_inputTimelineDragState is not null)
        {
            return;
        }

        EnsureInputTimelineCapacity(GetCurrentRequiredEndFrame());
        RefreshInputNodeRows();
    }

    private void RefreshInputNodeRows()
    {
        var selected = SelectedInputEvent;
        foreach (var row in InputNodeRows)
        {
            row.Dispose();
        }

        InputNodeRows.Clear();
        if (ActionDefinition is not null)
        {
            var lanes = _laneLayoutService.AssignLanes(ActionDefinition.InputEvents);
            foreach (var inputEvent in ActionDefinition.InputEvents)
            {
                InputNodeRows.Add(new InputNodeTimelineRow(
                    inputEvent,
                    lanes.TryGetValue(inputEvent, out var lane) ? lane : 0,
                    () => PixelsPerFrame,
                    () => ActiveKeyMapProfile ?? KeyMapDefaults.CreateLibrary().Profiles[0]));
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
        var laneCount = Math.Max(IsCompact ? 3 : 8, GetLaneCount());
        for (var lane = 0; lane <= laneCount; lane++)
        {
            InputTimelineLaneLines.Add(new InputTimelineLaneLine(
                TimelineTopPadding + lane * TimelineLaneHeight));
        }
    }

    private int GetLaneCount() =>
        InputNodeRows.Count == 0 ? 1 : InputNodeRows.Max(row => row.Lane) + 1;

    private int GetCurrentRequiredEndFrame() =>
        ActionDefinition?.InputEvents.Count == 0
            ? MinimumTimelineFrameCapacity
            : ActionDefinition?.InputEvents.Max(item =>
                item.Frame + (item.DurationFrames ?? 1)) ?? MinimumTimelineFrameCapacity;

    private void EnsureInputTimelineCapacity(int requiredEndFrame)
    {
        var desired = Math.Max(MinimumTimelineFrameCapacity, requiredEndFrame + 30);
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
            TimelineScrollViewer.ScrollToHorizontalOffset(Math.Max(0, left - 80));
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class SharedInputNodeDragState
{
    public SharedInputNodeDragState(
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

public sealed record InputNodeMarkerRow(double Left, string Label, string Kind);

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

    public string DisplayLabel
    {
        get => InputEvent.DisplayLabel;
        set
        {
            InputEvent.DisplayLabel = value ?? string.Empty;
            Refresh();
        }
    }

    public string ColorTag
    {
        get => InputEvent.ColorTag;
        set
        {
            InputEvent.ColorTag = value ?? string.Empty;
            Refresh();
        }
    }

    public string DisplayTitle =>
        $"[{DisplayFrame}F / 保持{DurationFrames}F] {TimelineText}";

    public double TimelineLeft =>
        Math.Max(0, InputEvent.Frame) * _getPixelsPerFrame();

    public double TimelineWidth =>
        Math.Max(1, DurationFrames) * _getPixelsPerFrame();

    public double TimelineTop =>
        30d + Lane * 42d;

    public double TimelineLabelWidth =>
        Math.Max(TimelineWidth, 24 + TimelineText.Length * 9);

    public string TimelineText =>
        string.IsNullOrWhiteSpace(InputEvent.DisplayLabel)
            ? InputNodeEditorLogic.FormatNode(InputEvent)
            : InputEvent.DisplayLabel;

    public Brush NodeBrush => InputEvent.ColorTag.Trim().ToLowerInvariant() switch
    {
        "red" or "赤" => Brushes.MistyRose,
        "green" or "緑" => Brushes.Honeydew,
        "yellow" or "黄" => Brushes.LemonChiffon,
        _ => Brushes.LightBlue
    };

    public Brush BorderBrush =>
        WarningText.Length == 0 ? Brushes.RoyalBlue : Brushes.DarkOrange;

    public IReadOnlyList<InputNodeMarkerRow> MarkerRows =>
        InputEvent.Markers
            .Select(marker => new InputNodeMarkerRow(
                Math.Max(0, marker.OffsetFrame) * _getPixelsPerFrame(),
                marker.Label,
                marker.Kind))
            .ToArray();

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    private void InputEvent_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e) =>
        Refresh();
}
