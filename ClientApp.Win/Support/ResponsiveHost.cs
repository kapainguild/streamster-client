using Streamster.ClientCore.Models;
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

        public static readonly DependencyProperty LayoutTypeProperty =
            DependencyProperty.Register("LayoutType", typeof(LayoutType), typeof(ResponsiveHost), new FrameworkPropertyMetadata(LayoutType.Standart, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ChannelCountProperty =
            DependencyProperty.Register("ChannelCount", typeof(int), typeof(ResponsiveHost), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static ResponsiveHostCalculation GetCalculation(DependencyObject element) => (ResponsiveHostCalculation)element?.GetValue(CalculationProperty);
        public static void SetCalculation(DependencyObject element, ResponsiveHostCalculation value) => element?.SetValue(CalculationProperty, value);

        public LayoutType LayoutType
        {
            get => (LayoutType)GetValue(LayoutTypeProperty);
            set => SetValue(LayoutTypeProperty, value);
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
            var sizes = Snap(AddDerivedValues(Calculate(constraint)));
            var values = Calculate(sizes, constraint);

            SetCalculation(this, new ResponsiveHostCalculation
            {
                Sizes = sizes,
                Values = values
            });

            return base.MeasureOverride(constraint);
        }

        private ResponsiveHostSizes AddDerivedValues(ResponsiveHostSizes sizes)
        {
            var main = sizes.main;
            if (main.Width > 0)
            {
                if (main.X != sizes.screenArea.X)
                {
                    // horizontal layout
                    sizes.editing = new Rect(main.X, main.Y + 30, main.Width, main.Height - 30); 
                }
                else
                {
                    sizes.editing = new Rect(sizes.screen.X, main.Y, sizes.screen.Width, Math.Min(main.Height - 4, 300));
                }
            }
            return sizes;
        }

        private ResponsiveHostValues Calculate(ResponsiveHostSizes sizes, Size constraint)
        {
            bool rightTwoColumns = sizes.screenSideRight.Width > 105;
            var res = new ResponsiveHostValues
            {
                HideBitrateTitle = sizes.main.Width < 500,
                HideBitrate = sizes.main.Width < 300 || LayoutType == LayoutType.ScreenAndIndicators,
                IndicatorsHorizontal = sizes.screenSideLeft.Height < HorizontalIndicatorHeight + 1,
                RightTwoColumns = rightTwoColumns,
                RightHideSettings = rightTwoColumns ?
                                        sizes.screenSideRight.Height < 250 :
                                        sizes.screenSideRight.Height < 310,
                RightHideInfo = rightTwoColumns ?
                                        sizes.screenSideRight.Height < 250 :
                                        sizes.screenSideRight.Height < 400,
                RightHideSliders = sizes.screenSideRight.Height < 250,
                MainAreaCaptionMargin = sizes.main.Top < HeaderHeight,
                ScreenSourcesHidden = sizes.screen.Width < 415,
                ScreenFpsHidden = sizes.screen.Width < 615,
                ScreenResolutionHidden = sizes.screen.Width < 545,
                EditingTooSmall = sizes.editing.Width < 680 || sizes.editing.Height < 150,

                EditingTabsHideMaximize = sizes.editing.Width > 860 && sizes.editing.Width < 957,
                EditingTabsOnlyIcons = sizes.editing.Width <= 860
            };

            res.HidePromo = constraint.Width < 500 || constraint.Height < 500 || sizes.main.Height < 150 || res.HideBitrate;

            if (ChannelCount > 0)
            {
                var mainWidth = sizes.main.Width;
                var mainHeight = sizes.main.Height;
                if (res.MainAreaCaptionMargin) mainHeight -= 26;
                if (!res.HideBitrate) mainHeight -= 47;

                mainHeight -= 20;
                mainWidth -= 20;

                res.HideAddTargetButton = constraint.Width < 465 || constraint.Height < 300 || mainHeight < 80 || mainWidth < 200;
                if (!res.HideAddTargetButton)
                    mainWidth -= 56 + 20;

                double baseWidth = 230+16;
                double baseHeight = 110+10;

                var count = ChannelCount;

                if (!ChannelFits(baseWidth, baseHeight, mainWidth, mainHeight, count) &&
                    (mainWidth * mainHeight < 200000 || mainHeight < 80))
                {
                    res.ChannelTemplate = 1;

                    if (!res.HideAddTargetButton)
                    {
                        res.HideAddTargetButton = true;
                        mainWidth += 56 + 20;
                    }
                    res.ChannelWidth = 110;

                    for (int q = 0; q < 30; q++)
                    {
                        if (ChannelFits(res.ChannelWidth + 10, res.ChannelWidth + 10, mainWidth, mainHeight, count))
                            break;
                        res.ChannelWidth -= 2;
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
            if (LayoutType == LayoutType.NoScreen)
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
            else if (LayoutType == LayoutType.ScreenOnly)
            {
                return PlaceScreenOnly(a);
            }
            else if (LayoutType == LayoutType.ScreenAndIndicators)
            {
                int minimumMain = 95;
                var remainingHeight = a.Height - minimumMain - HorizontalIndicatorHeight;

                var width = remainingHeight * ScreenRatio;

                if (width > a.Width) // tall, small width
                {
                    var screenHeight = a.Width / ScreenRatio;
                    var marginY = (remainingHeight - screenHeight) / 2;
                    return new ResponsiveHostSizes
                    {
                        screen = new Rect(0, marginY, a.Width, screenHeight),
                        screenArea = new Rect(0, 0, a.Width, remainingHeight),
                        screenSideLeft = new Rect(0, remainingHeight, a.Width, HorizontalIndicatorHeight),
                        main = new Rect(0, remainingHeight + HorizontalIndicatorHeight, a.Width, minimumMain),
                    };
                }
                else
                {
                    // it is rather wide
                    var screenHeight = a.Height - minimumMain;
                    var screenWidth = a.Width - 2 * VerticalIndicatorWidth;

                    if (screenWidth / screenHeight > ScreenRatio)
                    {
                        var leftRightWidth = (a.Width - screenHeight * ScreenRatio) / 2;
                        return new ResponsiveHostSizes
                        {
                            screen = new Rect(leftRightWidth, 0, screenHeight * ScreenRatio, screenHeight),
                            screenArea = new Rect(0, 0, a.Width, screenHeight),
                            screenSideLeft = new Rect(0, 0, leftRightWidth, screenHeight),
                            screenSideRight = new Rect(leftRightWidth + screenHeight * ScreenRatio, 0, leftRightWidth, screenHeight),
                            main = new Rect(0, screenHeight, a.Width, minimumMain),
                        };
                    }
                    else
                    {
                        var marginY = (screenHeight - screenWidth / ScreenRatio) / 2;
                        return new ResponsiveHostSizes
                        {
                            screen = new Rect(VerticalIndicatorWidth, marginY, screenWidth, screenWidth / ScreenRatio),
                            screenArea = new Rect(0, 0, a.Width, screenHeight),
                            screenSideLeft = new Rect(0, 0, VerticalIndicatorWidth, screenHeight),
                            screenSideRight = new Rect(VerticalIndicatorWidth + screenWidth, 0, VerticalIndicatorWidth, screenHeight),
                            main = new Rect(0, screenHeight, a.Width, minimumMain),
                        };
                    }
                }
            }

            bool norrow = a.Width < 450;
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
                bool smallHeight = a.Height < 250 || a.Height < 600 && a.Width > a.Height * 3;

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

                var idealForMain = Math.Min(MainAreaToScreenRatio * a.Height, 310);

                if (remainingForMain < idealForMain)
                {
                    mainHeight = idealForMain;
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
            screenSideRight = Snap(s.screenSideRight),
            editing = Snap(s.editing)

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

        public bool RightHideSettings { get; set; }

        public bool RightHideSliders { get; set; }

        public bool RightTwoColumns { get; set; }

        public bool MainAreaCaptionMargin { get; set; }

        public bool ScreenSourcesHidden { get; set; }

        public bool ScreenFpsHidden { get; internal set; }

        public bool ScreenResolutionHidden { get; internal set; }

        public int ChannelTemplate { get; internal set; }

        public double ChannelWidth { get; internal set; }

        public bool HidePromo { get; set; }

        public bool EditingTooSmall { get; set; }

        public bool EditingTabsHideMaximize { get; set; }

        public bool EditingTabsOnlyIcons { get; set; }
    }

    public class ResponsiveHostSizes
    {
        public Rect screen;
        public Rect screenArea;
        public Rect main;
        public Rect screenSideLeft;
        public Rect screenSideRight;
        public Rect editing;
    }
}
