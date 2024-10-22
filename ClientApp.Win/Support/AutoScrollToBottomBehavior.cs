using Streamster.ClientCore.Support;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Streamster.ClientApp.Win.Support
{
    /// Source: https://stackoverflow.com/questions/2337822/wpf-listbox-scroll-to-end-automatically/66362812


    /// <summary>
    /// This will auto scroll a list view to the bottom as items are added.
    /// Automatically suspends if the user scrolls up, and recommences when
    /// the user scrolls to the end.
    /// </summary>
    /// <example>
    ///     <ListView sf:AutoScrollToBottomBehavior="{Binding viewModelAutoScrollFlag}" />
    /// </example>
    public class AutoScrollToBottomBehavior
    {
        /// <summary>
        /// Enumerated type to keep track of the current auto scroll status
        /// </summary>

        public static AutoScollingStatusType GetAutoScrollToBottomStatus(DependencyObject obj)
        {
            return (AutoScollingStatusType)obj.GetValue(AutoScrollToBottomStatusProperty);
        }

        public static void SetAutoScrollToBottomStatus(DependencyObject obj, AutoScollingStatusType value)
        {
            obj.SetValue(AutoScrollToBottomStatusProperty, value);
        }

        // Using a DependencyProperty as the backing store for AutoScrollToBottomStatus.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AutoScrollToBottomStatusProperty =
            DependencyProperty.RegisterAttached(
              "AutoScrollToBottomStatus",
              typeof(AutoScollingStatusType),
              typeof(AutoScrollToBottomBehavior),
              new PropertyMetadata(AutoScollingStatusType.NotAutoScrollingToBottom, (s, e) =>
              {
                  if (s is DependencyObject viewer && e.NewValue is AutoScollingStatusType autoScrollToBottomStatus)
                  {
                      // Set the AutoScrollToBottom property to mirror this one

                      bool? autoScrollToBottom = autoScrollToBottomStatus switch
                      {
                          AutoScollingStatusType.AutoScrollingToBottom => true,
                          AutoScollingStatusType.NotAutoScrollingToBottom => false,
                          AutoScollingStatusType.AutoScrollingToBottomButSuppressed => false,
                          _ => null
                      };

                      if (autoScrollToBottom.HasValue)
                      {
                          SetAutoScrollToBottom(viewer, autoScrollToBottom.Value);
                      }

                      // Only hook/unhook for cases below, not when suspended
                      switch (autoScrollToBottomStatus)
                      {
                          case AutoScollingStatusType.AutoScrollingToBottom:
                              HookViewer(viewer);
                              break;
                          case AutoScollingStatusType.NotAutoScrollingToBottom:
                              UnhookViewer(viewer);
                              break;
                      }
                  }
              }));


        public static bool GetAutoScrollToBottom(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToBottomProperty);
        }

        public static void SetAutoScrollToBottom(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToBottomProperty, value);
        }

        // Using a DependencyProperty as the backing store for AutoScrollToBottom.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AutoScrollToBottomProperty =
            DependencyProperty.RegisterAttached(
              "AutoScrollToBottom",
              typeof(bool),
              typeof(AutoScrollToBottomBehavior),
              new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (s, e) =>
              {
                  if (s is DependencyObject viewer && e.NewValue is bool autoScrollToBottom)
                  {
                      // Set the AutoScrollToBottomStatus property to mirror this one
                      if (autoScrollToBottom)
                      {
                          SetAutoScrollToBottomStatus(viewer, AutoScollingStatusType.AutoScrollingToBottom);
                      }
                      else if (GetAutoScrollToBottomStatus(viewer) == AutoScollingStatusType.AutoScrollingToBottom)
                      {
                          SetAutoScrollToBottomStatus(viewer, AutoScollingStatusType.NotAutoScrollingToBottom);
                      }

                      // No change if autoScrollToBottom = false && viewer.AutoScrollToBottomStatus = AutoScrollToBottomStatusType.AutoScrollingToBottomButSuppressed;
                  }
              }));


        private static Action GetUnhookAction(DependencyObject obj)
        {
            return (Action)obj.GetValue(UnhookActionProperty);
        }

        private static void SetUnhookAction(DependencyObject obj, Action value)
        {
            obj.SetValue(UnhookActionProperty, value);
        }

        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        private static readonly DependencyProperty UnhookActionProperty =
            DependencyProperty.RegisterAttached("UnhookAction", typeof(Action), typeof(AutoScrollToBottomBehavior), new PropertyMetadata(null));

        private static void ItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                itemsControl.Loaded -= ItemsControl_Loaded;
                HookViewer(itemsControl);
            }
        }

        private static void HookViewer(DependencyObject viewer)
        {
            if (viewer is ItemsControl itemsControl)
            {
                // If this is triggered the xaml setup then the control won't be loaded yet,
                // and so won't have a visual tree which we need to get the scrollviewer,
                // so defer this hooking until the items control is loaded.
                if (!itemsControl.IsLoaded)
                {
                    itemsControl.Loaded += ItemsControl_Loaded;
                    return;
                }
                else if (!itemsControl.IsVisible) 
                {
                    itemsControl.IsVisibleChanged += ItemsControl_IsVisibleChanged;
                    return;
                }

                var scrollViewer = FindScrollViewer(viewer) as ScrollViewer;
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToBottom();

                    // Scroll to bottom when the item count changes
                    NotifyCollectionChangedEventHandler itemsCollectionChangedHandler = (s, e) =>
                    {
                        if (GetAutoScrollToBottom(viewer))
                        {
                            scrollViewer.ScrollToBottom();
                        }
                    };
                    ((INotifyCollectionChanged)itemsControl.Items).CollectionChanged += itemsCollectionChangedHandler;


                    ScrollChangedEventHandler scrollChangedEventHandler = (s, e) =>
                    {
                        bool userScrolledToBottom = (e.VerticalOffset + e.ViewportHeight) > (e.ExtentHeight - 1.0);
                        bool userScrolledUp = e.VerticalChange < 0;

                        // Check if auto scrolling should be suppressed
                        if (userScrolledUp && !userScrolledToBottom)
                        {
                            if (GetAutoScrollToBottomStatus(viewer) == AutoScollingStatusType.AutoScrollingToBottom)
                            {
                                SetAutoScrollToBottomStatus(viewer, AutoScollingStatusType.AutoScrollingToBottomButSuppressed);
                            }
                        }

                        // Check if auto scrolling should be unsuppressed
                        if (userScrolledToBottom)
                        {
                            if (GetAutoScrollToBottomStatus(viewer) == AutoScollingStatusType.AutoScrollingToBottomButSuppressed)
                            {
                                SetAutoScrollToBottomStatus(viewer, AutoScollingStatusType.AutoScrollingToBottom);
                            }
                        }
                    };

                    DependencyPropertyChangedEventHandler dependencyPropertyChangedEventHandler = (s, e) =>
                    {
                        if (e.NewValue is bool visibility && visibility)
                        {
                            if (GetAutoScrollToBottom(viewer))
                            {
                                scrollViewer.ScrollToBottom();
                            }
                        }
                    };

                    scrollViewer.ScrollChanged += scrollChangedEventHandler;
                    scrollViewer.IsVisibleChanged += dependencyPropertyChangedEventHandler;

                    Action unhookAction = () =>
                    {
                        ((INotifyCollectionChanged)itemsControl.Items).CollectionChanged -= itemsCollectionChangedHandler;
                        scrollViewer.ScrollChanged -= scrollChangedEventHandler;
                        scrollViewer.IsVisibleChanged -= dependencyPropertyChangedEventHandler;
                    };

                    SetUnhookAction(viewer, unhookAction);
                }
            }
        }

        private static void ItemsControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                itemsControl.IsVisibleChanged -= ItemsControl_IsVisibleChanged;
                itemsControl.ApplyTemplate();
                HookViewer(itemsControl);
            }
        }

        /// <summary>
        /// Unsubscribes the event listeners on the ItemsControl and ScrollViewer
        /// </summary>
        /// <param name="viewer"></param>
        private static void UnhookViewer(DependencyObject viewer)
        {
            var unhookAction = GetUnhookAction(viewer);
            SetUnhookAction(viewer, null);
            unhookAction?.Invoke();
        }

        /// <summary>
        /// A recursive function that drills down a visual tree until a ScrollViewer is found.
        /// </summary>
        /// <param name="viewer"></param>
        /// <returns></returns>
        private static ScrollViewer FindScrollViewer(DependencyObject viewer)
        {
            if (viewer is ScrollViewer scrollViewer)
                return scrollViewer;

            return Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(viewer))
              .Select(i => FindScrollViewer(VisualTreeHelper.GetChild(viewer, i)))
              .Where(child => child != null)
              .FirstOrDefault();
        }
    }
}
