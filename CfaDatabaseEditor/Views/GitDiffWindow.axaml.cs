using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CfaDatabaseEditor.Services;

namespace CfaDatabaseEditor.Views;

public partial class GitDiffWindow : Window
{
    private static readonly IBrush RemovedRowBg = new SolidColorBrush(Color.FromArgb(0x40, 0xC6, 0x28, 0x28));
    private static readonly IBrush AddedRowBg = new SolidColorBrush(Color.FromArgb(0x40, 0x2E, 0x7D, 0x32));
    private static readonly IBrush ModifiedRowBg = new SolidColorBrush(Color.FromArgb(0x28, 0x90, 0x90, 0x90));
    private static readonly IBrush RemovedCharBg = new SolidColorBrush(Color.FromArgb(0x90, 0xC6, 0x28, 0x28));
    private static readonly IBrush AddedCharBg = new SolidColorBrush(Color.FromArgb(0x90, 0x2E, 0x7D, 0x32));
    private static readonly IBrush CollapsedBg = new SolidColorBrush(Color.FromArgb(0x18, 0x80, 0x80, 0x80));
    private static readonly IBrush LineNumberBrush = new SolidColorBrush(Color.FromArgb(0xA0, 0x88, 0x88, 0x88));

    private const string MonoFont = "Consolas, Menlo, Courier New, monospace";

    public GitDiffWindow()
    {
        InitializeComponent();
    }

    public GitDiffWindow(string title, string leftLabel, string rightLabel, string leftText, string rightText) : this()
    {
        Title = $"Diff — {title}";
        HeaderLabel.Text = $"{leftLabel}  →  {rightLabel}";
        Render(leftText, rightText);
    }

    public static GitDiffWindow ForImage(string title, string leftLabel, string rightLabel, byte[]? leftBytes, byte[]? rightBytes)
    {
        var w = new GitDiffWindow();
        w.Title = $"Diff — {title}";
        w.HeaderLabel.Text = $"{leftLabel}  →  {rightLabel}";
        w.RenderImage(leftBytes, rightBytes);
        return w;
    }

    private void RenderImage(byte[]? leftBytes, byte[]? rightBytes)
    {
        TextScroller.IsVisible = false;
        ImagePanel.IsVisible = true;

        SetSideImage(leftBytes, LeftImage, LeftImageLabel, "(not in HEAD)");
        SetSideImage(rightBytes, RightImage, RightImageLabel, "(deleted)");

        if (leftBytes == null && rightBytes != null)
            StatusLabel.Text = "Image added.";
        else if (leftBytes != null && rightBytes == null)
            StatusLabel.Text = "Image deleted.";
        else if (leftBytes != null && rightBytes != null && leftBytes.SequenceEqual(rightBytes))
            StatusLabel.Text = "Image is identical between HEAD and working tree.";
        else
            StatusLabel.Text = "Image modified.";
    }

    private static void SetSideImage(byte[]? bytes, Image image, TextBlock label, string emptyMessage)
    {
        if (bytes == null || bytes.Length == 0)
        {
            label.Text = emptyMessage;
            image.Source = null;
            return;
        }

        try
        {
            using var ms = new MemoryStream(bytes);
            var bmp = new Bitmap(ms);
            image.Source = bmp;
            label.Text = $"{bmp.PixelSize.Width}×{bmp.PixelSize.Height}, {bytes.Length:N0} bytes";
        }
        catch (Exception ex)
        {
            label.Text = $"(failed to decode: {ex.Message})";
            image.Source = null;
        }
    }

    private void Render(string leftText, string rightText)
    {
        var rows = DiffService.ComputeSideBySide(leftText, rightText, contextLines: 3, maxRows: 5000);

        if (rows.Count == 0)
        {
            // Either there were no changes, or the diff exceeded the cap.
            // Distinguish the two by checking input.
            if (leftText == rightText)
            {
                StatusLabel.Text = "No changes between HEAD and working tree.";
            }
            else
            {
                StatusLabel.Text = "Diff is too large to render (over 5000 changed lines).";
            }
            return;
        }

        DiffGrid.RowDefinitions.Clear();
        for (int i = 0; i < rows.Count; i++)
            DiffGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < rows.Count; i++)
            AddRow(i, rows[i]);

        StatusLabel.Text = $"{rows.Count} rows displayed.";
    }

    private void AddRow(int rowIndex, DiffRow row)
    {
        if (row.Kind == DiffOpKind.Collapsed)
        {
            var collapsed = new Border
            {
                Background = CollapsedBg,
                Padding = new Thickness(8, 2),
                Child = new TextBlock
                {
                    Text = $"⋯ {row.CollapsedCount} unchanged line(s) collapsed",
                    FontSize = 11,
                    FontStyle = FontStyle.Italic,
                    Foreground = LineNumberBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            Grid.SetRow(collapsed, rowIndex);
            Grid.SetColumn(collapsed, 0);
            Grid.SetColumnSpan(collapsed, 4);
            DiffGrid.Children.Add(collapsed);
            return;
        }

        var leftBg = SideBackground(row.Kind, isLeft: true);
        var rightBg = SideBackground(row.Kind, isLeft: false);

        AddCell(rowIndex, 0, BuildLineNumber(row.LeftLineNumber), leftBg, monospace: true);
        AddCell(rowIndex, 1, BuildContent(row.LeftText, row.LeftSpans, isLeft: true), leftBg, monospace: true);
        AddCell(rowIndex, 2, BuildLineNumber(row.RightLineNumber), rightBg, monospace: true);
        AddCell(rowIndex, 3, BuildContent(row.RightText, row.RightSpans, isLeft: false), rightBg, monospace: true);
    }

    private static IBrush SideBackground(DiffOpKind kind, bool isLeft)
    {
        return kind switch
        {
            DiffOpKind.Removed => isLeft ? RemovedRowBg : Brushes.Transparent,
            DiffOpKind.Added => isLeft ? Brushes.Transparent : AddedRowBg,
            DiffOpKind.Modified => ModifiedRowBg,
            _ => Brushes.Transparent
        };
    }

    private void AddCell(int rowIndex, int col, Control content, IBrush background, bool monospace)
    {
        var border = new Border
        {
            Background = background,
            Padding = new Thickness(6, 1),
            Child = content
        };
        Grid.SetRow(border, rowIndex);
        Grid.SetColumn(border, col);
        DiffGrid.Children.Add(border);
    }

    private static TextBlock BuildLineNumber(int? n)
    {
        return new TextBlock
        {
            Text = n.HasValue ? n.Value.ToString() : "",
            FontFamily = new FontFamily(MonoFont),
            FontSize = 12,
            Foreground = LineNumberBrush,
            TextAlignment = TextAlignment.Right
        };
    }

    private static TextBlock BuildContent(string? text, IReadOnlyList<CharSpan>? spans, bool isLeft)
    {
        var tb = new TextBlock
        {
            FontFamily = new FontFamily(MonoFont),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        };

        if (text == null)
        {
            tb.Text = "";
            return tb;
        }

        if (spans == null || spans.Count == 0)
        {
            tb.Text = text;
            return tb;
        }

        var charBg = isLeft ? RemovedCharBg : AddedCharBg;
        foreach (var span in spans)
        {
            if (span.Length <= 0) continue;
            var run = new Run(text.Substring(span.Start, span.Length));
            if (span.Changed) run.Background = charBg;
            tb.Inlines!.Add(run);
        }
        return tb;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
