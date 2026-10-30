using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FamilyCreator.Revit;

public partial class ModelPickerDialog : Window
{
    private sealed record ModelChoice(string Id, bool IsCurrent)
    {
        public string Name => DisplayName(Id);
    }
    private readonly ModelChoice[] choices;
    public string? SelectedModel { get; private set; }

    public ModelPickerDialog(Window owner, string[] models, string current, string? notice = null)
    {
        choices = models.Distinct(StringComparer.Ordinal).Select(id => new ModelChoice(id, id == current)).OrderByDescending(m => m.IsCurrent).ThenBy(m => m.Id, StringComparer.Ordinal).ToArray();
        InitializeComponent();
        Owner = owner;
        Resources.MergedDictionaries.Add(owner.Resources);
        FontFamily = owner.FontFamily;
        if (notice is not null) { CatalogNotice.Text = notice; CatalogNotice.Visibility = Visibility.Visible; }
        ModelList.ItemsSource = choices;
        ModelList.SelectedItem = choices.FirstOrDefault(m => m.IsCurrent);
        UpdateCount(); UpdateSelection();
        Loaded += (_, _) => ModelSearch.Focus();
    }

    public static string DisplayName(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "Choose a model";
        var name = Regex.Replace(id, @"-\d{4}-\d{2}-\d{2}$", "");
        if (!name.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase)) return name;
        var words = name[4..].Split('-');
        return "GPT-" + words[0] + (words.Length > 1 ? " " + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(string.Join(" ", words.Skip(1))) : "");
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (ModelList is null) return;
        var selected = ModelList.SelectedItem;
        var query = ModelSearch.Text.Trim();
        ModelList.ItemsSource = choices.Where(m => m.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (selected is not null && ModelList.Items.Contains(selected)) ModelList.SelectedItem = selected;
        SearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateCount(); UpdateSelection();
    }
    private void UpdateCount()
    {
        ModelCount.Text = $"{ModelList.Items.Count} models · available to your API key";
        EmptyModels.Visibility = ModelList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private string? Candidate => !string.IsNullOrWhiteSpace(ManualModel.Text) ? ManualModel.Text.Trim() : (ModelList.SelectedItem as ModelChoice)?.Id;
    private void UpdateSelection()
    {
        if (UseModelButton is null) return;
        var candidate = Candidate;
        UseModelButton.IsEnabled = candidate is not null && candidate.Length <= 150 && !candidate.Any(char.IsWhiteSpace);
        SelectionSummary.Text = DisplayName(candidate ?? "");
        SelectionSummary.ToolTip = candidate;
    }
    private void Model_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ModelList.SelectedItem is not null && ManualModel is not null) ManualModel.Clear();
        UpdateSelection();
    }
    private void Manual_Changed(object sender, TextChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ManualModel.Text)) ModelList.SelectedItem = null;
        UpdateSelection();
    }
    private void UseModel_Click(object sender, RoutedEventArgs e) => Select();
    private void Select() { if (UseModelButton.IsEnabled) { SelectedModel = Candidate; DialogResult = true; } }
    private void Model_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(ModelList, source) is ListBoxItem) Select();
    }
    private void Model_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; Select(); } }
}
