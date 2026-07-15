using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DSDsp
{
    /// <summary>
    /// DisplayWindow.xaml の相互作用ロジック
    /// 画面を表示するための専用ウィンドウ
    /// </summary>
    public partial class DisplayWindow : Window
    {
        private UserControl? _currentScreen;
        private UserControl? _currentSubScreen;

        /// <summary>
        /// 現在表示中のメイン画面
        /// </summary>
        public UserControl? CurrentScreen => _currentScreen;

        /// <summary>
        /// 現在表示中のSUB画面
        /// </summary>
        public UserControl? CurrentSubScreen => _currentSubScreen;

        /// <summary>
        /// VisualBrush ミラーモードを有効にする。
        /// MirrorBrush.Visual に source を直接セットしてミラー表示を開始する。
        /// </summary>
        public void SetMirrorSource(Visual source)
        {
            MirrorBrush.Visual = source;
            MirrorRect.Visibility = Visibility.Visible;
            ContentGrid.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// ミラーモードを解除して通常の ShowScreen モードに戻す。
        /// </summary>
        public void ClearMirror()
        {
            MirrorRect.Visibility = Visibility.Collapsed;
            MirrorBrush.Visual = null;
            ContentGrid.Visibility = Visibility.Visible;
        }

        public DisplayWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// ウィンドウ全体の背景をイメージファイルで設定する。
        /// ファイル名のみを渡すと、アセンブリに埋め込まれた pack://application リソース
        /// （イメージ フォルダ）から読み込む。
        /// </summary>
        /// <param name="imageFileName">ファイル名のみ（例: "CHR169_背景01.png"）</param>
        /// <returns>実際に参照した pack URI 文字列（ログ用）</returns>
        public (string packUri, bool exists) SetBackgroundImage(string imageFileName)
        {
            var packUri = $"pack://application:,,,/DSDsp;component/イメージ/{imageFileName}";
            var uri = new System.Uri(packUri, System.UriKind.Absolute);

            // リソースとして存在するか確認
            var info = Application.GetResourceStream(uri);
            bool exists = (info != null);
            info?.Stream.Dispose();

            if (exists)
            {
                var bitmap = new BitmapImage(uri);
                var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                this.Background  = brush;
                RootGrid.Background    = brush;
                ContentGrid.Background = brush;
            }

            return (packUri, exists);
        }

        /// <summary>
        /// ウィンドウ全体の背景を RGB 色で設定する。
        /// </summary>
        public void SetBackgroundColor(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            this.Background  = brush;
            RootGrid.Background    = brush;
            ContentGrid.Background = brush;
        }

        /// <summary>
        /// ウィンドウ全体の背景を黒（デフォルト）に戻す。
        /// </summary>
        public void ClearBackground()
        {
            this.Background  = Brushes.Black;
            RootGrid.Background    = Brushes.Black;
            ContentGrid.Background = Brushes.Black;
        }

        /// <summary>
        /// 指定されたメイン画面を表示
        /// </summary>
        /// <param name="screen">表示する画面（UserControl）</param>
        public void ShowScreen(UserControl screen)
        {
            // 既存の画面があれば削除
            if (_currentScreen != null)
            {
                ContentGrid.Children.Remove(_currentScreen);
                
                // IDisposableを実装している場合は破棄
                if (_currentScreen is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            // 新しい画面を設定
            _currentScreen = screen;
            // ContentGrid のサイズに合わせてストレッチ表示
            _currentScreen.Width = ContentGrid.Width;
            _currentScreen.Height = ContentGrid.Height;
            ContentGrid.Children.Add(_currentScreen);
        }

        /// <summary>
        /// 指定されたSUB画面を表示（メインの上に透明背景で重ねて表示）
        /// </summary>
        /// <param name="screen">表示するSUB画面（UserControl）</param>
        public void ShowSubScreen(UserControl screen)
        {
            // 既存のSUB画面があれば削除
            if (_currentSubScreen != null)
            {
                SubContentGrid.Children.Remove(_currentSubScreen);

                if (_currentSubScreen is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            // 新しいSUB画面を設定
            _currentSubScreen = screen;
            _currentSubScreen.Width  = SubContentGrid.Width;
            _currentSubScreen.Height = SubContentGrid.Height;
            SubContentGrid.Children.Add(_currentSubScreen);
        }

        /// <summary>
        /// メイン画面をクリア
        /// </summary>
        public void ClearScreen()
        {
            if (_currentScreen != null)
            {
                ContentGrid.Children.Remove(_currentScreen);
                
                if (_currentScreen is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                _currentScreen = null;
            }
        }

        /// <summary>
        /// SUB画面をクリア
        /// </summary>
        public void ClearSubScreen()
        {
            if (_currentSubScreen != null)
            {
                SubContentGrid.Children.Remove(_currentSubScreen);

                if (_currentSubScreen is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _currentSubScreen = null;
            }
        }

        /// <summary>
        /// ウィンドウを閉じる際のクリーンアップ
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // メインウィンドウが閉じられる場合のみ、表示ウィンドウも閉じる
            // それ以外の場合（ユーザーが閉じボタンを押した場合）は、閉じる操作をキャンセルして非表示にする
            if (Application.Current.MainWindow?.IsLoaded == true)
            {
                // メインウィンドウがまだ開いている場合は、閉じずに非表示にする
                e.Cancel = true;
                this.Visibility = Visibility.Hidden;
                return;
            }

            // メインウィンドウが閉じられている場合は、クリーンアップして閉じる
            if (_currentScreen != null)
            {
                ContentGrid.Children.Remove(_currentScreen);
                
                if (_currentScreen is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                _currentScreen = null;
            }

            if (_currentSubScreen != null)
            {
                SubContentGrid.Children.Remove(_currentSubScreen);

                if (_currentSubScreen is IDisposable disposable2)
                {
                    disposable2.Dispose();
                }

                _currentSubScreen = null;
            }

            base.OnClosing(e);
        }
    }
}

// Made with Bob
