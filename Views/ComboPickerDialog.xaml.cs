using System.Windows;
using ComboLab.Models;

namespace ComboLab.Views;

public partial class ComboPickerDialog : Window
{
    public ComboPickerDialog(IEnumerable<ComboRecipe> combos)
    {
        InitializeComponent();
        Combos = combos
            .OrderBy(combo => combo.ComboName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        DataContext = this;
    }

    public IReadOnlyList<ComboRecipe> Combos { get; }
    public ComboRecipe? SelectedCombo { get; set; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCombo is null)
        {
            MessageBox.Show(
                this,
                "呼び出すコンボを選択してください。",
                "選択確認",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }
}
