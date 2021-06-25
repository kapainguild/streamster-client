using Streamster.ClientData.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Streamster.ClientApp.Win.Controls
{
    public class SceneItemControl : Control
    {
        private readonly VisualCollection _children;
        private UIElement _content;
        public static readonly DependencyProperty RectProperty = DependencyProperty.Register(nameof(Rect), typeof(SceneRect), typeof(SceneItemControl),
                                   new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty IsThumbsEnabledProperty = DependencyProperty.Register(nameof(IsThumbsEnabled), typeof(bool), typeof(SceneItemControl),
                                   new FrameworkPropertyMetadata(false, (a, b) => ((SceneItemControl)a).UpdateIsThumbsEnabled()));

        public bool IsThumbsEnabled
        {
            get => (bool)GetValue(IsThumbsEnabledProperty);
            set => SetValue(IsThumbsEnabledProperty, value);
        }

        public SceneRect Rect
        {
            get => (SceneRect)GetValue(RectProperty);
            set => SetValue(RectProperty, value);
        }

        public UIElement Content
        {
            get => this._content;
            set
            {
                _content = value;
                RefreshChildren();
            }
        }

        private void RefreshChildren()
        {
            this._children.Clear();

            this._children.Add(_content);

            if (IsThumbsEnabled)
            {
                this._children.Add(new SceneItemResizeThumb(this, -1, -1));
                this._children.Add(new SceneItemResizeThumb(this, 0, -1));
                this._children.Add(new SceneItemResizeThumb(this, 1, -1));
                this._children.Add(new SceneItemResizeThumb(this, 1, 0));
                this._children.Add(new SceneItemResizeThumb(this, 1, 1));
                this._children.Add(new SceneItemResizeThumb(this, 0, 1));
                this._children.Add(new SceneItemResizeThumb(this, -1, 1));
                this._children.Add(new SceneItemResizeThumb(this, -1, 0));
            }
            InvalidateMeasure();
        }

        private void UpdateIsThumbsEnabled()
        {
            RefreshChildren();
        }

        protected override Visual GetVisualChild(int index) => this._children[index];

        protected override int VisualChildrenCount => this._children.Count;

        public SceneItemControl()
        {
            this._children = new VisualCollection(this);
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Size availableSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
            foreach (UIElement child in _children)
            {
                child?.Measure(availableSize);
            }
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var rect = Rect;
            var absolute = new Rect(rect.L * arrangeSize.Width, rect.T * arrangeSize.Height, rect.W * arrangeSize.Width, rect.H * arrangeSize.Height);

            foreach (UIElement child in this._children)
            {
                child.Arrange(absolute);
            }

            return arrangeSize;
        }
    }
}
