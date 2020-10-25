using Serilog;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;

namespace Streamster.ClientApp.Win.Support
{
    public class ResizableWindowBehavior
    {
        public static readonly DependencyProperty ResizableProperty =
            DependencyProperty.RegisterAttached("Resizable", typeof(bool), typeof(ResizableWindowBehavior), new FrameworkPropertyMetadata(false
                                                    , FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnResizableChanged));

        public static bool GetResizable(DependencyObject target) => (bool)target.GetValue(ResizableProperty);

        public static void SetResizable(DependencyObject target, bool value) => target.SetValue(ResizableProperty, value);

        private static void OnResizableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool b && b)
            {
                var wnd = (Window)d;
                wnd.ResizeMode = ResizeMode.CanResizeWithGrip;
            }
        }


        public static readonly DependencyProperty WindowMoveEnabledProperty =
            DependencyProperty.RegisterAttached("WindowMoveEnabled", typeof(bool), typeof(ResizableWindowBehavior), new FrameworkPropertyMetadata(false
                                                    , FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnWindowMoveEnabledChanged));

        public static bool GetWindowMoveEnabled(DependencyObject target) => (bool)target.GetValue(ResizableProperty);

        public static void SetWindowMoveEnabled(DependencyObject target, bool value) => target.SetValue(ResizableProperty, value);

        private static void OnWindowMoveEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool b && b)
            {
                var element = (UIElement)d;

                element.MouseDown += (s, e) =>
                {
                    if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed &&
                        e.ChangedButton == System.Windows.Input.MouseButton.Left)
                    {
                        try
                        {
                            Window parentWindow = Window.GetWindow(d);
                            parentWindow.DragMove();
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Drag move failed");
                        }
                    }
                };
            }
        }
    }
}
