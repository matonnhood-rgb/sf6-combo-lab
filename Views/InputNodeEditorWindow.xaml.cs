using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using ComboLab.Models;

namespace ComboLab.Views;

public partial class InputNodeEditorWindow : Window, INotifyPropertyChanged
{
    private readonly Action _requestTestPlayback;
    private KeyMapProfile _activeKeyMapProfile;

    public InputNodeEditorWindow(
        ActionDefinition actionDefinition,
        Func<KeyMapProfile> getKeyMapProfile,
        Action requestTestPlayback)
    {
        ActionDefinition = actionDefinition;
        _activeKeyMapProfile = getKeyMapProfile();
        _requestTestPlayback = requestTestPlayback;
        InitializeComponent();
        DataContext = this;
        ActionDefinition.PropertyChanged += ActionDefinition_PropertyChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ActionDefinition ActionDefinition { get; }

    public KeyMapProfile ActiveKeyMapProfile
    {
        get => _activeKeyMapProfile;
        set
        {
            _activeKeyMapProfile = value;
            OnPropertyChanged();
        }
    }

    public string ActionNameText => $"入力ノード編集: {ActionDefinition.ActionName}";

    public void SetEditingEnabled(bool enabled) =>
        TimelineEditor.SetEditingEnabled(enabled);

    public void CommitEditing() =>
        TimelineEditor.CommitEditing();

    private void TimelineEditor_TestPlaybackRequested(object sender, EventArgs e) =>
        _requestTestPlayback();

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CommitEditing();
        Close();
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

    protected override void OnClosed(EventArgs e)
    {
        ActionDefinition.PropertyChanged -= ActionDefinition_PropertyChanged;
        base.OnClosed(e);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
