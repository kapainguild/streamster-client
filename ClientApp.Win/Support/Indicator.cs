using Streamster.ClientCore.Models;
using Streamster.ClientData.Model;
using System.Windows;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Support
{
    public class Indicator : ContentControl
    {
        public static DependencyProperty SmallIconProperty = DependencyProperty.Register("SmallIcon", typeof(object), typeof(Indicator));
        public static DependencyProperty SmallContentProperty = DependencyProperty.Register("SmallContent", typeof(object), typeof(Indicator));
        public static DependencyProperty BigIconProperty = DependencyProperty.Register("BigIcon", typeof(object), typeof(Indicator));

        public static DependencyProperty StateProperty = DependencyProperty.Register("State", typeof(IndicatorState), typeof(Indicator));

        public static DependencyProperty DetailedDescriptionProperty = DependencyProperty.Register("DetailedDescription", typeof(string), typeof(Indicator));

        public object SmallIcon
        {
            get => GetValue(SmallIconProperty);
            set => SetValue(SmallIconProperty, value);
        }

        public object SmallContent
        {
            get => GetValue(SmallContentProperty);
            set => SetValue(SmallContentProperty, value);
        }

        public object BigIcon
        {
            get => GetValue(BigIconProperty);
            set => SetValue(BigIconProperty, value);
        }

        public IndicatorState State
        {
            get => (IndicatorState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public string DetailedDescription
        {
            get => (string)GetValue(DetailedDescriptionProperty);
            set => SetValue(DetailedDescriptionProperty, value);
        }
        
    }
}
