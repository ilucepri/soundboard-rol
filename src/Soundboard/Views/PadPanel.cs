using System.Windows;
using System.Windows.Controls;

namespace Soundboard.Views;

/// <summary>
/// La rejilla de pads. Reproduce el
/// <c>grid-template-columns: repeat(auto-fill, minmax(166px, 1fr))</c> del diseño: mete tantas
/// columnas como quepan y reparte el ancho sobrante entre ellas, en vez de dejar un hueco a la
/// derecha como haría un WrapPanel con anchos fijos.
/// </summary>
public sealed class PadPanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty =
        DependencyProperty.Register(nameof(MinItemWidth), typeof(double), typeof(PadPanel),
            new FrameworkPropertyMetadata(166d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(PadPanel),
            new FrameworkPropertyMetadata(112d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(PadPanel),
            new FrameworkPropertyMetadata(9d, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int count = InternalChildren.Count;
        if (count == 0) return new Size(0, 0);

        // Dentro de un ScrollViewer sin scroll horizontal el ancho llega acotado; si aun así viniera
        // infinito, nos quedamos con una sola columna en vez de dividir por infinito.
        double width = double.IsInfinity(availableSize.Width) ? MinItemWidth : availableSize.Width;

        int columns = ColumnsFor(width);
        double itemWidth = ItemWidthFor(width, columns);

        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(itemWidth, ItemHeight));

        int rows = (count + columns - 1) / columns;
        return new Size(width, rows * ItemHeight + (rows - 1) * Spacing);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int count = InternalChildren.Count;
        if (count == 0) return finalSize;

        int columns = ColumnsFor(finalSize.Width);
        double itemWidth = ItemWidthFor(finalSize.Width, columns);

        for (int i = 0; i < count; i++)
        {
            int column = i % columns;
            int row = i / columns;
            InternalChildren[i].Arrange(new Rect(
                column * (itemWidth + Spacing),
                row * (ItemHeight + Spacing),
                itemWidth,
                ItemHeight));
        }

        return finalSize;
    }

    int ColumnsFor(double width) =>
        Math.Max(1, (int)((width + Spacing) / (MinItemWidth + Spacing)));

    double ItemWidthFor(double width, int columns) =>
        Math.Max(MinItemWidth, (width - (columns - 1) * Spacing) / columns);
}
