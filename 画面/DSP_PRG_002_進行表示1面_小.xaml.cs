using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DSDsp.画面
{
    /// <summary>
    /// DSP_PRG_002_進行表示1面_小.xaml の相互作用ロジック
    ///
    /// ステップ構成:
    ///   STEP1 (phase=0): COM001/COM002/COM003 を表示・更新
    ///   STEP2 (phase=1): IM_タイトル1/LB_タイトル1/IM_明細1/LB_明細1 をフェードインで表示
    ///   STEP3 (phase=2): STEP2 で表示したものをフェードアウトして非表示
    ///   STEP4: COM003 等を非表示 → RaiseScreenCompleted()
    /// </summary>
    public partial class DSP_PRG_002_進行表示1面_小 : DSDspScreenBase
    {
        #region 定数
        private const string FontFamilyName = "Segoe UI Semibold";
        #endregion

        #region フィールド
        private bool _closeRequested = false;
        #endregion

        #region プロパティ
        protected override int TotalSteps => 100;
        #endregion

        #region コンストラクタ
        public DSP_PRG_002_進行表示1面_小()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }
        #endregion

        #region イベントハンドラ
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsurePartsMainInitialized();
            HideAllParts();
        }
        #endregion

        #region オーバーライドメソッド
        protected override void ExecuteCurrentStep()
        {
            int phase = _currentStep % 3; // 0=STEP1, 1=STEP2, 2=STEP3
            if (phase == 0)
            {
                if (_closeRequested) Step4();
                else Step1();
            }
            else if (phase == 1) Step2();
            else Step3();
        }
        #endregion

        #region ステップ実装

        /// <summary>STEP1: COM001, COM002, COM003 を表示・更新。ラベルをクリア。</summary>
        private void Step1()
        {
            EnsurePartsMainInitialized();

            // ラベルクリア
            SetLabelContent(PartsLST006, "LB_明細1", string.Empty);

            if (PartsCOM001.FindName("TB_左上1") is TextBlock tb1)
                tb1.Text = DA_Master != null ? DSDspDataHelper.Get競技会名(DA_Master) : string.Empty;

            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
            {
                string prgNo   = DSDspDataHelper.Get現在進行番号(DS_Status, 区分番号, ラウンド番号);
                string kbnName = DA_Master != null ? DSDspDataHelper.Get区分名(DA_Master, 区分番号) : "";
                string rndName = DA_Master != null ? DSDspDataHelper.Getラウンド名(DA_Master, 区分番号, ラウンド番号) : "";
                tb2.Text = $"{prgNo}　{kbnName}　{rndName}";
                tb2.Visibility = Visibility.Visible;
            }

            if (PartsCOM002.FindName("LB_右上") is Label lbRight)
            {
                lbRight.Content = $"現在時刻　{DateTime.Now:HH:mm}";
                if (PartsCOM002.FindName("Canvas_右上01") is UIElement cv002)
                    cv002.Visibility = Visibility.Visible;
                lbRight.Visibility = Visibility.Visible;
            }
            StartClock();

            if (PartsCOM003.FindName("LB_右上") is Label lbRight03)
            {
                string 種目テキスト = DA_Master != null
                    ? string.Join("  ", DSDspDataHelper.Get全種目リスト(DA_Master, 区分番号, ラウンド番号).Select(d => d.DncCd))
                    : string.Empty;
                lbRight03.Content = 種目テキスト;
                lbRight03.Visibility = Visibility.Visible;
            }
        }

        /// <summary>STEP2: タイトル・明細をフェードインで表示</summary>
        private void Step2()
        {
            EnsurePartsMainInitialized();
            var p = PartsLST006;

            var next = DSDspDataHelper.Get次進行情報(DS_Status, 区分番号, ラウンド番号);

            SetLabelContent(p, "LB_タイトル1", "次の競技");
            SetOpacity(p, "IM_タイトル1", 0); SetVisible(p, "IM_タイトル1", true);
            SetOpacity(p, "LB_タイトル1", 0); SetVisible(p, "LB_タイトル1", true);

            if (next.HasValue && DA_Master != null)
            {
                string nk = DSDspDataHelper.Get区分名(DA_Master, next.Value.KbnNo);
                string nr = DSDspDataHelper.Getラウンド名(DA_Master, next.Value.KbnNo, next.Value.RndNo);
                string text = $"{next.Value.PrgNo}　{nk}　{nr}";
                SetLabelContent(p, "LB_明細1", text);
                // LB_明細1 幅は LST006 パーツに依存（小画面のため実効幅150程度）
                if (p.FindName("LB_明細1") is Label lb)
                    _partsMain?.フォントサイズ自動調整(lb, text, 145, 11, 6, FontFamilyName);
                SetOpacity(p, "IM_明細1", 0); SetVisible(p, "IM_明細1", true);
                SetOpacity(p, "LB_明細1", 0); SetVisible(p, "LB_明細1", true);
            }
            else
            {
                SetLabelContent(p, "LB_明細1", string.Empty);
                SetVisible(p, "IM_明細1", false);
                SetVisible(p, "LB_明細1", false);
            }

            if (_partsMain == null)
            {
                SetOpacity(p, "IM_タイトル1", 1); SetOpacity(p, "LB_タイトル1", 1);
                if (p.FindName("IM_明細1") is UIElement im && im.Visibility == Visibility.Visible) SetOpacity(p, "IM_明細1", 1);
                if (p.FindName("LB_明細1") is UIElement lb1 && lb1.Visibility == Visibility.Visible) SetOpacity(p, "LB_明細1", 1);
                return;
            }

            var imSb = new Storyboard();
            if (p.FindName("IM_タイトル1") is UIElement imT)
                _partsMain.フェードイン(true, imT, imSb, 0);
            if (p.FindName("IM_明細1") is UIElement imM && imM.Visibility == Visibility.Visible)
                _partsMain.フェードイン(true, imM, imSb, 100);

            imSb.Completed += (s, e) =>
            {
                var lbSb = new Storyboard();
                foreach (var n in new[] { "LB_タイトル1", "LB_明細1" })
                    if (p.FindName(n) is UIElement el && el.Visibility == Visibility.Visible)
                        _partsMain?.フェードイン(true, el, lbSb, 0);
                lbSb.Begin();
            };
            imSb.Begin();
        }

        /// <summary>STEP3: STEP2 で表示したものをフェードアウトして非表示</summary>
        private void Step3()
        {
            EnsurePartsMainInitialized();
            var p = PartsLST006;
            var targets = new[] { "IM_タイトル1", "LB_タイトル1", "IM_明細1", "LB_明細1" };

            if (_partsMain == null)
            {
                foreach (var n in targets) SetVisible(p, n, false);
                return;
            }

            var sb = new Storyboard();
            bool any = false;
            foreach (var n in targets)
                if (p.FindName(n) is UIElement el && el.Visibility == Visibility.Visible && el.Opacity > 0)
                { _partsMain.フェードアウト(true, el, sb, 0); any = true; }

            if (!any) { foreach (var n in targets) SetVisible(p, n, false); return; }

            sb.Completed += (s, e) => { foreach (var n in targets) SetVisible(p, n, false); };
            sb.Begin();
        }

        /// <summary>STEP4: COM001のTB_左上2、COM002、COM003 を非表示 → ScreenCompleted</summary>
        private void Step4()
        {
            if (PartsCOM001.FindName("TB_左上2") is TextBlock tb2)
                tb2.Visibility = Visibility.Collapsed;
            if (PartsCOM002.FindName("Canvas_右上01") is UIElement cv002)
                cv002.Visibility = Visibility.Collapsed;
            if (PartsCOM003.FindName("LB_右上") is Label lb003)
                lb003.Visibility = Visibility.Collapsed;
            RaiseScreenCompleted();
        }

        #endregion

        #region 公開メソッド
        public void RequestClose() => _closeRequested = true;
        #endregion

        #region ヘルパー

        private void HideAllParts()
        {
            var p = PartsLST006;
            SetLabelContent(p, "LB_明細1", string.Empty);
            foreach (var name in new[] { "IM_タイトル1", "LB_タイトル1", "IM_明細1", "LB_明細1" })
                SetVisible(p, name, false);
        }

        private static void SetVisible(FrameworkElement p, string name, bool visible)
        {
            if (p.FindName(name) is UIElement el)
                el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void SetOpacity(FrameworkElement p, string name, double opacity)
        {
            if (p.FindName(name) is UIElement el) el.Opacity = opacity;
        }

        private static void SetLabelContent(FrameworkElement p, string name, string text)
        {
            if (p.FindName(name) is Label lb) lb.Content = text;
        }

        #endregion
    }
}

// Made with Bob
