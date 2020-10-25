using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Support
{
    public class ResponsivePanel : Panel
    {
        public static readonly DependencyProperty ResponsiveTypeProperty =
            DependencyProperty.RegisterAttached("ResponsiveType", typeof(ResponsiveType), typeof(ResponsivePanel));

        public static ResponsiveType GetResponsiveType(DependencyObject element) => (ResponsiveType)element?.GetValue(ResponsiveTypeProperty);
        public static void SetResponsiveType(DependencyObject element, ResponsiveType value) => element?.SetValue(ResponsiveTypeProperty, value);

        protected override Size MeasureOverride(Size a)
        {
            var calculation = ResponsiveHost.GetCalculation(this);
            var r = calculation.Sizes;

            Get(ResponsiveType.ScreenArea).Measure(r.screenArea.Size);
            Get(ResponsiveType.Screen).Measure(r.screen.Size);
            Get(ResponsiveType.ScreenLeftSide).Measure(r.screenSideLeft.Size);
            Get(ResponsiveType.ScreenRightSide).Measure(r.screenSideRight.Size);
            Get(ResponsiveType.MainArea).Measure(r.main.Size);

            return a;
        }
        protected override Size ArrangeOverride(Size a)
        {
            var calculation = ResponsiveHost.GetCalculation(this);
            var r = calculation.Sizes;

            Get(ResponsiveType.ScreenArea).Arrange(r.screenArea);
            Get(ResponsiveType.ScreenLeftSide).Arrange(r.screenSideLeft);
            Get(ResponsiveType.ScreenRightSide).Arrange(r.screenSideRight);
            Get(ResponsiveType.Screen).Arrange(r.screen);
            Get(ResponsiveType.MainArea).Arrange(r.main);

            return a;
        }

        private FrameworkElement Get(ResponsiveType type) => GetChild<FrameworkElement>(type);

        private T GetChild<T>(ResponsiveType type) where T : DependencyObject
        {
            foreach(var d in Children.OfType<T>())
            {
                if (GetResponsiveType(d) == type)
                    return d;
            }
            return default;
        }
    }

    public enum ResponsiveType
    {
        Screen,
        ScreenRightSide,
        ScreenLeftSide,
        ScreenArea,
        MainArea,
        Notifications,
    }
}
