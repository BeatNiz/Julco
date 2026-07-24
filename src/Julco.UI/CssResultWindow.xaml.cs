using System.Windows;

namespace Julco.UI;

public partial class CssResultWindow : Window
{
    public CssResultWindow(SelectorInspectionResultView css)
    {
        InitializeComponent();
        CssExplanationGrid.ItemsSource = css.Explanations;
        ComputedCssTextBox.Text = css.ComputedCss;
    }
}

public sealed record SelectorInspectionResultView(
    IReadOnlyList<CssExplanation> Explanations,
    string ComputedCss);
