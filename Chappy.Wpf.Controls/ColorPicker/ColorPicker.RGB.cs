// Controls/ColorPicker.Rgb.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Chappy.Wpf.Controls.ColorPicker
{
    public partial class ColorPicker
    {
        // --- Slider drag state (used by ColorPicker.cs OnSelectedColorChanged to avoid "jump-back") ---
        private bool _isDraggingSlider;
        private string? _draggingPartName;

        // --- RGB DependencyProperties ---
        public static readonly DependencyProperty RProperty =
            DependencyProperty.Register(
                nameof(R),
                typeof(byte),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(
                    (byte)0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnRgbChanged));

        public static readonly DependencyProperty GProperty =
            DependencyProperty.Register(
                nameof(G),
                typeof(byte),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(
                    (byte)0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnRgbChanged));

        public static readonly DependencyProperty BProperty =
            DependencyProperty.Register(
                nameof(B),
                typeof(byte),
                typeof(ColorPicker),
                new FrameworkPropertyMetadata(
                    (byte)0,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnRgbChanged));

        public byte R
        {
            get => (byte)GetValue(RProperty);
            set => SetValue(RProperty, value);
        }

        public byte G
        {
            get => (byte)GetValue(GProperty);
            set => SetValue(GProperty, value);
        }

        public byte B
        {
            get => (byte)GetValue(BProperty);
            set => SetValue(BProperty, value);
        }

        /// <summary>
        /// Called when R/G/B changes from UI (Slider/TextBox).
        /// Updates SelectedColor while preventing re-entrant loop.
        /// </summary>
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
        /// Call this from ColorPicker.cs OnApplyTemplate() to hook slider drag events.
        /// </summary>
        private void HookAllSlidersForDrag()
        {
            HookSliderDrag("PART_HueSlider");
            HookSliderDrag("PART_AlphaSlider");

            HookSliderDrag("PART_RSlider");
            HookSliderDrag("PART_GSlider");
            HookSliderDrag("PART_BSlider");
        }

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
        /// Updates RGB DP values from SelectedColor (so Slider/TextBox follow).
        /// Call this from ColorPicker.cs OnSelectedColorChanged.
        /// </summary>
        private void SyncRgbFromSelectedColor(Color color)
        {
            // Use SetCurrentValue so existing bindings are preserved
            SetCurrentValue(RProperty, color.R);
            SetCurrentValue(GProperty, color.G);
            SetCurrentValue(BProperty, color.B);
        }

        /// <summary>
        /// Expose dragging state for ColorPicker.cs (same class, so it can read private fields)
        /// </summary>
        private bool IsDraggingAnySlider => _isDraggingSlider;

        /// <summary>
        /// Optional: useful for debugging which PART is dragging.
        /// </summary>
        private string? DraggingPartName => _draggingPartName;
    }
}