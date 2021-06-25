using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Streamster.ClientApp.Win.Support
{
    public static class Behaviors
    {
        public static readonly DependencyProperty IsActivatedProperty = DependencyProperty.RegisterAttached(
            "IsActivated",
            typeof(bool),
            typeof(Behaviors),
            new FrameworkPropertyMetadata(default(bool), FrameworkPropertyMetadataOptions.Inherits));

        public static bool GetIsActivated(DependencyObject element)
        {
            return (bool)element.GetValue(IsActivatedProperty);
        }

        public static void SetIsActivated(DependencyObject element, bool value)
        {
            element.SetValue(IsActivatedProperty, value);
        }

        public static readonly DependencyProperty DoubleValueProperty = DependencyProperty.RegisterAttached("DoubleValue", typeof(double), typeof(Behaviors));
        public static void SetDoubleValue(UIElement element, double value) => element.SetValue(DoubleValueProperty, value);
        public static double GetDoubleValue(UIElement element) => (double)element.GetValue(DoubleValueProperty);



        public static readonly DependencyProperty MouseOverProperty = DependencyProperty.RegisterAttached("MouseOver", typeof(bool), typeof(Behaviors));
        public static void SetMouseOver(UIElement element, bool value) => element.SetValue(MouseOverProperty, value);
        public static bool GetMouseOver(UIElement element) => (bool)element.GetValue(MouseOverProperty);


        public static readonly DependencyProperty MouseOverListenProperty = DependencyProperty.RegisterAttached("MouseOverListen", typeof(bool), typeof(Behaviors), new PropertyMetadata(OnMouseOverListenChanged));
        public static void SetMouseOverListen(UIElement element, bool value) => element.SetValue(MouseOverListenProperty, value);
        public static bool GetMouseOverListen(UIElement element) => (bool)element.GetValue(MouseOverListenProperty);


        public static readonly DependencyProperty TextBoxHasPasteButtonProperty = DependencyProperty.RegisterAttached("TextBoxHasPasteButton", typeof(bool), typeof(Behaviors), new PropertyMetadata(false, TextBoxHasPasteChanged));

        private static void TextBoxHasPasteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox b)
            {
                if (b.IsLoaded)
                    UpdatePasteHandler(b);
                else
                    b.Loaded += (sender, args) => UpdatePasteHandler(b);
            }
        }

        private static void UpdatePasteHandler(TextBox b)
        {
            var clearButton = b.Template.FindName("PART_PasteButton", b) as Button;
            if (clearButton != null)
            {
                RoutedEventHandler handler = (sender, args) => 
                {
                    b.Text = null;
                    b.Paste();
                };
                if (GetTextBoxHasPasteButton(b))
                    clearButton.Click += handler;
                else
                    clearButton.Click -= handler;
            }
        }

        public static void SetTextBoxHasPasteButton(UIElement element, bool value) => element.SetValue(TextBoxHasPasteButtonProperty, value);
        public static bool GetTextBoxHasPasteButton(UIElement element) => (bool)element.GetValue(TextBoxHasPasteButtonProperty);


        public static readonly DependencyProperty TextBoxIsFlattyProperty = DependencyProperty.RegisterAttached("TextBoxIsFlatty", typeof(bool), typeof(Behaviors));
        public static void SetTextBoxIsFlatty(UIElement element, bool value) => element.SetValue(TextBoxIsFlattyProperty, value);
        public static bool GetTextBoxIsFlatty(UIElement element) => (bool)element.GetValue(TextBoxIsFlattyProperty);



        private static void OnMouseOverListenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = ((UIElement)d);

            if (e.NewValue is bool b && b)
            {
                element.MouseEnter += ElementOnMouseEnter;
                element.MouseLeave += ElementOnMouseLeave;
            }
            else
            {
                element.MouseEnter -= ElementOnMouseEnter;
                element.MouseLeave -= ElementOnMouseLeave;
            }
        }

        private static void ElementOnMouseLeave(object sender, MouseEventArgs mouseEventArgs)
        {
            var element = ((UIElement)sender);
            SetMouseOver(element, false);
        }

        private static void ElementOnMouseEnter(object sender, MouseEventArgs mouseEventArgs)
        {
            var element = ((UIElement)sender);
            SetMouseOver(element, true);
        }
    }
}
