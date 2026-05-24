using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using JandH.Core.Models;
using JandH.Core.Services;
using JandH.Core.ViewModels;

namespace MyCraftyStash.Controls
{
    /// <summary>
    /// An image control with double-click zoom toggle, click-and-drag pan when zoomed,
    /// and Ctrl + mouse-wheel finer zoom when zoomed.
    /// </summary>
    public partial class ZoomableImage : UserControl
    {
        // Zoom multipliers
        private const double DefaultZoomedScale = 2.0;
        private const double MinScale = 1.0;
        private const double MaxScale = 8.0;

        private bool _isZoomed;
        private bool _isPanning;
        private Point _panStart;
        private double _panStartTranslateX;
        private double _panStartTranslateY;

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(ImageSource), typeof(ZoomableImage),
                new PropertyMetadata(null, OnSourceChanged));

        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(ZoomableImage),
                new PropertyMetadata(Stretch.Uniform));

        public ImageSource? Source
        {
            get => (ImageSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public Stretch Stretch
        {
            get => (Stretch)GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        public ZoomableImage()
        {
            InitializeComponent();
            // Reset zoom whenever the source changes so a fresh image starts un-zoomed.
            MouseDoubleClick += OnMouseDoubleClick;
            PreviewMouseLeftButtonDown += OnLeftButtonDown;
            PreviewMouseMove += OnMouseMove;
            PreviewMouseLeftButtonUp += OnLeftButtonUp;
            PreviewMouseWheel += OnPreviewMouseWheel;
            Cursor = Cursors.Arrow;
        }

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZoomableImage zi)
                zi.ResetZoom();
        }

        private void ResetZoom()
        {
            _isZoomed = false;
            _isPanning = false;
            ImgScale.ScaleX = 1.0;
            ImgScale.ScaleY = 1.0;
            ImgTranslate.X = 0;
            ImgTranslate.Y = 0;
            Cursor = Cursors.Arrow;
        }

        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double-click toggles between fit-to-container and 2x zoom.
            if (_isZoomed)
            {
                ResetZoom();
            }
            else
            {
                ImgScale.ScaleX = DefaultZoomedScale;
                ImgScale.ScaleY = DefaultZoomedScale;
                ImgTranslate.X = 0;
                ImgTranslate.Y = 0;
                _isZoomed = true;
                Cursor = Cursors.SizeAll;
            }
            e.Handled = true;
        }

        private void OnLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // A double-click sends a single down + up first; the OnMouseDoubleClick handler
            // marks that round-trip as Handled. Here we only start panning on a real single press while zoomed.
            if (!_isZoomed) return;
            if (e.ClickCount > 1) return;

            _isPanning = true;
            _panStart = e.GetPosition(this);
            _panStartTranslateX = ImgTranslate.X;
            _panStartTranslateY = ImgTranslate.Y;
            Cursor = Cursors.Hand;
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;
            var current = e.GetPosition(this);
            ImgTranslate.X = _panStartTranslateX + (current.X - _panStart.X);
            ImgTranslate.Y = _panStartTranslateY + (current.Y - _panStart.Y);
        }

        private void OnLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;
            _isPanning = false;
            ReleaseMouseCapture();
            Cursor = _isZoomed ? Cursors.SizeAll : Cursors.Arrow;
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Only allow Ctrl+wheel finer-zoom when already in zoomed mode.
            if (!_isZoomed) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            const double step = 0.2;
            double delta = e.Delta > 0 ? step : -step;
            double newScale = Math.Max(MinScale, Math.Min(MaxScale, ImgScale.ScaleX + delta));

            ImgScale.ScaleX = newScale;
            ImgScale.ScaleY = newScale;

            // If user wheels back down to 1.0, exit zoomed mode entirely.
            if (Math.Abs(newScale - MinScale) < 0.01)
            {
                ResetZoom();
            }

            e.Handled = true;
        }
    }
}
