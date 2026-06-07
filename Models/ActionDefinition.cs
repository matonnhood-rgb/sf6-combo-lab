using System.Collections.ObjectModel;

namespace ComboLab.Models;

public sealed class ActionDefinition : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _actionName = "新しいアクション";
    private string _description = string.Empty;
    private ObservableCollection<InputEvent> _inputEvents = [];

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string ActionName
    {
        get => _actionName;
        set => SetProperty(ref _actionName, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public ObservableCollection<InputEvent> InputEvents
    {
        get => _inputEvents;
        set => SetProperty(ref _inputEvents, value ?? []);
    }
}
