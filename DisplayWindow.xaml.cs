using System.Windows;
using System.Windows.Controls;

namespace DSDsp
{
    /// <summary>
    /// DisplayWindow.xaml の相互作用ロジック
    /// 画面を表示するための専用ウィンドウ
    /// </summary>
    public partial class DisplayWindow : Window
    {
        private UserControl? _currentScreen;

        /// <summary>
        /// 現在表示中の画面
        /// </summary>
        public UserControl? CurrentScreen => _currentScreen;

        public DisplayWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 指定された画面を表示
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
        /// 画面をクリア
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

            base.OnClosing(e);
        }
    }
}

// Made with Bob
