using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Streamster.ClientApp.Win.Controls
{
    public class SceneItemResizeThumb : Thumb
    {
        private readonly SceneItemControl _parent;
        private Point _startDrag;
        private SceneRect _startRect;

        public int X { get; set; }

        public int Y { get; set; }


        public SceneItemResizeThumb(SceneItemControl parent, int v1, int v2)
        {
            _parent = parent;
            X = v1;
            Y = v2;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            double x = (1 + X) * arrangeBounds.Width / 2;
            double y = (1 + Y) * arrangeBounds.Height / 2;

            var desired = DesiredSize;

            if (VisualChildrenCount > 0)
            {
                ((UIElement)GetVisualChild(0))?.Arrange(new Rect(x - desired.Width / 2, y - desired.Height / 2, desired.Width, desired.Height));
            }

            return arrangeBounds;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            var p = Mouse.GetPosition(_parent);
            var size = _parent.RenderSize;
            var delta = p - _startDrag;
            var oppositePoint = new Point(_startRect.L + _startRect.W * (1 - X) / 2,
                                          _startRect.T + _startRect.H * (1 - Y) / 2);
            var draggingPoint = new Point(_startRect.L + _startRect.W * (1 + X) / 2,
                                          _startRect.T + _startRect.H * (1 + Y) / 2);

            var fromOpposite = GetChangeVector(draggingPoint - oppositePoint + new Vector(delta.X / size.Width, delta.Y / size.Height));

            var newDraggingPoint = oppositePoint + fromOpposite;
            var changed = newDraggingPoint - draggingPoint;

            _parent.Rect = new SceneRect(_startRect.L + (X > 0 ? 0.0 : changed.X),
                _startRect.T + (Y > 0 ? 0.0 : changed.Y),
                _startRect.W + (X > 0 ? changed.X : -changed.X),
                _startRect.H + (Y > 0 ? changed.Y : -changed.Y));

        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            _startDrag = Mouse.GetPosition(_parent);
            _startRect = _parent.Rect;
        }

        private Vector GetChangeVector(Vector delta)
        {
            if (X == 0)
                return new Vector(0, delta.Y);
            if (Y == 0) 
                return new Vector(delta.X, 0);

            if (delta.Y == 0 || Math.Abs(delta.X / delta.Y) > _startRect.W / _startRect.H)
                return new Vector(delta.X, Math.Sign(delta.Y) * Math.Abs(delta.X) * _startRect.H / _startRect.W);
            
            return new Vector(Math.Sign(delta.X) * Math.Abs(delta.Y) * _startRect.W / _startRect.H, delta.Y);
        }
    }
}
