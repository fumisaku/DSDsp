using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DSDsp.パーツ
{
    /// <summary>
    /// COM000_PartsMain.xaml の相互作用ロジック
    /// </summary>
    public partial class COM000_PartsMain : Window
    {
        public COM000_PartsMain()
        {
            InitializeComponent();
        }


        public void フェードイン(bool fadeFlag, UIElement obj, Storyboard sb, int beginTime)
        {
            if (obj != null)
            {
                if (obj.Opacity == 0)
                {
                    DoubleAnimation fadeInAnimation = new DoubleAnimation();

                    Storyboard.SetTarget(fadeInAnimation, obj);

                    fadeInAnimation.Duration = fadeFlag
                        ? new Duration(TimeSpan.FromMilliseconds(800))
                        : new Duration(TimeSpan.FromMilliseconds(250));

                    fadeInAnimation.BeginTime = TimeSpan.FromMilliseconds(beginTime);
                    fadeInAnimation.From = 0.0;
                    fadeInAnimation.To = 1.0;
                    fadeInAnimation.DecelerationRatio = 0.8;

                    Storyboard.SetTargetProperty(
                        fadeInAnimation,
                        new PropertyPath("Opacity"));

                    sb.Children.Add(fadeInAnimation);

                    obj.Visibility = Visibility.Visible;
                }
            }
        }

        public void フェードアウト(bool fadeFlag, UIElement obj, Storyboard sb, int beginTime)
        {
            if (obj != null)
            {
                DoubleAnimation fadeOutAnimation = new DoubleAnimation();

                Storyboard.SetTarget(fadeOutAnimation, obj);

                fadeOutAnimation.Duration = fadeFlag
                    ? new Duration(TimeSpan.FromMilliseconds(800))
                    : new Duration(TimeSpan.FromMilliseconds(100));

                fadeOutAnimation.BeginTime = TimeSpan.FromMilliseconds(beginTime);
                fadeOutAnimation.From = 1.0;
                fadeOutAnimation.To = 0.0;
                fadeOutAnimation.DecelerationRatio = 0.8;

                Storyboard.SetTargetProperty(
                    fadeOutAnimation,
                    new PropertyPath("Opacity"));

                sb.Children.Add(fadeOutAnimation);

                obj.Visibility = Visibility.Visible;
            }
        }

        #region フォントサイズ自動調整機能

        /// <summary>
        /// 文字列の長さに応じてフォントサイズを自動調整
        /// テキストの実際の幅を測定して、指定幅に収まる最大のフォントサイズを見つける
        /// </summary>
        /// <param name="label">対象のLabel</param>
        /// <param name="text">表示するテキスト</param>
        /// <param name="maxWidth">最大テキスト幅（デフォルト: 500px）</param>
        /// <param name="maxFontSize">最大フォントサイズ（デフォルト: 20）</param>
        /// <param name="minFontSize">最小フォントサイズ（デフォルト: 6）</param>
        /// <param name="fontFamilyName">フォントファミリー名（デフォルト: Segoe UI Semibold）</param>
        public void フォントサイズ自動調整(
            Label label,
            string text,
            double maxWidth = 500,
            double maxFontSize = 20,
            double minFontSize = 6,
            string fontFamilyName = "Segoe UI Semibold")
        {
            if (label == null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            if (string.IsNullOrEmpty(text))
            {
                label.FontSize = maxFontSize;
                return;
            }

            double fontSize = minFontSize;

            // フォントサイズを最大から最小まで1ずつ減らしながら、収まるサイズを探す
            for (double f = maxFontSize; f >= minFontSize; f--)
            {
                double textWidth = テキスト幅取得(text, fontFamilyName, f, label);
                
                if (textWidth < maxWidth)
                {
                    fontSize = f;
                    break;
                }
            }

            label.FontSize = fontSize;
        }

        /// <summary>
        /// 指定されたテキスト、フォントファミリー、フォントサイズでのテキストの幅を取得
        /// </summary>
        /// <param name="text">測定するテキスト</param>
        /// <param name="fontFamilyName">フォントファミリー名</param>
        /// <param name="fontSize">フォントサイズ</param>
        /// <param name="referenceElement">DPI取得用の参照要素</param>
        /// <returns>テキストの幅（ピクセル）</returns>
        public double テキスト幅取得(
            string text,
            string fontFamilyName,
            double fontSize,
            Visual referenceElement)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            try
            {
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(
                        new FontFamily(fontFamilyName),
                        FontStyles.Normal,
                        FontWeights.Bold,
                        FontStretches.Normal),
                    fontSize,
                    Brushes.Black,
                    new NumberSubstitution(),
                    VisualTreeHelper.GetDpi(referenceElement).PixelsPerDip);

                return formattedText.Width;
            }
            catch
            {
                // フォントが見つからない場合などのエラー時は、文字数ベースの概算値を返す
                return text.Length * fontSize * 0.6;
            }
        }

        #endregion
    }
}
