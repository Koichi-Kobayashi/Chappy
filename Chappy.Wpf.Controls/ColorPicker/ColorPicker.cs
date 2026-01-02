using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Chappy.Wpf.Controls.Uitl;

namespace Chappy.Wpf.Controls.ColorPicker;

[TemplatePart(Name = PartSpectrumImage, Type = typeof(Image))]
[TemplatePart(Name = PartSpectrumCanvas, Type = typeof(Canvas))]
[TemplatePart(Name = PartSpectrumThumb, Type = typeof(Thumb))]
[TemplatePart(Name = PartHueSlider, Type = typeof(Slider))]
[TemplatePart(Name = PartAlphaSlider, Type = typeof(Slider))]
[TemplatePart(Name = PartPaletteItems, Type = typeof(ItemsControl))]
public partial class ColorPicker : Control
{
    private const string PartSpectrumImage = "PART_SpectrumImage";
    private const string PartSpectrumCanvas = "PART_SpectrumCanvas";
    private const string PartSpectrumThumb = "PART_SpectrumThumb";
    private const string PartHueSlider = "PART_HueSlider";
    private const string PartAlphaSlider = "PART_AlphaSlider";
    private const string PartPaletteItems = "PART_PaletteItems";

    private bool _syncing;
    private ItemsControl? _paletteItems;

