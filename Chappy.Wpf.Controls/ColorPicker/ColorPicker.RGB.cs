// Controls/ColorPicker.Rgb.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker;

public partial class ColorPicker
{
    // --- Slider drag state (used by ColorPicker.cs OnSelectedColorChanged to avoid "jump-back") ---
    /// <summary>スライダーのドラッグ中かどうかを示すフラグ</summary>
    private bool _isDraggingSlider;
    /// <summary>現在ドラッグ中のスライダーのパート名</summary>
    private string? _draggingPartName;

    // --- RGB DependencyProperties ---
    /// <summary>
    /// 赤成分（0-255）を表す依存プロパティ
    /// </summary>
    public static readonly DependencyProperty RProperty =
        DependencyProperty.Register(
            nameof(R),
            typeof(byte),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                (byte)0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRgbChanged));

    /// <summary>
    /// 緑成分（0-255）を表す依存プロパティ
    /// </summary>
    public static readonly DependencyProperty GProperty =
        DependencyProperty.Register(
            nameof(G),
            typeof(byte),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                (byte)0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRgbChanged));

    /// <summary>
    /// 青成分（0-255）を表す依存プロパティ
    /// </summary>
    public static readonly DependencyProperty BProperty =
        DependencyProperty.Register(
            nameof(B),
            typeof(byte),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                (byte)0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRgbChanged));

    /// <summary>
    /// 赤成分（0-255）を取得または設定する
    /// </summary>
    public byte R
    {
        get => (byte)GetValue(RProperty);
        set => SetValue(RProperty, value);
    }

    /// <summary>
    /// 緑成分（0-255）を取得または設定する
    /// </summary>
    public byte G
    {
        get => (byte)GetValue(GProperty);
        set => SetValue(GProperty, value);
    }

    /// <summary>
    /// 青成分（0-255）を取得または設定する
    /// </summary>
    public byte B
    {
        get => (byte)GetValue(BProperty);
        set => SetValue(BProperty, value);
    }

    /// <summary>
    /// R/G/Bプロパティが変更された時に呼ばれるコールバック
    /// UI（スライダー/テキストボックス）からの変更時にSelectedColorを更新する
    /// 再帰ループを防ぐための処理を含む
    /// </summary>
    /// <param name="d">変更された依存オブジェクト</param>
    /// <param name="e">変更イベントの引数</param>
    private static void OnRgbChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (ColorPicker)d;
        if (c._syncing) return;

        // If the new value is same as SelectedColor component, skip (minor stability)
        var sc = c.SelectedColor;
        var r = c.R;
        var g = c.G;
        var b = c.B;

        if (sc.R == r && sc.G == g && sc.B == b)
            return;

        c._syncing = true;
        try
        {
            // Keep alpha from SelectedColor (or if you have Alpha DP, use it here)
            var a = sc.A;
            c.SelectedColor = Color.FromArgb(a, r, g, b);
        }
        finally { c._syncing = false; }
    }

    // ----------------------------
    // Hooks (called from ColorPicker.cs OnApplyTemplate)
    // ----------------------------

    /// <summary>
    /// すべてのスライダーにドラッグイベントをフックする
    /// ColorPicker.csのOnApplyTemplate()から呼び出す
    /// </summary>
    private void HookAllSlidersForDrag()
    {
        HookSliderDrag("PART_HueSlider");
        HookSliderDrag("PART_AlphaSlider");

        HookSliderDrag("PART_RSlider");
        HookSliderDrag("PART_GSlider");
        HookSliderDrag("PART_BSlider");
    }

    /// <summary>
    /// 指定されたパート名のスライダーにドラッグイベントをフックする
    /// </summary>
    /// <param name="partName">スライダーのパート名</param>
    private void HookSliderDrag(string partName)
    {
        if (GetTemplateChild(partName) is Slider s)
        {
            // DragStarted
            s.AddHandler(Thumb.DragStartedEvent,
                new DragStartedEventHandler((_, __) =>
                {
                    _isDraggingSlider = true;
                    _draggingPartName = partName;
                }));

            // DragCompleted
            s.AddHandler(Thumb.DragCompletedEvent,
                new DragCompletedEventHandler((_, __) =>
                {
                    _isDraggingSlider = false;
                    _draggingPartName = null;

                    // NOTE: Heavy redraw should be done from ColorPicker.cs if needed.
                    // e.g. RenderSpectrum(), UpdateThumbPosition()
                }));
        }
    }

    // ----------------------------
    // Sync helpers (called from ColorPicker.cs OnSelectedColorChanged)
    // ----------------------------

    /// <summary>
    /// SelectedColorからRGB依存プロパティの値を更新する
    /// スライダー/テキストボックスが追従するようにする
    /// ColorPicker.csのOnSelectedColorChangedから呼び出す
    /// </summary>
    /// <param name="color">選択された色</param>
    private void SyncRgbFromSelectedColor(Color color)
    {
        // Use SetCurrentValue so existing bindings are preserved
        SetCurrentValue(RProperty, color.R);
        SetCurrentValue(GProperty, color.G);
        SetCurrentValue(BProperty, color.B);
    }

    /// <summary>
    /// いずれかのスライダーがドラッグ中かどうかを取得する
    /// ColorPicker.csから使用する（同じクラスなのでprivateフィールドを読み取れる）
    /// </summary>
    private bool IsDraggingAnySlider => _isDraggingSlider;

    /// <summary>
    /// 現在ドラッグ中のスライダーのパート名を取得する
    /// デバッグ時にどのパートがドラッグ中かを確認するのに便利
    /// </summary>
    private string? DraggingPartName => _draggingPartName;
}
