using Streamster.ClientData.Model;
using System.Windows;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Controls
{
    public class SceneCanvas : Panel
    {

        public static readonly DependencyProperty RectProperty = DependencyProperty.RegisterAttached("Rect", typeof(SceneRect), typeof(SceneCanvas),
                                                                        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsParentArrange));


        public static SceneRect GetRect(DependencyObject element) => (SceneRect)element.GetValue(RectProperty);
        public static void SetRect(DependencyObject element, SceneRect value) => element.SetValue(RectProperty, value);

        protected override Size MeasureOverride(Size constraint)
        {
            Size availableSize = new Size(double.PositiveInfinity, double.PositiveInfinity);
            foreach (UIElement child in base.InternalChildren)
            {
                child?.Measure(availableSize);
            }

            return default(Size);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            foreach (UIElement child in base.InternalChildren)
            {
                var rect = GetRect(child);
                if (rect != null)
                    child.Arrange(new Rect(rect.L * arrangeSize.Width, rect.T * arrangeSize.Height, rect.W * arrangeSize.Width, rect.H * arrangeSize.Height));
            }

            return arrangeSize;
        }
    }
}
