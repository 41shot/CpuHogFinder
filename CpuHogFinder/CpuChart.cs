using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CpuHogFinder;

public sealed class CpuChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double?>), typeof(CpuChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double?>? Values
    {
        get => (IReadOnlyList<double?>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 80 || height < 50)
            return;

        var textBrush = new SolidColorBrush(Color.FromRgb(96, 111, 130));
        var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(221, 229, 238)), 1);
        var linePen = new Pen(new SolidColorBrush(Color.FromRgb(0, 129, 128)), 2);
        double left = 55, top = 14, right = width - 12, bottom = height - 24;
        double max = Math.Max(100, Values?.Where(value => value.HasValue).Select(value => value.GetValueOrDefault()).DefaultIfEmpty(0).Max() ?? 0);
        max = Math.Ceiling(max / 100) * 100;
        var typeface = new Typeface("Segoe UI");
        for (int i = 0; i <= 2; i++)
        {
            double y = top + (bottom - top) * i / 2;
            dc.DrawLine(gridPen, new Point(left, y), new Point(right, y));
            var label = new FormattedText($"{max * (2 - i) / 2:0}%", CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, 11, textBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(label, new Point(2, y - 7));
        }
        if (Values is { Count: > 1 } values)
        {
            var geometry = new StreamGeometry();
            Point? lastPoint = null;
            using (var context = geometry.Open())
            {
                bool drawing = false;
                for (int i = 0; i < values.Count; i++)
                {
                    if (values[i] is not double value)
                    {
                        drawing = false;
                        continue;
                    }
                    var point = new Point(left + (right - left) * i / (values.Count - 1),
                        bottom - Math.Clamp(value / max, 0, 1) * (bottom - top));
                    if (!drawing) context.BeginFigure(point, false, false);
                    else context.LineTo(point, true, false);
                    drawing = true;
                    lastPoint = point;
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(null, linePen, geometry);
            if (lastPoint is Point pointToMark)
                dc.DrawEllipse(linePen.Brush, null, pointToMark, 3, 3);
        }
        var now = new FormattedText("now", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            typeface, 11, textBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(now, new Point(right - now.Width, bottom + 5));
    }
}
