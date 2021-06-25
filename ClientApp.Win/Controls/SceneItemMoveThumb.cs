using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Streamster.ClientApp.Win.Controls
{
    public class SceneItemMoveThumb : Thumb
    {
        private readonly SceneItemControl _parent;
        private Point _startDrag;
        private SceneRect _startRect;

        public SceneItemMoveThumb(SceneItemControl parent)
        {
            _parent = parent;
            DragStarted += OnDragStarted;
            DragDelta += OnDragDelta;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            var p = Mouse.GetPosition(_parent);
            var size = _parent.RenderSize;
            var delta = p - _startDrag;

            _parent.Rect = new SceneRect(
                _startRect.L + delta.X / size.Width,
                _startRect.T + delta.Y / size.Height, 
                _startRect.W, 
                _startRect.H);

        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            _startDrag = Mouse.GetPosition(_parent);
            _startRect = _parent.Rect;
        }
    }
}
