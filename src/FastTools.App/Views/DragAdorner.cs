using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace FastTools.App.Views;

internal sealed class DragAdorner : Adorner
{
    private readonly VisualBrush _brush;
    private readonly System.Windows.Size _size;
    private System.Windows.Point _offset;

    public DragAdorner(UIElement adornedElement, UIElement draggedElement, System.Windows.Point offset)
        : base(adornedElement)
    {
        _size = new System.Windows.Size(draggedElement.RenderSize.Width, draggedElement.RenderSize.Height);
        _brush = new VisualBrush(draggedElement)
        {
            Opacity = 0.65,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
        };
        _offset = offset;
        IsHitTestVisible = false;
    }

    public void UpdatePosition(System.Windows.Point position)
    {
        _offset = position;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(
            _brush,
            null,
            new Rect(
                new System.Windows.Point(_offset.X - _size.Width / 2, _offset.Y - _size.Height / 2),
                _size));
    }
}
