using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace CoupleFinance.Desktop.Presentation;

public static class SmoothScrollBehavior
{
    public static readonly DependencyProperty EnableProperty = DependencyProperty.RegisterAttached(
        "Enable",
        typeof(bool),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(false, OnEnableChanged));

    public static readonly DependencyProperty WheelChangeProperty = DependencyProperty.RegisterAttached(
        "WheelChange",
        typeof(double),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(88d));

    public static readonly DependencyProperty DurationProperty = DependencyProperty.RegisterAttached(
        "Duration",
        typeof(Duration),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(new Duration(TimeSpan.FromMilliseconds(240))));

    private static readonly DependencyProperty AnimatedVerticalOffsetProperty = DependencyProperty.RegisterAttached(
        "AnimatedVerticalOffset",
        typeof(double),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(0d, OnAnimatedVerticalOffsetChanged));

    private static readonly DependencyProperty IsAttachedProperty = DependencyProperty.RegisterAttached(
        "IsAttached",
        typeof(bool),
        typeof(SmoothScrollBehavior),
        new PropertyMetadata(false));

    public static bool GetEnable(DependencyObject element) => (bool)element.GetValue(EnableProperty);

    public static void SetEnable(DependencyObject element, bool value) => element.SetValue(EnableProperty, value);

    public static double GetWheelChange(DependencyObject element) => (double)element.GetValue(WheelChangeProperty);

    public static void SetWheelChange(DependencyObject element, double value) => element.SetValue(WheelChangeProperty, value);

    public static Duration GetDuration(DependencyObject element) => (Duration)element.GetValue(DurationProperty);

    public static void SetDuration(DependencyObject element, Duration value) => element.SetValue(DurationProperty, value);

    private static void OnEnableChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is not ScrollViewer scrollViewer)
        {
            return;
        }

        var enable = (bool)eventArgs.NewValue;
        var isAttached = (bool)scrollViewer.GetValue(IsAttachedProperty);

        if (enable && !isAttached)
        {
            scrollViewer.Loaded += ScrollViewerOnLoaded;
            scrollViewer.Unloaded += ScrollViewerOnUnloaded;
            scrollViewer.PreviewMouseWheel += ScrollViewerOnPreviewMouseWheel;
            scrollViewer.SetValue(IsAttachedProperty, true);
        }
        else if (!enable && isAttached)
        {
            scrollViewer.Loaded -= ScrollViewerOnLoaded;
            scrollViewer.Unloaded -= ScrollViewerOnUnloaded;
            scrollViewer.PreviewMouseWheel -= ScrollViewerOnPreviewMouseWheel;
            scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
            scrollViewer.SetValue(IsAttachedProperty, false);
        }
    }

    private static void ScrollViewerOnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.SetValue(AnimatedVerticalOffsetProperty, scrollViewer.VerticalOffset);
        }
    }

    private static void ScrollViewerOnUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
        }
    }

    private static void ScrollViewerOnPreviewMouseWheel(object sender, MouseWheelEventArgs eventArgs)
    {
        if (sender is not ScrollViewer scrollViewer ||
            scrollViewer.ScrollableHeight <= 0 ||
            scrollViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
        {
            return;
        }

        var step = GetWheelChange(scrollViewer);
        if (step <= 0)
        {
            return;
        }

        var currentOffset = scrollViewer.VerticalOffset;
        var requestedOffset = currentOffset - ((eventArgs.Delta / 120d) * step);
        var targetOffset = Math.Clamp(requestedOffset, 0d, scrollViewer.ScrollableHeight);

        if (Math.Abs(targetOffset - currentOffset) < 0.1d)
        {
            return;
        }

        eventArgs.Handled = true;
        scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, null);
        scrollViewer.SetValue(AnimatedVerticalOffsetProperty, currentOffset);

        var animation = new DoubleAnimation
        {
            From = currentOffset,
            To = targetOffset,
            Duration = GetDuration(scrollViewer),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        scrollViewer.BeginAnimation(AnimatedVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void OnAnimatedVerticalOffsetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (dependencyObject is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset((double)eventArgs.NewValue);
        }
    }
}
