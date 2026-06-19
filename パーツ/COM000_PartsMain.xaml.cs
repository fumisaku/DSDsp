using System;
using System.Collections.Generic;
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



    }
}
