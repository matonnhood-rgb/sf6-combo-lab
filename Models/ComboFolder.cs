using System.Collections.ObjectModel;

namespace ComboLab.Models;

public sealed class ComboFolder : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = "新しいフォルダー";

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ObservableCollection<ComboFolder> Children { get; set; } = [];
}
