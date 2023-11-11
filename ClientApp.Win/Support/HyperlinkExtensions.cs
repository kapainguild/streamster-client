using Serilog;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Streamster.ClientApp.Win.Support
{
    public static class HyperlinkExtensions
    {
        public static readonly DependencyProperty IsExternalProperty =
            DependencyProperty.RegisterAttached("IsExternal", typeof(bool), typeof(HyperlinkExtensions), new UIPropertyMetadata(false, OnIsExternalChanged));

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(HyperlinkExtensions));


        public static bool GetIsExternal(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsExternalProperty);
        }

        public static void SetIsExternal(DependencyObject obj, bool value)
        {
            obj.SetValue(IsExternalProperty, value);
        }

        public static ICommand GetCommand(DependencyObject obj)
        {
            return (ICommand)obj.GetValue(CommandProperty);
        }

        public static void SetCommand(DependencyObject obj, ICommand value)
        {
            obj.SetValue(CommandProperty, value);
        }

        private static void OnIsExternalChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var hyperlink = (Hyperlink)sender;

            if ((bool)args.NewValue)
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
            else
                hyperlink.RequestNavigate -= Hyperlink_RequestNavigate;
        }

        private static void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            var hyperlink = sender as Hyperlink;
            var startInfo = new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            Process.Start(startInfo);
            GetCommand(hyperlink)?.Execute(e.Uri);
            Log.Information($"Navigate {e.Uri}");
            e.Handled = true;
        }

        public static string GetFormattedText(DependencyObject obj) => (string)obj.GetValue(FormattedTextProperty);

        public static void SetFormattedText(DependencyObject obj, string value) => obj.SetValue(FormattedTextProperty, value);

        public static readonly DependencyProperty FormattedTextProperty =
            DependencyProperty.RegisterAttached("FormattedText", typeof(string), typeof(HyperlinkExtensions),
            new PropertyMetadata(string.Empty, OnFormattedTextChanged));


        private static void OnFormattedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            string text = e.NewValue as string;
            var textBlock = d as TextBlock;
            if (textBlock != null)
            {
                textBlock.Inlines.Clear();
                var str = text.Split('|');
                for (int i = 0; i < str.Length; i++)
                {
                    if (i % 3 == 0)
                        textBlock.Inlines.Add(new Run { Text = str[i] });
                    else if (i % 3 == 2)
                    {
                        Hyperlink link = new Hyperlink { NavigateUri = new Uri(str[i]), Foreground = new SolidColorBrush(Color.FromRgb(0x20, 0x90, 0xF0)) };
                        link.RequestNavigate += Hyperlink_RequestNavigate;
                        link.Inlines.Add(new Run { Text = str[i - 1] });
                        textBlock.Inlines.Add(link);
                    }
                }
            }
        }
    }
}
