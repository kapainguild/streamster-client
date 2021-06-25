using DynamicStreamer;
using Serilog;
using Streamster.ClientCore.Models;
using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Streamster.ClientApp.Win.Controls
{
    [TemplatePart(Name = "PART_Content", Type = typeof(ContentPresenter))]
    public class SceneEditor : ContentControl
    {
        private (double x, double y) _dragPtzStart;
        private SceneItemModel _dragPtzZoomOwner;
        private SceneRect _dragPtzStartModel;
        private bool _captured;
        private bool _wasDragMovement;
        private ContentPresenter _contentPresenter;

        private Point _dragItemStart;
        private SceneRect _dragItemStartRect;

        private Point _dragReseizeStart;
        private SceneRect _dragResizeStartRect;

        public SceneEditor()
        {
        }

        static SceneEditor()
        {
            EventManager.RegisterClassHandler(typeof(SceneEditor), Thumb.DragDeltaEvent, new DragDeltaEventHandler((s, e) => ((SceneEditor)s).OnThumbDragDelta(e)));
            EventManager.RegisterClassHandler(typeof(SceneEditor), Thumb.DragStartedEvent, new DragStartedEventHandler((s, e) => ((SceneEditor)s).OnThumbDragStarted(e)));
            EventManager.RegisterClassHandler(typeof(SceneEditor), Thumb.DragCompletedEvent, new DragCompletedEventHandler((s, e) => ((SceneEditor)s).OnThumbDragCompleted(e)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _contentPresenter = GetTemplateChild("PART_Content") as ContentPresenter;
        }


        private void OnThumbDragCompleted(DragCompletedEventArgs e)
        {
            _dragResizeStartRect = null;
        }

        private void OnThumbDragStarted(DragStartedEventArgs e)
        {
            var selected = GetSelected();
            if (selected != null)
            {
                _dragReseizeStart = GetRelativePos(null);
                _dragResizeStartRect = selected.Rect.Value;
            }
        }

        private void OnThumbDragDelta(DragDeltaEventArgs e)
        {
            var init = _dragResizeStartRect;
            var selected = GetSelected();
            if (selected != null && init != null)
            {
                var thumb = (SceneItemResizeThumb)e.OriginalSource;

                var p = GetRelativePos(null);
                var delta = p - _dragReseizeStart;
                var oppositePoint = new Point(init.L + init.W * (1 - thumb.X) / 2,
                                              init.T + init.H * (1 - thumb.Y) / 2);
                var draggingPoint = new Point(init.L + init.W * (1 + thumb.X) / 2,
                                              init.T + init.H * (1 + thumb.Y) / 2);

                var fromOpposite = GetChangeVector(thumb, draggingPoint - oppositePoint + new Vector(delta.X, delta.Y), oppositePoint);

                var newDraggingPoint = oppositePoint + fromOpposite;
                var changed = newDraggingPoint - draggingPoint;

                var newRect = new SceneRect(init.L + (thumb.X > 0 ? 0.0 : changed.X),
                    init.T + (thumb.Y > 0 ? 0.0 : changed.Y),
                    init.W + (thumb.X > 0 ? changed.X : -changed.X),
                    init.H + (thumb.Y > 0 ? changed.Y : -changed.Y));
                selected.Rect.Value = GetRectWithLimitations(newRect, selected.Rect.Value);
            }
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            var point = GetPoint(e);
            var zoomOwner = GetZoomModel(e);
            if (zoomOwner != null)
            {
                var old = zoomOwner.Zoom.Zoom.Value;

                var rect = zoomOwner.Model.Rect;

                double x = (point.x - rect.L) / rect.W;
                double y = (point.y - rect.T) / rect.H;

                zoomOwner.Zoom.DoZoom(old, old + e.Delta / 240.0 / 10.0, x, y);
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && !VM.EditingMode.Value)
            {
                var pos = GetRelativePos(e);
                if(SceneRect.Full().Contains(pos.X, pos.Y))
                {
                    UpdateMouseOver(e);
                    SelectMouseOver(e);
                }
            }
            else if (VM.EditingMode.Value)
            {
                var selected = GetSelected();
                var pos = GetRelativePos(e);
                if (selected != null && selected.Rect.Value.Contains(pos.X, pos.Y))
                {
                    _dragItemStart = pos;
                    _dragItemStartRect = selected.Rect.Value;
                    _wasDragMovement = false;
                }
                _captured = CaptureMouse();
            }
            else 
            {
                var zoomOwner = GetZoomModel(e);
                if (zoomOwner != null && zoomOwner.Zoom.HasZoom.Value)
                {
                    _dragPtzStart = GetPoint(e);
                    _dragPtzZoomOwner = zoomOwner;
                    _dragPtzStartModel = zoomOwner.Zoom.GetPtz();
                    _captured = CaptureMouse();
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            RefreshNeedMouse(e);
            if (_captured)
            {
                var selected = GetSelected();
                if (VM.EditingMode.Value && _dragItemStartRect != null)
                {
                    var pos = GetRelativePos(e);
                    var rect = new PositionRect(_dragItemStartRect.L - _dragItemStart.X + pos.X, _dragItemStartRect.T - _dragItemStart.Y + pos.Y, _dragItemStartRect.W, _dragItemStartRect.H);
                    var dx = 0.0;
                    var dy = 0.0;

                    if (UseAnchoring())
                    {
                        var leftAchor = GetVerticalAnchor(selected, rect.Left, rect.Top, rect.Top + rect.Height);
                        var rightAchor = GetVerticalAnchor(selected, rect.Left + rect.Width, rect.Top, rect.Top + rect.Height);
                        if (leftAchor != null)
                        {
                            if (rightAchor != null && rightAchor.Value.Distance < leftAchor.Value.Distance)
                                dx = rightAchor.Value.X - rect.Width - rect.Left;
                            else
                                dx = leftAchor.Value.X - rect.Left;
                        }
                        else if (rightAchor != null)
                            dx = rightAchor.Value.X - rect.Width - rect.Left;


                        var topAchor = GetHorizontalAnchor(selected, rect.Top, rect.Left, rect.Left + rect.Width);
                        var bottonAchor = GetHorizontalAnchor(selected, rect.Top + rect.Height, rect.Left, rect.Left + rect.Width);
                        if (topAchor != null)
                        {
                            if (bottonAchor != null && bottonAchor.Value.Distance < topAchor.Value.Distance)
                                dy = bottonAchor.Value.Y - rect.Height - rect.Top;
                            else
                                dy = topAchor.Value.Y - rect.Top;
                        }
                        else if (bottonAchor != null)
                            dy = bottonAchor.Value.Y - rect.Height - rect.Top;
                    }

                    var newRect = new SceneRect(rect.Left + dx, rect.Top + dy, rect.Width, rect.Height);
                    selected.Rect.Value = GetRectWithLimitations(newRect, selected.Rect.Value);
                }
                else if (_dragPtzZoomOwner != null)
                {
                    var current = GetPoint(e);
                    _dragPtzZoomOwner.Zoom.MoveDelta(_dragPtzStart, current, _dragPtzStartModel);
                }

                _wasDragMovement = true;
            }

            if (VM.EditingMode.Value)
                UpdateMouseOver(e);

            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            if (_captured)
            {
                _captured = false;
                ReleaseMouseCapture();

                if (!_wasDragMovement && VM.EditingMode.Value)
                {
                    SelectMouseOver(e);
                }
            }
            _wasDragMovement = false;
            _dragItemStartRect = null;
            _dragPtzStartModel = null;
            _dragPtzZoomOwner = null;

            base.OnMouseUp(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            if (VM.EditingMode.Value)
                UpdateMouseOver(e);
        }


        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            _captured = false;
            _dragItemStartRect = null;
            _dragPtzStartModel = null;
            base.OnLostMouseCapture(e);
        }

       

        private void SelectMouseOver(MouseEventArgs e)
        {
            var items = GetItems().ToList();
            var mouseOver = items.FirstOrDefault(s => s.IsMouseOver.Value);
            if (mouseOver != null)
            {
                VM.SelectItem(mouseOver);
                UpdateMouseOver(e);
            }
            else
            {
                // unselect?
                var selected = GetSelected();
                var pos = GetRelativePos(e);
                if (selected != null)
                {
                    if (!selected.Rect.Value.Contains(pos.X, pos.Y))
                    {
                        VM.SelectAddLayer();
                    }
                }
                else
                {
                    if (!SceneRect.Full().Contains(pos.X, pos.Y))
                        VM.DoClose();
                    else
                        VM.SelectAddLayer();
                }
                    
            }
        }


        private void UpdateMouseOver(MouseEventArgs e)
        {
            var point = GetRelativePos(e);
            var items = GetItems().ToList();

            bool found = false;
            foreach (var i in items)
            {
                if (i.IsSelected.Value)
                {
                    i.IsMouseOver.Value = false;
                }
                else
                {
                    if (!found)
                    {
                        if (i.Rect.Value.Contains(point.X, point.Y))
                        {
                            found = true;
                            if (!i.IsMouseOver.Value)
                                i.IsMouseOver.Value = true;
                        }
                        else
                            i.IsMouseOver.Value = false;
                    }
                    else
                        i.IsMouseOver.Value = false;
                }
            }
        }

        private (double X, double Distance)? GetVerticalAnchor(SceneItemModel excluded, double x, double y1, double y2)
        {
            var rects = GetItems().Where(s => s != excluded).Select(s => s.Rect.Value).Concat(new[] { SceneRect.Full() })
                .Where(r => ! (y1 < r.T && y2 < r.T || y1 > r.T + r.H && y2 > r.T + r.H)).ToList();

            var verticals = rects.SelectMany(s => new[] { s.L, s.Right() }).Select(s => new { X = s, Distance = Math.Abs(x - s) }).ToList();
            if (verticals.Count > 0)
            {
                var best = verticals.Min(s => s.Distance);
                var bestVal = verticals.FirstOrDefault(s => s.Distance == best);
                if (bestVal != null && bestVal.Distance < 10 / _contentPresenter.RenderSize.Width)
                    return (bestVal.X, bestVal.Distance);
            }
            return null;
        }

        private (double Y, double Distance)? GetHorizontalAnchor(SceneItemModel excluded, double y, double x1, double x2)
        {
            var rects = GetItems().Where(s => s != excluded).Select(s => s.Rect.Value).Concat(new[] { SceneRect.Full() })
                .Where(r => !(x1 < r.L && x2 < r.L || x1 > r.L + r.W && x2 > r.L + r.W)).ToList();

            var horizontals = rects.SelectMany(s => new[] { s.T, s.Right()}).Select(s => new { Y = s, Distance = Math.Abs(y - s) }).ToList();
            if (horizontals.Count > 0)
            {
                var best = horizontals.Min(s => s.Distance);
                var bestVal = horizontals.FirstOrDefault(s => s.Distance == best);
                if (bestVal != null && bestVal.Distance < 10 / _contentPresenter.RenderSize.Height)
                    return (bestVal.Y, bestVal.Distance);
            }
            return null;
        }


        private Vector GetChangeVector(SceneItemResizeThumb thumb, Vector delta, Point oppositePoint)
        {
            if (thumb.X == 0)
            {
                var anchor = GetHorizontalAnchor(GetSelected(), oppositePoint.Y + delta.Y, _dragResizeStartRect.L, _dragResizeStartRect.Right());
                if (anchor != null && UseAnchoring())
                    return new Vector(0, anchor.Value.Y - oppositePoint.Y);
                return new Vector(0, delta.Y);
            }

            if (thumb.Y == 0)
            {
                var anchor = GetVerticalAnchor(GetSelected(), oppositePoint.X + delta.X, _dragResizeStartRect.T, _dragResizeStartRect.Bottom());
                if (anchor != null && UseAnchoring())
                    return new Vector(anchor.Value.X - oppositePoint.X, 0);
                return new Vector(delta.X, 0);
            }

            Vector result;
            if (delta.Y == 0 || Math.Abs(delta.X / delta.Y) > _dragResizeStartRect.W / _dragResizeStartRect.H)
                result = new Vector(delta.X, Math.Sign(delta.Y) * Math.Abs(delta.X) * _dragResizeStartRect.H / _dragResizeStartRect.W);
            else
                result = new Vector(Math.Sign(delta.X) * Math.Abs(delta.Y) * _dragResizeStartRect.W / _dragResizeStartRect.H, delta.Y);

            if (UseAnchoring())
            {
                var anchorV = GetVerticalAnchor(GetSelected(), oppositePoint.X + result.X, Math.Min(oppositePoint.Y, oppositePoint.Y + result.Y), Math.Max(oppositePoint.Y, oppositePoint.Y + result.Y));
                if (anchorV != null)
                {
                    result = new Vector(anchorV.Value.X - oppositePoint.X, Math.Sign(delta.Y) * Math.Abs(anchorV.Value.X - oppositePoint.X) * _dragResizeStartRect.H / _dragResizeStartRect.W);
                }
                else
                {
                    var anchorH = GetHorizontalAnchor(GetSelected(), oppositePoint.Y + result.Y, Math.Min(oppositePoint.X, oppositePoint.X + result.X), Math.Max(oppositePoint.X, oppositePoint.X + result.X));
                    if (anchorH != null)
                        result = new Vector(Math.Sign(delta.X) * Math.Abs(anchorH.Value.Y - oppositePoint.Y) * _dragResizeStartRect.W / _dragResizeStartRect.H, anchorH.Value.Y - oppositePoint.Y);
                }
            }

            return result;
        }


        private bool UseAnchoring() => !Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl);

        private SceneRect GetRectWithLimitations(SceneRect newRect, SceneRect oldRect)
        {
            var minAway = 0.005;
            if (newRect.W < 0.01 ||
                newRect.H < 0.01 ||
                newRect.Right() < -minAway ||
                newRect.Bottom() < -minAway ||
                newRect.L > 1 + minAway ||
                newRect.T > 1 + minAway)
                return oldRect;
            return newRect;
        }

        private (double x, double y) GetPoint(MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            return (pos.X / ActualWidth, pos.Y / ActualHeight);
        }

        private Point GetRelativePos(MouseEventArgs e)
        {
            var pos = e != null ? e.GetPosition(_contentPresenter) : Mouse.GetPosition(_contentPresenter);
            return new Point(pos.X / _contentPresenter.RenderSize.Width, pos.Y / _contentPresenter.RenderSize.Height);
        }

        private void RefreshNeedMouse(MouseEventArgs e)
        {
            var result = false;
            if (VM.EditingMode.Value)
                result = true;
            else
            {
                var zoom = GetZoomModel(e);
                if (zoom != null && zoom.Zoom.HasZoom.Value)
                    result = true;
            }
            result = !result;

            if (VM.DragIsNotRequired.Value != result)
                VM.DragIsNotRequired.Value = result;
        }


        private SceneItemModel GetZoomModel(MouseEventArgs e)
        {
            var vm = VM;
            if (vm.SelectedItem.Value != null)
                return vm.SelectedItem.Value;

            var point = GetRelativePos(e);
            var items = GetItems().ToList();

            foreach (var i in items)
            {
                if (i.Rect.Value.Contains(point.X, point.Y) &&
                    i.Model.Source.Device != null)
                {
                    return i;
                }
            }
            return null;

        }
            


        private SceneEditingModel VM => ((MainModel)DataContext).SceneEditing;

        private ObservableCollection<SceneItemModel> GetItems() => VM.Items;

        private SceneItemModel GetSelected() => VM.SelectedItem.Value;
    }
}
