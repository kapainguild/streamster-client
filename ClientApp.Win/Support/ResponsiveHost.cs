using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Streamster.ClientApp.Win.Support
{
    class ResponsiveHost : ContentControl
    {
        public static readonly DependencyProperty CalculationProperty =
            DependencyProperty.RegisterAttached("Calculation", typeof(ResponsiveHostCalculation), typeof(ResponsiveHost), 
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

        public static readonly DependencyProperty DisplayVideoHiddenProperty =
            DependencyProperty.Register("DisplayVideoHidden", typeof(bool), typeof(ResponsiveHost), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ChannelCountProperty =
            DependencyProperty.Register("ChannelCount", typeof(int), typeof(ResponsiveHost), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static ResponsiveHostCalculation GetCalculation(DependencyObject element) => (ResponsiveHostCalculation)element?.GetValue(CalculationProperty);
        public static void SetCalculation(DependencyObject element, ResponsiveHostCalculation value) => element?.SetValue(CalculationProperty, value);

        public bool DisplayVideoHidden
        {
            get => (bool)GetValue(DisplayVideoHiddenProperty);
            set => SetValue(DisplayVideoHiddenProperty, value);
        }

        public int ChannelCount
        {
            get => (int)GetValue(ChannelCountProperty);
            set => SetValue(ChannelCountProperty, value);
        }


        private const double VerticalIndicatorWidth = 60.0;
        private const double HorizontalIndicatorHeight = 30.0;
        private const double ScreenRatio = 16.0 / 9.0;
        private const double MainAreaMinHeight = 50.0;
        private const double MainAreaMinWidth = 50.0;
        private const double MainAreaToScreenRatio = 0.3;
        private const double HeaderHeight = 30;
        private const double WideRightWidth = 95;


        protected override Size MeasureOverride(Size constraint)
        {
            var sizes = Snap(Calculate(constraint));
            var values = Calculate(sizes, constraint);

            SetCalculation(this, new ResponsiveHostCalculation
            {
                Sizes = sizes,
                Values = values
            });

            return base.MeasureOverride(constraint);
        }

        private ResponsiveHostValues Calculate(ResponsiveHostSizes sizes, Size constraint)
        {
            var res = new ResponsiveHostValues
            {
                HideBitrateTitle = sizes.main.Width < 500,
                HideBitrate = sizes.main.Width < 300,
                IndicatorsHorizontal = sizes.screenSideLeft.Height < HorizontalIndicatorHeight + 1,
                RightHideInfo = sizes.screenSideLeft.Height < 230 && sizes.screenSideLeft.Width < WideRightWidth - 1,
                MainAreaCaptionMargin = sizes.main.Top < HeaderHeight,
                ScreenFilterHidden = sizes.screen.Width < 615,
                ScreenFpsHidden = sizes.screen.Width < 515,
                ScreenResolutionHidden = sizes.screen.Width < 415,
                HidePromo = constraint.Width < 500 || constraint.Height < 500
            };

            if (ChannelCount > 0)
            {

                var mainWidth = sizes.main.Width;
                var mainHeight = sizes.main.Height;
                if (res.MainAreaCaptionMargin) mainHeight -= 26;
                if (!res.HideBitrate) mainHeight -= 41;
                mainHeight -= 20;
                mainWidth -= 20;

                res.HideAddTargetButton = constraint.Width < 465 || constraint.Height < 300 || mainHeight < 80 || mainWidth < 200;
                if (!res.HideAddTargetButton)
                    mainWidth -= 56 + 20;

                double baseWidth = 238;
                double baseHeight = 121;

                var count = ChannelCount;

                if (ChannelFits(baseWidth, baseHeight, mainWidth, mainHeight, count))
                {
                    var sol = 30;
                    for (int q = 0; q < 30; q++)
                    {
                        if (!ChannelFits(baseWidth + (q + 1) * 3 + 20, baseHeight, mainWidth, mainHeight, count))
                        {
                            sol = q;
                            break;
                        }
                    }
                    res.ChannelWidth = baseWidth + sol * 3;
                }
                else
                {
                    res.ChannelWidth = 238;

                    if (mainWidth * mainHeight < 100000)
                    {
                        res.ChannelWidth = 104;
                        for (int q = 0; q < 25; q++)
                        {
                            res.ChannelWidth -= 2;
                            if (ChannelFits(res.ChannelWidth + 12, res.ChannelWidth + 12, mainWidth, mainHeight, count))
                                break;
                        }
                        res.ChannelTemplate = 1;
                        res.HideAddTargetButton = true;
                    }
                }

            }
            return res;
        }

        private bool ChannelFits(double baseWidth, double baseHeight, double mainWidth, double mainHeight, int count)
        {
            var countInRow = Math.Floor(mainWidth / (baseWidth));
            var countInCol = Math.Floor(mainHeight / (baseHeight));

            return count <= countInRow * countInCol;
        }

        private ResponsiveHostSizes Calculate(Size a)
        {
            bool norrow = a.Width < 450;

            if (DisplayVideoHidden)
            {
                bool rightButtonsVisible = a.Width >= 450 && a.Height >= 450;

                if (rightButtonsVisible)
                {
                    var height = 150;
                    var width = WideRightWidth;
                    return new ResponsiveHostSizes
                    {
                        screen = new Rect(width, HeaderHeight, a.Width - 2*width, height - HeaderHeight),
                        screenSideLeft = new Rect(0, height, a.Width, HorizontalIndicatorHeight),
                        screenSideRight = new Rect(a.Width - width, 0, width, height),
                        screenArea = new Rect(0, 0, a.Width, height),
                        main = new Rect(0, height + HorizontalIndicatorHeight, a.Width, a.Height - height - HorizontalIndicatorHeight),
                    };
                }
                else
                {
                    return new ResponsiveHostSizes
                    {
                        screenSideLeft = new Rect(0, HeaderHeight, a.Width, HorizontalIndicatorHeight),
                        screenArea = new Rect(0, 0, a.Width, HorizontalIndicatorHeight + HeaderHeight),
                        main = new Rect(0, HorizontalIndicatorHeight + HeaderHeight, a.Width, a.Height - HorizontalIndicatorHeight - HeaderHeight),
                    };
                }
            }

            if (norrow)
            {
                var screenHeight = a.Width / ScreenRatio;
                var remaining = a.Height - screenHeight;
                if (remaining < 0) // no space for anything
                {
                    var screenWidth = a.Height * ScreenRatio;
                    if (a.Width - screenWidth >= VerticalIndicatorWidth) // is there space for indicators?
                    {
                        // center [indicators][screen]
                        return PlaceLeftIndicatorAndScreen(a);
                    }
                    else
                        return PlaceScreenOnly(a);
                }
                else if (remaining < HorizontalIndicatorHeight) // still no space for anything
                {
                    return PlaceScreenOnly(a);
                }
                else if (remaining < HorizontalIndicatorHeight + MainAreaMinHeight) // some space for indicator below
                {
                    var marginY = (a.Height - screenHeight - HorizontalIndicatorHeight) / 2;
                    return new ResponsiveHostSizes
                    {
                        screen = new Rect(0, 0, a.Width, screenHeight),
                        screenArea = new Rect(a),
                        screenSideLeft = new Rect(0, marginY + screenHeight, a.Width, HorizontalIndicatorHeight)
                    };
                }
                else
                {
                    // now there is space for main
                    return new ResponsiveHostSizes
                    {
                        screen = new Rect(0, 0, a.Width, screenHeight),
                        screenArea = new Rect(0, 0, a.Width, screenHeight + HorizontalIndicatorHeight),
                        screenSideLeft = new Rect(0, screenHeight, a.Width, HorizontalIndicatorHeight),
                        main = new Rect(0, screenHeight + HorizontalIndicatorHeight, a.Width, a.Height - screenHeight - HorizontalIndicatorHeight)
                    };
                }
            }
            else
            {
                bool smallHeight = a.Height < 250;

                if (smallHeight)
                {
                    var scrWidth = a.Height * ScreenRatio;
                    var remaining = a.Width - scrWidth;
                    if (remaining < VerticalIndicatorWidth) // no space for anything
                    {
                        return PlaceScreenOnly(a);
                    }
                    else if (remaining < VerticalIndicatorWidth + MainAreaMinWidth)
                    {
                        return PlaceLeftIndicatorAndScreen(a);
                    }
                    else
                    {
                        // no there is space to main
                        return new ResponsiveHostSizes
                        {
                            screenSideLeft = new Rect(0, 0, VerticalIndicatorWidth, a.Height),
                            screen = new Rect(VerticalIndicatorWidth, 0, scrWidth, a.Height),
                            screenArea = new Rect(0, 0, scrWidth + VerticalIndicatorWidth, a.Height),
                            main = new Rect(scrWidth + VerticalIndicatorWidth, 0, a.Width - VerticalIndicatorWidth - scrWidth, a.Height)
                        };
                    }
                }


                var screenSideWidth = VerticalIndicatorWidth;

                var screenWidth = a.Width - screenSideWidth * 2;
                var screenHeight = screenWidth / ScreenRatio;

                var remainingForMain = a.Height - screenHeight;

                double mainHeight = remainingForMain;

                if (remainingForMain < MainAreaToScreenRatio * a.Height)
                {
                    mainHeight = MainAreaToScreenRatio * a.Height;
                    screenHeight = a.Height - mainHeight;
                    screenWidth = screenHeight * ScreenRatio;
                    screenSideWidth = (a.Width - screenWidth) / 2;
                }

                return new ResponsiveHostSizes
                {
                    main = new Rect(0, screenHeight, a.Width, mainHeight),
                    screen = new Rect(screenSideWidth, 0, screenWidth, screenHeight),
                    screenSideLeft = new Rect(0, 0, screenSideWidth, screenHeight),
                    screenSideRight = new Rect(screenSideWidth + screenWidth, 0, screenSideWidth, screenHeight),
                    screenArea = new Rect(0, 0, a.Width, screenHeight)
                };
            }
        }
        private ResponsiveHostSizes PlaceScreenOnly(Size a)
        {
            var screenHeight = a.Width / ScreenRatio;
            if (screenHeight > a.Height)
            {
                var screenWidth = a.Height * ScreenRatio;
                var left = (a.Width - screenWidth) / 2;
                return new ResponsiveHostSizes
                {
                    screen = new Rect(left, 0, screenWidth, a.Height),
                    screenArea = new Rect(a)
                };
            }
            else
            {
                var top = (a.Height - screenHeight) / 2;
                return new ResponsiveHostSizes
                {
                    screen = new Rect(0, top, a.Width, screenHeight),
                    screenArea = new Rect(a)
                };
            }
        }

        private ResponsiveHostSizes PlaceLeftIndicatorAndScreen(Size a)
        {
            var screenWidth = a.Height * ScreenRatio;
            var left = (a.Width - screenWidth - VerticalIndicatorWidth) / 2.0;
            return new ResponsiveHostSizes
            {
                screenSideLeft = new Rect(left, 0, VerticalIndicatorWidth, a.Height),
                screen = new Rect(left + VerticalIndicatorWidth, 0, screenWidth, a.Height),
                screenArea = new Rect(a)
            };
        }
        private ResponsiveHostSizes Snap(ResponsiveHostSizes s) => new ResponsiveHostSizes
        {
            main = Snap(s.main),
            screen = Snap(s.screen),
            screenArea = Snap(s.screenArea),
            screenSideLeft = Snap(s.screenSideLeft),
            screenSideRight = Snap(s.screenSideRight)
        };

        private Rect Snap(Rect r)
        {
            var x = Math.Floor(r.X);
            var y = Math.Floor(r.Y);
            var x2 = Math.Floor(r.X + r.Width);
            var y2 = Math.Floor(r.Y + r.Height);

            return new Rect(x, y, x2 - x, y2 - y);
        }



    }

    public class ResponsiveHostCalculation
    {
        public ResponsiveHostSizes Sizes { get; set; }

        public ResponsiveHostValues Values { get; set; }
    }

    public class ResponsiveHostValues
    {
        public bool HideBitrateTitle { get; set; }

        public bool HideBitrate { get; set; }

        public bool HideAddTargetButton { get; set; }

        public bool IndicatorsHorizontal { get; set; }

        public bool RightHideInfo { get; set; }

        public bool MainAreaCaptionMargin { get; set; }

        public bool ScreenFilterHidden { get; internal set; }

        public bool ScreenFpsHidden { get; internal set; }

        public bool ScreenResolutionHidden { get; internal set; }

        public int ChannelTemplate { get; internal set; }

        public double ChannelWidth { get; internal set; }

        public bool HidePromo { get; set; }
    }

    public class ResponsiveHostSizes
    {
        public Rect screen;
        public Rect screenArea;
        public Rect main;
        public Rect screenSideLeft;
        public Rect screenSideRight;
    }
}