    static ColorPicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker),
            new FrameworkPropertyMetadata(typeof(ColorPicker)));
    }

    public ColorPicker()
    {
        // 32 colors (8 x 4) palette with JA/EN/Tailwind name
        PaletteColors = new ObservableCollection<PaletteColor>(new[]
        {
            // warm (row 1)
            new PaletteColor("アンバー",          "Amber",         "amber-500",   (Color)ColorConverter.ConvertFromString("#FFF59E0B")),
            new PaletteColor("オレンジ",          "Orange",        "orange-500",  (Color)ColorConverter.ConvertFromString("#FFF97316")),
            new PaletteColor("ダークオレンジ",    "Dark Orange",   "orange-600",  (Color)ColorConverter.ConvertFromString("#FFEA580C")),
            new PaletteColor("レッド（濃）",      "Red (Dark)",    "red-600",     (Color)ColorConverter.ConvertFromString("#FFDC2626")),
            new PaletteColor("レッド",            "Red",           "red-500",     (Color)ColorConverter.ConvertFromString("#FFEF4444")),
            new PaletteColor("ローズ",            "Rose",          "rose-500",    (Color)ColorConverter.ConvertFromString("#FFF43F5E")),
            new PaletteColor("ピンク",            "Pink",          "pink-500",    (Color)ColorConverter.ConvertFromString("#FFEC4899")),
            new PaletteColor("ダークピンク",      "Dark Pink",     "pink-600",    (Color)ColorConverter.ConvertFromString("#FFDB2777")),

            // purple/indigo + sky/cyan (row 2)
            new PaletteColor("パープル",          "Purple",        "purple-500",  (Color)ColorConverter.ConvertFromString("#FFA855F7")),
            new PaletteColor("バイオレット（濃）","Violet (Dark)", "violet-600",  (Color)ColorConverter.ConvertFromString("#FF7C3AED")),
            new PaletteColor("インディゴ",        "Indigo",        "indigo-500",  (Color)ColorConverter.ConvertFromString("#FF6366F1")),
            new PaletteColor("ダークインディゴ",  "Indigo (Dark)", "indigo-600",  (Color)ColorConverter.ConvertFromString("#FF4F46E5")),
            new PaletteColor("スカイブルー",      "Sky Blue",      "sky-500",     (Color)ColorConverter.ConvertFromString("#FF0EA5E9")),
            new PaletteColor("スカイブルー（濃）","Sky Blue (Dark)","sky-600",    (Color)ColorConverter.ConvertFromString("#FF0284C7")),
            new PaletteColor("シアン",            "Cyan",          "cyan-500",    (Color)ColorConverter.ConvertFromString("#FF06B6D4")),
            new PaletteColor("シアン（濃）",      "Cyan (Dark)",   "cyan-600",    (Color)ColorConverter.ConvertFromString("#FF0891B2")),

            // greens/teals (row 3)
            new PaletteColor("グリーン",          "Green",         "green-500",   (Color)ColorConverter.ConvertFromString("#FF22C55E")),
            new PaletteColor("グリーン（濃）",    "Green (Dark)",  "green-600",   (Color)ColorConverter.ConvertFromString("#FF16A34A")),
            new PaletteColor("エメラルド",        "Emerald",       "emerald-500", (Color)ColorConverter.ConvertFromString("#FF10B981")),
            new PaletteColor("エメラルド（濃）",  "Emerald (Dark)","emerald-600", (Color)ColorConverter.ConvertFromString("#FF059669")),
            new PaletteColor("ライム",            "Lime",          "lime-500",    (Color)ColorConverter.ConvertFromString("#FF84CC16")),
            new PaletteColor("ライム（濃）",      "Lime (Dark)",   "lime-600",    (Color)ColorConverter.ConvertFromString("#FF65A30D")),
            new PaletteColor("ティール",          "Teal",          "teal-500",    (Color)ColorConverter.ConvertFromString("#FF14B8A6")),
            new PaletteColor("ティール（濃）",    "Teal (Dark)",   "teal-700",    (Color)ColorConverter.ConvertFromString("#FF0F766E")),

            // grays (row 4)
            new PaletteColor("ライトグレー",      "Light Gray",    "gray-300",    (Color)ColorConverter.ConvertFromString("#FFD1D5DB")),
            new PaletteColor("グレー（明）",      "Gray (Light)",  "gray-400",    (Color)ColorConverter.ConvertFromString("#FF9CA3AF")),
            new PaletteColor("グレー",            "Gray",          "gray-500",    (Color)ColorConverter.ConvertFromString("#FF6B7280")),
            new PaletteColor("ダークグレー",      "Dark Gray",     "gray-600",    (Color)ColorConverter.ConvertFromString("#FF4B5563")),
            new PaletteColor("スレート",          "Slate",         "gray-700",    (Color)ColorConverter.ConvertFromString("#FF374151")),
            new PaletteColor("ダークスレート",    "Dark Slate",    "gray-800",    (Color)ColorConverter.ConvertFromString("#FF1F2937")),
            new PaletteColor("ほぼ黒",            "Near Black",    "gray-900",    (Color)ColorConverter.ConvertFromString("#FF111827")),
            new PaletteColor("白",                "White",         "white",       (Color)ColorConverter.ConvertFromString("#FFFFFFFF")),
        });
    }

    private Image? _spectrumImage;
    private Canvas? _spectrumCanvas;
    private Thumb? _spectrumThumb;
    private Slider? _hueSlider;
    private Slider? _alphaSlider;

    private WriteableBitmap? _spectrumBitmap;
    private int _bmpW;
    private int _bmpH;

    private bool _isTemplateUpdating;

    #region Dependency Properties

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                Colors.Transparent,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public static readonly DependencyProperty HueProperty =
        DependencyProperty.Register(
            nameof(Hue),
            typeof(double),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(0d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHSVChanged, CoerceHue));

    public double Hue
    {
        get => (double)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public static readonly DependencyProperty SaturationProperty =
        DependencyProperty.Register(
            nameof(Saturation),
            typeof(double),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(1d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHSVChanged, CoerceUnit));

    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(1d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHSVChanged, CoerceUnit));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty AlphaProperty =
        DependencyProperty.Register(
            nameof(Alpha),
            typeof(double),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(1d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnAlphaChanged, CoerceUnit));

    public double Alpha
    {
        get => (double)GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    public static readonly DependencyProperty PaletteColorsProperty =
        DependencyProperty.Register(
            nameof(PaletteColors),
            typeof(ObservableCollection<PaletteColor>),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(null));

    public ObservableCollection<PaletteColor> PaletteColors
    {
        get => (ObservableCollection<PaletteColor>)GetValue(PaletteColorsProperty);
        set => SetValue(PaletteColorsProperty, value);
    }

    public static readonly DependencyProperty ToolTipLanguageProperty =
        DependencyProperty.Register(
            nameof(ToolTipLanguage),
            typeof(string),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata("ja"));

    /// <summary>
    /// "ja" または "en"（それ以外は ja 扱い）
    /// </summary>
    public string ToolTipLanguage
    {
        get => (string)GetValue(ToolTipLanguageProperty);
        set => SetValue(ToolTipLanguageProperty, value);
    }

    private static object CoerceHue(DependencyObject d, object baseValue)
    {
        var v = (double)baseValue;
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0d;
        v %= 360d;
        if (v < 0) v += 360d;
        return v;
    }

    private static object CoerceUnit(DependencyObject d, object baseValue)
    {
        var v = (double)baseValue;
        if (double.IsNaN(v) || double.IsInfinity(v)) return 0d;
        if (v < 0) return 0d;
        if (v > 1) return 1d;
        return v;
    }

    #endregion

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        Detach();

        _spectrumImage = GetTemplateChild(PartSpectrumImage) as Image;
        _spectrumCanvas = GetTemplateChild(PartSpectrumCanvas) as Canvas;
        _spectrumThumb = GetTemplateChild(PartSpectrumThumb) as Thumb;
        _hueSlider = GetTemplateChild(PartHueSlider) as Slider;
        _alphaSlider = GetTemplateChild(PartAlphaSlider) as Slider;
        _paletteItems = GetTemplateChild(PartPaletteItems) as ItemsControl;

        // パレットクリック（バブリング）をこのコントロールで拾う
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnAnyButtonClick));

        // RGB/Drag hook（RGB.cs のメソッド）
        HookAllSlidersForDrag();

        Attach();

        // 初期同期
        SyncFromSelectedColor(SelectedColor);
        RebuildSpectrumBitmapIfNeeded();
        RenderSpectrum();
        UpdateThumbPosition();

        Dispatcher.BeginInvoke(new Action(() =>
        {
            RebuildSpectrumBitmapIfNeeded();
            RenderSpectrum();
            UpdateThumbPosition();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Attach()
    {
        if (_spectrumCanvas != null)
        {
            _spectrumCanvas.SizeChanged += OnSpectrumSizeChanged;
            _spectrumCanvas.MouseLeftButtonDown += OnSpectrumMouseDown;
            _spectrumCanvas.MouseMove += OnSpectrumMouseMove;
            _spectrumCanvas.MouseLeftButtonUp += OnSpectrumMouseUp;
        }

        if (_hueSlider != null)
            _hueSlider.ValueChanged += OnHueSliderChanged;

        if (_alphaSlider != null)
            _alphaSlider.ValueChanged += OnAlphaSliderChanged;

        if (_spectrumThumb != null)
        {
            // Thumb のドラッグ操作も可能に（Canvas上をドラッグしてもOK）
            _spectrumThumb.DragDelta += OnThumbDragDelta;
        }
    }

    private void Detach()
    {
        if (_spectrumCanvas != null)
        {
            _spectrumCanvas.SizeChanged -= OnSpectrumSizeChanged;
            _spectrumCanvas.MouseLeftButtonDown -= OnSpectrumMouseDown;
            _spectrumCanvas.MouseMove -= OnSpectrumMouseMove;
            _spectrumCanvas.MouseLeftButtonUp -= OnSpectrumMouseUp;
        }

        if (_hueSlider != null)
            _hueSlider.ValueChanged -= OnHueSliderChanged;

        if (_alphaSlider != null)
            _alphaSlider.ValueChanged -= OnAlphaSliderChanged;

        if (_spectrumThumb != null)
            _spectrumThumb.DragDelta -= OnThumbDragDelta;
    }

    private void OnAnyButtonClick(object sender, RoutedEventArgs e)
    {
        // パレットのタイルボタンだけ処理（TagにColorを入れる）
        if (e.OriginalSource is Button btn && btn.Name == "PART_PaletteSwatchButton")
        {
            if (btn.Tag is PaletteColor pc)
            {
                // 現在のAlphaを維持して RGB だけ変える
                var a = (byte)Math.Round(MathUtil.Clamp(Alpha, 0, 1) * 255);
                SelectedColor = Color.FromArgb(a, pc.Color.R, pc.Color.G, pc.Color.B);

                // HSVとThumbも同期
                SyncFromSelectedColor(SelectedColor);
                RenderSpectrum();
                UpdateThumbPosition();
            }

        }
    }

    #region Property change callbacks

    private static void OnHSVChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var cp = (ColorPicker)d;
        if (cp._isTemplateUpdating) return;

        cp.UpdateSelectedColorFromHSV();
        // Hueが変わったらスペクトラムも変わる
        if (e.Property == HueProperty)
        {
            cp.RebuildSpectrumBitmapIfNeeded();
            cp.RenderSpectrum();
        }
        cp.UpdateThumbPosition();
    }

    private static void OnAlphaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (ColorPicker)d;
        if (c._syncing) return;

        c._syncing = true;
        try
        {
            var a = (byte)Math.Round(c.Alpha * 255.0); // Alphaが0..1なら
            var sc = c.SelectedColor;
            c.SelectedColor = Color.FromArgb(a, sc.R, sc.G, sc.B);
        }
        finally
        {
            c._syncing = false;
        }
    }

    #endregion

    #region Input / interactions (Spectrum)

    private bool _capturing;

    private void OnSpectrumMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_spectrumCanvas == null) return;
        _capturing = true;
        _spectrumCanvas.CaptureMouse();
        SetSVFromPoint(e.GetPosition(_spectrumCanvas));
    }

    private void OnSpectrumMouseMove(object sender, MouseEventArgs e)
    {
        if (!_capturing || _spectrumCanvas == null) return;
        SetSVFromPoint(e.GetPosition(_spectrumCanvas));
    }

    private void OnSpectrumMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_spectrumCanvas == null) return;
        _capturing = false;
        _spectrumCanvas.ReleaseMouseCapture();
    }

    private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_spectrumCanvas == null) return;

        // 現在のThumb位置にdeltaを加え、Canvas座標へ
        var left = Canvas.GetLeft(_spectrumThumb);
        var top = Canvas.GetTop(_spectrumThumb);

        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;

        // Thumbの中心で計算
        var x = left + (_spectrumThumb?.ActualWidth ?? 0) / 2 + e.HorizontalChange;
        var y = top + (_spectrumThumb?.ActualHeight ?? 0) / 2 + e.VerticalChange;

        SetSVFromPoint(new Point(x, y));
    }

    private void SetSVFromPoint(Point p)
    {
        if (_spectrumCanvas == null) return;

        var w = Math.Max(1, _spectrumCanvas.ActualWidth);
        var h = Math.Max(1, _spectrumCanvas.ActualHeight);

        var tw = _spectrumThumb?.ActualWidth ?? 0;
        var th = _spectrumThumb?.ActualHeight ?? 0;

        var x = MathUtil.Clamp(p.X, tw / 2, w - tw / 2);
        var y = MathUtil.Clamp(p.Y, th / 2, h - th / 2);

        var s = x / w;
        var v = 1.0 - (y / h);

        _isTemplateUpdating = true;
        try
        {
            Saturation = s;
            Value = v;
        }
        finally
        {
            _isTemplateUpdating = false;
        }

        UpdateSelectedColorFromHSV();
        UpdateThumbPosition();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (ColorPicker)d;
        if (e.NewValue is not Color color) return;
        if (c._syncing) return;

        c._syncing = true;
        try
        {
            var dragging = c._draggingPartName;

            // --- 常に: RGB 表示は（ドラッグ中でも）追従させてOK ---
            // ただし、RGB 自体をドラッグ中ならその成分は書き戻さない
            if (dragging != "PART_RSlider") c.SetCurrentValue(RProperty, color.R);
            if (dragging != "PART_GSlider") c.SetCurrentValue(GProperty, color.G);
            if (dragging != "PART_BSlider") c.SetCurrentValue(BProperty, color.B);

            // --- Alpha ドラッグ中: ここで終了（戻り防止） ---
            // ※Alpha 以外の重い同期（HSV再計算/描画/つまみ位置更新）を一切しない
            if (dragging == "PART_AlphaSlider")
            {
                // Alpha表示用のDPがあるなら、"書き戻し"はしない（必要なら他表示だけ）
                // 例: AlphaText のみ更新、など。
                return;
            }

            // --- Hue ドラッグ中: Hue 自体は書き戻さない（あなたの Hue DP があるなら） ---
            if (dragging == "PART_HueSlider")
            {
                // Hue/SV再計算はドラッグ側が主導なので、ここでは余計な再同期をしない
                // 必要ならRGBやテキスト等だけ更新して return でもOK
                // いったん戻り防止優先で return（安定）
                return;
            }

            // --- それ以外（パレットクリック、SV面クリック、外部からSelectedColor変更など） ---
            // ここでまとめて内部状態を同期＆描画更新
            c.SyncFromSelectedColor(color);
            c.RenderSpectrum();
            c.UpdateThumbPosition();
        }
        finally
        {
            c._syncing = false;
        }
    }

    #endregion

    #region Sliders

    private void OnHueSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Slider(0..360)
        Hue = e.NewValue;
    }

    private void OnAlphaSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Slider(0..1)
        Alpha = e.NewValue;
    }

    #endregion

    #region Spectrum rendering

    private void OnSpectrumSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildSpectrumBitmapIfNeeded();
        RenderSpectrum();
        UpdateThumbPosition();
    }

    private void RebuildSpectrumBitmapIfNeeded()
    {
        if (_spectrumImage == null || _spectrumCanvas == null) return;

        // Canvasの表示サイズを基準にする（Imageは0になりやすい）
        var w = (int)Math.Max(1, Math.Round(_spectrumCanvas.ActualWidth));
        var h = (int)Math.Max(1, Math.Round(_spectrumCanvas.ActualHeight));

        if (w == _bmpW && h == _bmpH && _spectrumBitmap != null) return;

        _bmpW = w;
        _bmpH = h;

        _spectrumBitmap = new WriteableBitmap(_bmpW, _bmpH, 96, 96, PixelFormats.Bgra32, null);
        _spectrumImage.Source = _spectrumBitmap;
    }


    private void RenderSpectrum()
    {
        if (_spectrumBitmap == null) return;

        // Hue固定で S(横) / V(縦) の2D面を生成（不透明RGB）
        var w = _bmpW;
        var h = _bmpH;
        if (w <= 0 || h <= 0) return;

        var stride = w * 4;
        var pixels = new byte[h * stride];

        var hue = Hue;

        for (int y = 0; y < h; y++)
        {
            double v = 1.0 - (double)y / (h - 1 == 0 ? 1 : (h - 1));
            for (int x = 0; x < w; x++)
            {
                double s = (double)x / (w - 1 == 0 ? 1 : (w - 1));

                var rgb = HsvColor.FromHsv(hue, s, v).ToColor(alpha: 1.0);

                int i = (y * stride) + (x * 4);
                pixels[i + 0] = rgb.B;
                pixels[i + 1] = rgb.G;
                pixels[i + 2] = rgb.R;
                pixels[i + 3] = 255; // opaque spectrum; alpha is controlled separately
            }
        }

        _spectrumBitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
    }

    private void UpdateThumbPosition()
    {
        if (_spectrumCanvas == null || _spectrumThumb == null) return;

        var w = Math.Max(1, _spectrumCanvas.ActualWidth);
        var h = Math.Max(1, _spectrumCanvas.ActualHeight);

        var tw = Math.Max(1, _spectrumThumb.ActualWidth);
        var th = Math.Max(1, _spectrumThumb.ActualHeight);

        // Thumb中心が必ずCanvas内に収まるようにクランプ
        var x = Saturation * w;
        var y = (1.0 - Value) * h;

        x = MathUtil.Clamp(x, tw / 2, w - tw / 2);
        y = MathUtil.Clamp(y, th / 2, h - th / 2);

        Canvas.SetLeft(_spectrumThumb, x - tw / 2);
        Canvas.SetTop(_spectrumThumb, y - th / 2);
    }


    #endregion

    #region Sync helpers

    private void SyncFromSelectedColor(Color c)
    {
        var hsv = HsvColor.FromColor(c);

        _isTemplateUpdating = true;
        try
        {
            Hue = hsv.H;
            Saturation = hsv.S;
            Value = hsv.V;
            Alpha = c.A / 255.0;
        }
        finally
        {
            _isTemplateUpdating = false;
        }
    }

    private void UpdateSelectedColorFromHSV()
    {
        var c = HsvColor.FromHsv(Hue, Saturation, Value).ToColor(Alpha);

        _isTemplateUpdating = true;
        try
        {
            SelectedColor = c;
        }
        finally
        {
            _isTemplateUpdating = false;
        }
    }
    #endregion
}
