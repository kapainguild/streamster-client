using System.Windows;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Support
{
    public class GoodByeFocus
    {

        public static readonly DependencyProperty RemoveProperty =
                                            DependencyProperty.RegisterAttached("Remove", typeof(bool), typeof(GoodByeFocus), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnRemoveChanged));

        public static bool GetRemove(DependencyObject obj)=>  (bool)obj.GetValue(RemoveProperty);
        public static void SetRemove(DependencyObject obj, bool value) => obj.SetValue(RemoveProperty, value);


        private static void OnRemoveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool a && a)
            {
                if (d is Control c && c.Focusable && c.FocusVisualStyle != null)
                    c.FocusVisualStyle = null;
            }

        }
    }
}
