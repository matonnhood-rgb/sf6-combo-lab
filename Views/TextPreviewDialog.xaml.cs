using System.Windows;

namespace ComboLab.Views;

public partial class TextPreviewDialog : Window
{
    public TextPreviewDialog(string text)
    {
        InitializeComponent();
        PreviewTextBox.Text = text;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
