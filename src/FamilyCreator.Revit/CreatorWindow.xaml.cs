using System.Windows;

namespace FamilyCreator.Revit;

public partial class CreatorWindow : Window
{
    public CreatorWindow() => InitializeComponent();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
