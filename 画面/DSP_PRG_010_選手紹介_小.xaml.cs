using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_010_選手紹介_小.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (case 0): 全要素を非表示 → COM001（ロゴ・競技会名・区分名）、COM002（"表彰式"）を表示
    ///   STEP2 (case 1): DV_Result から指定順位の選手情報を取得してフェードイン表示
    ///                     IM_種目1/2 を左右スライド＋フェードイン
    ///                     → 完了後に LB_区分名、LB_順位、LB_選手名 をフェードイン
    ///                     → AJS採点の場合: LB_得点（総合得点）をフェードイン（LB_所属は非表示）
    ///                     → AJS以外の場合: LB_所属 をフェードイン
    ///   STEP3 (case 2): 何もしない（テロップ表示保持 → 次の再生ボタン待ち）
    ///   STEP4 (case 3): STEP2 で表示したものをフェードアウト → RaiseScreenCompleted()
    ///
    /// 操作フロー: 再生1回目＝STEP1クリア、再生2回目＝テロップ表示、
    ///             再生3回目＝保持（何もしない）、再生4回目＝フェードアウト→次選手へ
    /// </summary>
    public partial class DSP_PRG_010_選手紹介_小 : DSDspScreenBase
    {
        #region 定数定義
        private const double SLIDE_FROM_LEFT         = -1000;
        private const double SLIDE_FROM_RIGHT        = 1000;
        private const int    FADE_DELAY_MILLISECONDS = 800;
        private const string FONT_FAMILY_NAME        = "Segoe UI Semibold";

        // フォントサイズ調整定数（TIT005 の LB 幅 417px に合わせる）
        private const double MAX_WIDTH = 400;
        private const double MAX_FS    = 16;
        private const double MIN_FS    = 6;
        #endregion

        #region プロパティ
        protected override int TotalSteps => 4;

        /// <summary>
        /// 表示する選手の総合順位番号（1から始まる整数）。
        /// MainWindow から設定する。0 以下の場合は最高位（1位）を表示。
        /// </summary>
        public int 順位番号 { get; set; } = 1;

        /// <summary>
        /// オナーダンス表示モード。true のとき:
        ///   - 得点は非表示
        ///   - LB_順位 の代わりに背番号を表示
        ///   - 所属を常に表示（AJS採点でも）
        /// </summary>
        public bool HonorMode { get; set; } = false;

        /// <summary>HonorMode 時に LB_順位 に表示する背番号。</summary>
        public string HonorBango { get; set; } = string.Empty;

        /// <summary>HonorMode 時に LB_所属 に表示する所属。</summary>
        public string HonorAffiliation { get; set; } = string.Empty;

        /// <summary>
        /// Step1 で COM002 に表示するテキストのオーバーライド。
        /// 空の場合は既定値（"表彰式"）を使用する。
        /// ジャッジ紹介など用途変用時に MainWindow から設定する。
        /// </summary>
        public string COM002TextOverride { get; set; } = string.Empty;

        /// <summary>
        /// HonorMode 時に LB_区分名 に表示するテキストのオーバーライド。
        /// null の場合はデフォルト（区分名）を使用。
        /// </summary>
        public string? HonorLB区分名Override { get; set; } = null;

        /// <summary>
        /// HonorMode 時に LB_選手名 に表示するテキストのオーバーライド。
        /// null の場合はデフォルト（選手名）を使用。
        /// </summary>
        public string? HonorLB選手名Override { get; set; } = null;
        #endregion

        #region コンストラクタ
        public DSP_PRG_010_選手紹介_小()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }
        #endregion

        #region イベントハンドラ
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();

            // Advance() が先に呼ばれて Step1 以降が実行済みの場合は非表示化をスキップする。
            // ShowScreen → Advance() の順に呼んだとき、WPF の Loaded イベントが
            // Dispatcher キュー経由で後から発火し、Step1 の設定を上書きしてしまうのを防ぐ。
            if (_currentStep == 0)
                非表示All();

            // クロマキ背景色をAppSettingsから設定
            try
            {
                var colorStr = AppSettings.Instance.ChromaKeySettings.BackgroundColor;
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                if (RootGrid != null)
                    RootGrid.Background = new System.Windows.Media.SolidColorBrush(color);
            }
            catch { /* デフォルト（黒）のまま */ }
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            switch (_currentStep)
            {
                case 0: Step1(); break;
                case 1: Step2(); break;
                case 2: /* STEP3: 保持（何もしない） */ break;
                case 3: Step4(); break;
            }
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: 全要素を非表示 → COM001・COM002 を設定して表示</summary>
        public void Step1()
        {
            EnsurePartsMainInitialized();

            // 全要素を非表示・クリア
            非表示All();

            // COM001: ロゴ・競技会名・区分名
            PartsCOM001.IM_JDSFマーク.Source = new BitmapImage(
                new Uri("pack://application:,,,/DSDsp;component/イメージ/JDSFマーク.png"));
            PartsCOM001.TB_左上1.Text = DSDspDataHelper.Get競技会名(DA_Master);
            PartsCOM001.TB_左上2.Text = DSDspDataHelper.Get区分名(DA_Master, 区分番号);

            // COM002: "表彰式"（オーバーライドがあればそちらを使用）
            PartsCOM002.LB_右上.Content = string.IsNullOrEmpty(COM002TextOverride)
                ? "表彰式"
                : COM002TextOverride;

            // COM001: 左上02はデフォルトで区分名。ジャッジ紹介用にオーバーライド可能。
            // （TB_左上1 は既に設定済み）
            // ※ COM002TextOverride が設定されている場合（=ジャッジ紹介用途）は左上02をブランクに
            if (!string.IsNullOrEmpty(COM002TextOverride))
                PartsCOM001.TB_左上2.Text = string.Empty;

            // COM003: クリア
            PartsCOM003.LB_右上.Content = string.Empty;
        }

        /// <summary>STEP2: DV_Result から指定順位の選手情報を取得してフェードイン表示</summary>
        public void Step2()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            // ── DV_Result から指定順位の選手データ取得 ──
            var 総合リスト = DSDspDataHelper.Get総合結果リスト(DV_Result);

            int targetRank = 順位番号 > 0 ? 順位番号 : 1;
            var entry = 総合リスト.Find(e => e.順位番号 == targetRank);
            if (entry == default && 総合リスト.Count > 0)
                entry = 総合リスト[0];

            string 背番号   = entry.背番号  ?? string.Empty;
            string 順位表記 = entry.順位表記 ?? string.Empty;
            if (string.IsNullOrEmpty(順位表記))
                順位表記 = targetRank > 0 ? $"{targetRank}位" : string.Empty;

            // DA_Master から選手名・所属を取得
            var 選手情報 = DSDspDataHelper.Get選手情報(DA_Master, 背番号, 区分番号);
            string 選手名L = DSDspDataHelper.Get選手名L(選手情報);
            string 選手名P = DSDspDataHelper.Get選手名P(選手情報);
            string 所属   = DSDspDataHelper.Get所属(選手情報);
            string 選手名  = string.IsNullOrEmpty(選手名P) ? 選手名L : $"{選手名L}・{選手名P}";

            // AJS採点かどうかを判定
            bool isAJS = DSDspDataHelper.IsAJS採点(DV_Result);

            PartsPRG006.LB_選手名.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;

            if (HonorMode)
            {
                // ── オナーダンス / ジャッジ紹介個別 用テキスト設定 ──
                // LB_区分名: オーバーライドが設定されている場合はそちらを使用
                if (HonorLB区分名Override != null)
                    PartsPRG006.LB_区分名.Content = HonorLB区分名Override;
                // LB_順位: ジャッジ記号 / 背番号
                PartsPRG006.LB_順位.Content  = HonorBango;
                // LB_選手名: オーバーライドが設定されている場合はそちら（ジャッジ表記名）
                string lb選手名テキスト = HonorLB選手名Override
                    ?? (string.IsNullOrEmpty(選手名P) ? 選手名L : $"{選手名L}・{選手名P}");
                PartsPRG006.LB_選手名.Content = lb選手名テキスト;
                PartsPRG006.LB_所属.Content  = HonorAffiliation;
                PartsPRG006.LB_得点.Content  = string.Empty;
                _partsMain.フォントサイズ自動調整(PartsPRG006.LB_区分名,  PartsPRG006.LB_区分名.Content?.ToString() ?? "", MAX_WIDTH, 14, MIN_FS, FONT_FAMILY_NAME);
                _partsMain.フォントサイズ自動調整(PartsPRG006.LB_順位,    HonorBango,       MAX_WIDTH, MAX_FS, MIN_FS, FONT_FAMILY_NAME);
                _partsMain.フォントサイズ自動調整(PartsPRG006.LB_選手名,  lb選手名テキスト, MAX_WIDTH, MAX_FS, MIN_FS, FONT_FAMILY_NAME);
                _partsMain.フォントサイズ自動調整(PartsPRG006.LB_所属,    HonorAffiliation, MAX_WIDTH, MAX_FS, MIN_FS, FONT_FAMILY_NAME);
            }
            else
            {
                // ── 通常テキスト設定 ──
                string lb区分テキスト     = DSDspDataHelper.Get区分名(DA_Master, 区分番号);
                string lb選手紹介テキスト = $"{背番号}  {選手名}";
                string lb順位テキスト     = 順位表記;

                PartsPRG006.LB_区分名.Content = lb区分テキスト;
                PartsPRG006.LB_順位.Content   = lb順位テキスト;
                PartsPRG006.LB_選手名.Content = lb選手紹介テキスト;

                _partsMain.フォントサイズ自動調整(PartsPRG006.LB_区分名,  lb区分テキスト,     MAX_WIDTH, 14,      MIN_FS, FONT_FAMILY_NAME);
                _partsMain.フォントサイズ自動調整(PartsPRG006.LB_順位,    lb順位テキスト,     MAX_WIDTH, MAX_FS, MIN_FS, FONT_FAMILY_NAME);
                _partsMain.フォントサイズ自動調整(PartsPRG006.LB_選手名,  lb選手紹介テキスト, MAX_WIDTH, MAX_FS, MIN_FS, FONT_FAMILY_NAME);

                if (isAJS)
                {
                    // AJS採点: 所属の代わりに総合得点を表示
                    string lb得点テキスト = entry.得点.ToString("F3");
                    PartsPRG006.LB_得点.Content = lb得点テキスト;
                    PartsPRG006.LB_所属.Content = string.Empty;
                    _partsMain.フォントサイズ自動調整(PartsPRG006.LB_得点, lb得点テキスト, MAX_WIDTH, MAX_FS, MIN_FS, FONT_FAMILY_NAME);
                }
                else
                {
                    // AJS以外: 所属を表示
                    PartsPRG006.LB_所属.Content = 所属;
                    PartsPRG006.LB_得点.Content = string.Empty;
                    _partsMain.フォントサイズ自動調整(PartsPRG006.LB_所属, 所属, MAX_WIDTH, MAX_FS, MIN_FS, FONT_FAMILY_NAME);
                }
            }

            // ── IM_種目1/2 スライド＋フェードイン ──
            // ラベルは Collapsed のまま IM フェードイン完了後に表示する
            PartsPRG006.LB_区分名.Visibility = Visibility.Collapsed;
            PartsPRG006.LB_順位.Visibility   = Visibility.Collapsed;
            PartsPRG006.LB_選手名.Visibility = Visibility.Collapsed;
            PartsPRG006.LB_所属.Visibility   = Visibility.Collapsed;
            PartsPRG006.LB_得点.Visibility   = Visibility.Collapsed;

            PartsPRG006.IM_種目1.Opacity    = 0;
            PartsPRG006.IM_種目2.Opacity    = 0;
            PartsPRG006.IM_種目1.Visibility = Visibility.Visible;
            PartsPRG006.IM_種目2.Visibility = Visibility.Visible;

            CreateAndStartSlideAnimation(PartsPRG006.IM_種目1, SLIDE_FROM_RIGHT);
            CreateAndStartSlideAnimation(PartsPRG006.IM_種目2, SLIDE_FROM_LEFT);

            var imageSb = new Storyboard();
            _partsMain.フェードイン(true, PartsPRG006.IM_種目1, imageSb, 0);
            _partsMain.フェードイン(true, PartsPRG006.IM_種目2, imageSb, 0);

            // IM フェードイン完了後にラベルを Visible にしてフェードイン
            imageSb.Completed += (s, e) =>
            {
                PartsPRG006.LB_区分名.Opacity = 0;
                PartsPRG006.LB_順位.Opacity   = 0;
                PartsPRG006.LB_選手名.Opacity = 0;
                PartsPRG006.LB_所属.Opacity   = 0;
                PartsPRG006.LB_得点.Opacity   = 0;

                PartsPRG006.LB_区分名.Visibility = Visibility.Visible;
                PartsPRG006.LB_選手名.Visibility = Visibility.Visible;

                var lbSb = new Storyboard();
                _partsMain?.フェードイン(true, PartsPRG006.LB_区分名, lbSb, 0);
                _partsMain?.フェードイン(true, PartsPRG006.LB_選手名, lbSb, FADE_DELAY_MILLISECONDS);

                if (HonorMode)
                {
                    // オナーダンス: 得点非表示、LB_順位に背番号、LB_所属に所属を表示
                    PartsPRG006.LB_順位.Content    = HonorBango;
                    PartsPRG006.LB_所属.Content    = HonorAffiliation;
                    PartsPRG006.LB_順位.Visibility = Visibility.Visible;
                    PartsPRG006.LB_所属.Visibility = Visibility.Visible;
                    _partsMain?.フェードイン(true, PartsPRG006.LB_順位, lbSb, FADE_DELAY_MILLISECONDS);
                    _partsMain?.フェードイン(true, PartsPRG006.LB_所属, lbSb, FADE_DELAY_MILLISECONDS);
                }
                else if (isAJS)
                {
                    // AJS採点: 総合得点をフェードイン
                    PartsPRG006.LB_順位.Visibility = Visibility.Visible;
                    PartsPRG006.LB_得点.Visibility = Visibility.Visible;
                    _partsMain?.フェードイン(true, PartsPRG006.LB_順位, lbSb, FADE_DELAY_MILLISECONDS);
                    _partsMain?.フェードイン(true, PartsPRG006.LB_得点, lbSb, FADE_DELAY_MILLISECONDS);
                }
                else
                {
                    // AJS以外: 所属をフェードイン
                    PartsPRG006.LB_順位.Visibility = Visibility.Visible;
                    PartsPRG006.LB_所属.Visibility = Visibility.Visible;
                    _partsMain?.フェードイン(true, PartsPRG006.LB_順位, lbSb, FADE_DELAY_MILLISECONDS);
                    _partsMain?.フェードイン(true, PartsPRG006.LB_所属, lbSb, FADE_DELAY_MILLISECONDS);
                }

                lbSb.Begin();
            };
            imageSb.Begin();
        }

        /// <summary>STEP4: STEP2 で表示したものをフェードアウト → RaiseScreenCompleted</summary>
        public void Step4()
        {
            EnsurePartsMainInitialized();
            if (_partsMain == null) return;

            var sb = new Storyboard();
            _partsMain.フェードアウト(true, PartsPRG006.IM_種目1,  sb, 0);
            _partsMain.フェードアウト(true, PartsPRG006.IM_種目2,  sb, 0);
            _partsMain.フェードアウト(true, PartsPRG006.LB_区分名, sb, 0);
            _partsMain.フェードアウト(true, PartsPRG006.LB_順位,   sb, 0);
            _partsMain.フェードアウト(true, PartsPRG006.LB_選手名, sb, 0);
            _partsMain.フェードアウト(true, PartsPRG006.LB_所属,   sb, 0);
            _partsMain.フェードアウト(true, PartsPRG006.LB_得点,   sb, 0);

            sb.Completed += (s, e) => RaiseScreenCompleted();
            sb.Begin();
        }

        #endregion

        #region ヘルパー

        /// <summary>全パーツを非表示にしてラベルをクリア</summary>
        private void 非表示All()
        {
            PartsCOM001.TB_左上2.Text   = string.Empty;
            PartsCOM002.LB_右上.Content = string.Empty;
            PartsCOM003.LB_右上.Content = string.Empty;

            PartsPRG006.LB_区分名.Visibility   = Visibility.Collapsed;
            PartsPRG006.LB_順位.Visibility = Visibility.Collapsed;
            PartsPRG006.LB_選手名.Visibility = Visibility.Collapsed;
            PartsPRG006.LB_所属.Visibility     = Visibility.Collapsed;
            PartsPRG006.LB_得点.Visibility = Visibility.Collapsed;

            PartsPRG006.IM_種目1.Visibility    = Visibility.Collapsed;
            PartsPRG006.IM_種目2.Visibility    = Visibility.Collapsed;

            PartsPRG006.LB_区分名.Content   = string.Empty;
            PartsPRG006.LB_順位.Content = string.Empty;
            PartsPRG006.LB_選手名.Content = string.Empty;
            PartsPRG006.LB_所属.Content     = string.Empty;
            PartsPRG006.LB_得点.Content = string.Empty;

        }

        #endregion
    }
}

// Made with Bob
