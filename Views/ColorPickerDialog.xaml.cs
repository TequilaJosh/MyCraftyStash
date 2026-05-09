using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MyCraftyStash.Views
{
    public partial class ColorPickerDialog : Window
    {
        private double _hue;        // 0..360
        private double _saturation; // 0..1
        private double _value;      // 0..1
        private bool _suppressInputSync;
        private bool _draggingSv;
        private bool _draggingHue;

        public Color SelectedColor { get; private set; }

        public ColorPickerDialog(Color initial, string? title = null)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(title))
                TitleText.Text = title!;

            SelectedColor = initial;
            OriginalSwatch.Background = new SolidColorBrush(initial);

            (_hue, _saturation, _value) = RgbToHsv(initial);
            Loaded += (_, _) => RefreshAll();
        }

        // ── Mouse handling on the SV square and Hue strip ──────────────────────

        private void Sv_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _draggingSv = true;
            SvCanvas.CaptureMouse();
            UpdateSvFromMouse(e.GetPosition(SvCanvas));
        }
        private void Sv_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingSv) UpdateSvFromMouse(e.GetPosition(SvCanvas));
        }
        private void Sv_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _draggingSv = false;
            SvCanvas.ReleaseMouseCapture();
        }

        private void Hue_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _draggingHue = true;
            HueCanvas.CaptureMouse();
            UpdateHueFromMouse(e.GetPosition(HueCanvas));
        }
        private void Hue_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingHue) UpdateHueFromMouse(e.GetPosition(HueCanvas));
        }
        private void Hue_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _draggingHue = false;
            HueCanvas.ReleaseMouseCapture();
        }

        private void UpdateSvFromMouse(Point p)
        {
            var w = SvCanvas.ActualWidth;
            var h = SvCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            _saturation = Clamp(p.X / w, 0, 1);
            _value = 1.0 - Clamp(p.Y / h, 0, 1);
            RefreshAll();
        }

        private void UpdateHueFromMouse(Point p)
        {
            var h = HueCanvas.ActualHeight;
            if (h <= 0) return;
            _hue = Clamp(p.Y / h, 0, 1) * 360.0;
            RefreshAll();
        }

        // ── Hex / RGB inputs ───────────────────────────────────────────────────

        private void HexBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitHex(); e.Handled = true; }
        }
        private void HexBox_LostFocus(object sender, RoutedEventArgs e) => CommitHex();

        private void CommitHex()
        {
            if (_suppressInputSync) return;
            var text = (HexBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                if (!text.StartsWith("#")) text = "#" + text;
                if (ColorConverter.ConvertFromString(text) is Color c)
                {
                    (_hue, _saturation, _value) = RgbToHsv(c);
                    RefreshAll();
                }
            }
            catch { /* invalid hex — ignore, user can retry */ }
        }

        private void RgbBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitRgb(); e.Handled = true; }
        }
        private void RgbBox_LostFocus(object sender, RoutedEventArgs e) => CommitRgb();

        private void CommitRgb()
        {
            if (_suppressInputSync) return;
            if (!byte.TryParse(RBox.Text, out var r)) return;
            if (!byte.TryParse(GBox.Text, out var g)) return;
            if (!byte.TryParse(BBox.Text, out var b)) return;
            (_hue, _saturation, _value) = RgbToHsv(Color.FromRgb(r, g, b));
            RefreshAll();
        }

        // ── Render: push state out to all UI elements ──────────────────────────

        private void RefreshAll()
        {
            var hueColor = HsvToRgb(_hue, 1, 1);
            var color = HsvToRgb(_hue, _saturation, _value);
            SelectedColor = color;

            // SV square base hue
            SvHueRect.Fill = new LinearGradientBrush(Colors.White, hueColor, new Point(0, 0), new Point(1, 0));

            // Markers
            var w = SvCanvas.ActualWidth;
            var h = SvCanvas.ActualHeight;
            if (w > 0 && h > 0)
            {
                Canvas.SetLeft(SvMarker, _saturation * w - SvMarker.Width / 2);
                Canvas.SetTop(SvMarker, (1 - _value) * h - SvMarker.Height / 2);
            }
            var hh = HueCanvas.ActualHeight;
            if (hh > 0)
            {
                Canvas.SetLeft(HueMarker, 0);
                Canvas.SetTop(HueMarker, (_hue / 360.0) * hh - 5);
            }

            // Preview swatch
            NewSwatch.Background = new SolidColorBrush(color);

            // Sync number boxes (avoid recursion via _suppressInputSync)
            _suppressInputSync = true;
            HexBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            RBox.Text = color.R.ToString();
            GBox.Text = color.G.ToString();
            BBox.Text = color.B.ToString();
            _suppressInputSync = false;
        }

        // ── OK / Cancel ────────────────────────────────────────────────────────

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Color math ─────────────────────────────────────────────────────────

        private static double Clamp(double v, double lo, double hi)
            => v < lo ? lo : v > hi ? hi : v;

        private static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            var c = v * s;
            var x = c * (1 - Math.Abs(((h / 60) % 2) - 1));
            var m = v - c;
            double r, g, b;
            if      (h <  60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else              { r = c; g = 0; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        private static (double H, double S, double V) RgbToHsv(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            double h = 0;
            if (delta > 0)
            {
                if (max == r)      h = 60 * (((g - b) / delta) % 6);
                else if (max == g) h = 60 * (((b - r) / delta) + 2);
                else               h = 60 * (((r - g) / delta) + 4);
            }
            if (h < 0) h += 360;
            double s = max == 0 ? 0 : delta / max;
            return (h, s, max);
        }
    }
}
