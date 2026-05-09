using System;
using System.Windows;
using System.Windows.Controls;

namespace MyCraftyStash.Views
{
    public partial class EnvelopeExpertView : UserControl
    {
        private const float EnvelopeMargin = 0.56125f;
        private const float BoxMargin      = 0.53125f;
        private bool _isBoxMode = false;
        private bool _loaded    = false;

        public EnvelopeExpertView()
        {
            InitializeComponent();
            _loaded = true;
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loaded) return;
            _isBoxMode = RadioButton_Box.IsChecked == true;
            Panel_BoxDepth.Visibility  = _isBoxMode ? Visibility.Visible   : Visibility.Collapsed;
            Label_Height.Text          = _isBoxMode ? "Length" : "Height";
            EnvelopeResults.Visibility = _isBoxMode ? Visibility.Collapsed : Visibility.Visible;
            BoxResults.Visibility      = _isBoxMode ? Visibility.Visible   : Visibility.Collapsed;
            Calculate();
        }

        private void Input_Changed(object sender, EventArgs e)
        {
            if (!_loaded) return;
            Calculate();
        }

        private void Calculate()
        {
            if (!_loaded) return;
            HideError();
            ResultsPanel.Visibility = Visibility.Collapsed;

            if (!TryParseInput(TextBox_Width.Text, ComboBox_WidthFraction, out float width))   return;
            if (!TryParseInput(TextBox_Height.Text, ComboBox_HeightFraction, out float height)) return;
            if (width <= 0 || height <= 0) return;

            if (_isBoxMode)
            {
                if (!TryParseInput(TextBox_BoxDepth.Text, ComboBox_BoxDepthFraction, out float depth)) return;
                if (depth <= 0) return;
                CalculateBox(width, height, depth);
            }
            else
            {
                CalculateEnvelope(width, height);
            }

            ResultsPanel.Visibility = Visibility.Visible;
        }

        private void CalculateEnvelope(float width, float height)
        {
            float length = Math.Max(width, height);
            float w      = Math.Min(width, height);
            double calcLength = length * Math.Sqrt(0.5);
            double calcWidth  = w      * Math.Sqrt(0.5);
            double paperSize  = calcWidth + calcLength + (EnvelopeMargin * 2);
            double scoreLine  = calcLength + EnvelopeMargin;
            Result_PaperSize.Text = ToFractionString(paperSize, 8) + " inches";
            Result_ScoreLine.Text = ToFractionString(scoreLine, 8) + " inches";
        }

        private void CalculateBox(float width, float length, float depth)
        {
            double calcLength   = length * Math.Sqrt(0.5);
            double calcWidth    = width  * Math.Sqrt(0.5);
            double calcDepth    = depth  * Math.Sqrt(0.5);
            double paperSize    = calcWidth + calcLength + (2 * (calcDepth + BoxMargin));
            double firstScore   = BoxMargin + calcWidth;
            double secondScore  = (BoxMargin + calcWidth) + (2 * calcDepth);
            Result_BoxPaperSize.Text   = ToFractionString(paperSize,   8) + " inches";
            Result_BoxFirstScore.Text  = ToFractionString(firstScore,  8) + " inches";
            Result_BoxSecondScore.Text = ToFractionString(secondScore, 8) + " inches";
        }

        private bool TryParseInput(string wholeText, ComboBox fractionCombo, out float result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(wholeText)) return false;
            if (!int.TryParse(wholeText.Trim(), out int whole) || whole < 0)
            {
                ShowError("Please enter valid whole numbers for dimensions.");
                return false;
            }
            float frac = 0;
            if (fractionCombo?.SelectedItem is ComboBoxItem item)
                frac = FractionToDecimal(item.Content?.ToString() ?? "0");
            result = whole + frac;
            return true;
        }

        private static float FractionToDecimal(string fraction) => fraction switch
        {
            "1/8" => 0.125f, "1/4" => 0.25f,  "3/8" => 0.375f,
            "1/2" => 0.50f,  "5/8" => 0.625f, "3/4" => 0.75f,
            "7/8" => 0.875f, _ => 0f
        };

        private string ToFractionString(double value, int denominator)
        {
            int whole     = (int)Math.Floor(value);
            double rem    = value - whole;
            int numerator = (int)Math.Round(rem * denominator);
            int g         = GCD(Math.Abs(numerator), denominator);
            numerator /= g;
            int denom  = denominator / g;
            if (numerator == denom) return $"{whole + 1}";
            if (numerator == 0)    return $"{whole}";
            if (whole == 0)        return $"{numerator}/{denom}";
            return $"{whole} {numerator}/{denom}";
        }

        private static int GCD(int a, int b) => b == 0 ? a : GCD(b, a % b);

        private void ShowError(string message)
        {
            ErrorText.Text       = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HideError() => ErrorText.Visibility = Visibility.Collapsed;
    }
}
