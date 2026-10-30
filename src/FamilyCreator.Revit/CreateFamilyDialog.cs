using FamilyCreator.Generation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FamilyCreator.Revit;

internal sealed class CreateFamilyDialog : Window
{
    public FamilyCreationOptions? Selection { get; private set; }
    public CreateFamilyDialog(Window owner, NativePreview preview, FamilyCategoryChoice[] categories, string template)
    {
        Owner = owner;
        Title = "Create family";
        Width = 480; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(247, 250, 255));
        Resources = owner.Resources;
        FontFamily = owner.FontFamily;
        FontSize = owner.FontSize;
        Foreground = owner.Foreground;
        var panel = new StackPanel { Margin = new Thickness(24), Background = Background };
        Content = panel;
        panel.Children.Add(new TextBlock { Text = "Create and load family", FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = $"Target: {preview.TargetTitle}\nNo separate RFA file will be saved.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
        void Label(string text) => panel.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 12, 0, 6) });
        Label("Category");
        var category = new ComboBox { ItemsSource = categories, DisplayMemberPath = "Name", SelectedItem = categories.FirstOrDefault(c => c.Name.Equals(preview.Recipe.Category, StringComparison.OrdinalIgnoreCase)) ?? categories.FirstOrDefault(c => c.Name == "Generic Models") ?? categories.FirstOrDefault() };
        System.Windows.Automation.AutomationProperties.SetName(category, "Family category");
        panel.Children.Add(category);
        Label("Family name");
        var name = new TextBox { Text = preview.Recipe.FamilyName, Padding = new Thickness(8), MaxLength = 120 };
        System.Windows.Automation.AutomationProperties.SetName(name, "Family name"); panel.Children.Add(name);
        Label("Type name");
        var type = new TextBox { Text = preview.Recipe.TypeName, Padding = new Thickness(8), MaxLength = 120 };
        System.Windows.Automation.AutomationProperties.SetName(type, "Type name"); panel.Children.Add(type);
        var templateBox = new TextBox { Text = template, TextWrapping = TextWrapping.Wrap, IsReadOnly = true, Margin = new Thickness(0, 8, 0, 8) };
        var browse = new Button { Content = "Choose Generic Model template…", Style = (Style)owner.FindResource("SecondaryButton") };
        browse.Click += (_, _) =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Revit family templates|*.rft", Title = "Choose a non-hosted Generic Model template" };
            if (picker.ShowDialog(this) == true) templateBox.Text = picker.FileName;
        };
        var templatePanel = new StackPanel(); templatePanel.Children.Add(templateBox); templatePanel.Children.Add(browse);
        panel.Children.Add(new Expander { Header = "Family template", Content = templatePanel, IsExpanded = template.Length == 0, Margin = new Thickness(0, 16, 0, 8) });
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick, Margin = new Thickness(0, 8, 0, 8) };
        panel.Children.Add(error);
        var create = new Button { Content = "Create and load", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        create.Click += (_, _) =>
        {
            try
            {
                if (category.SelectedItem is not FamilyCategoryChoice choice) throw new InvalidOperationException("Choose a category.");
                if (!System.IO.File.Exists(templateBox.Text)) throw new InvalidOperationException("Choose an installed Generic Model template.");
                Selection = new FamilyCreationOptions(choice.Id, FamilyRecipe.ValidateName(name.Text, "family name"), FamilyRecipe.ValidateName(type.Text, "type name"), templateBox.Text);
                DialogResult = true;
            }
            catch (Exception e) { error.Text = e.Message; }
        };
        panel.Children.Add(create);
    }
}
