using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

/// <summary>
/// The ship view wired as OverlayWindow.xaml wires it (T8.5): a settings session fills the theme resources and the view
/// model, the view takes them by binding and resource reference; rendered to pixels. STA only.
/// </summary>
internal static class ViewPictures
{
    /// <summary>A view of <paramref name="viewModel"/> under <paramref name="settings"/>, laid out at <paramref name="size"/>.</summary>
    public static (ShipViewControl View, FrameworkElement Root) Wire(OverlayViewModel viewModel, AppSettings settings, Size size)
    {
        Border root = new();

        // The constructor applies everything and writes nothing (D31), so the path is never touched.
        _ = new SettingsSession(root.Resources, viewModel, Path.Combine(Path.GetTempPath(), "cargoplan-view-pictures.json"), settings);
        root.SetResourceReference(Border.BackgroundProperty, "WindowBackgroundBrush");

        ShipViewControl view = new() { DataContext = viewModel };
        Bind(view, ShipViewControl.SceneProperty, nameof(OverlayViewModel.Scene));
        Bind(view, ShipViewControl.SelectionProperty, nameof(OverlayViewModel.Selection), BindingMode.TwoWay);
        Bind(view, ShipViewControl.MaxPxPerCellProperty, nameof(OverlayViewModel.MaxZoomPxPerCell));
        Bind(view, ShipViewControl.StopPaletteProperty, nameof(OverlayViewModel.StopPalette));
        Bind(view, ShipViewControl.CameraProperty, nameof(OverlayViewModel.Camera), BindingMode.TwoWay);
        Bind(view, ShipViewControl.HoloProperty, nameof(OverlayViewModel.Holo));
        Bind(view, ShipViewControl.HoloOpacityProperty, nameof(OverlayViewModel.HoloOpacity));
        Bind(view, ShipViewControl.HoloHullColorProperty, nameof(OverlayViewModel.HoloHullColor));
        Bind(view, ShipViewControl.HoloEdgeColorProperty, nameof(OverlayViewModel.HoloEdgeColor));
        view.SetResourceReference(ShipViewControl.CrateFillAlphaProperty, "CratesFillAlpha");
        view.SetResourceReference(ShipViewControl.CrateEdgeBrushProperty, "CratesEdgeBrush");
        view.SetResourceReference(ShipViewControl.RackNumberBrushProperty, "CratesRackNumberBrush");
        view.SetResourceReference(ShipViewControl.RackNumberFontSizeProperty, "CratesRackNumberFontSize");
        view.SetResourceReference(ShipViewControl.RackNumberHaloBrushProperty, "CratesHaloBrush");
        view.SetResourceReference(ShipViewControl.RackNumberHaloSizeProperty, "CratesHaloSize");
        view.SetResourceReference(ShipViewControl.SelectionPenProperty, "CratesSelectionPen");
        view.SetResourceReference(ShipViewControl.SelectionOutlinePenProperty, "CratesSelectionOutlinePen");

        // App.xaml's brush, which OnRender looks up; there is no App in the tests.
        view.Resources["HitTestBackgroundBrush"] = Brushes.Transparent;
        root.Child = view;
        Layout(root, size);
        return (view, root);
    }

    public static void Layout(FrameworkElement root, Size size)
    {
        root.Measure(size);
        root.Arrange(new Rect(size));
        root.UpdateLayout();
    }

    /// <summary>The picture as premultiplied BGRA bytes.</summary>
    public static byte[] Pixels(FrameworkElement root)
    {
        var bitmap = Render(root);
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }

    /// <summary>The share of pixels that differ, 0 to 1.</summary>
    public static double Difference(byte[] a, byte[] b)
    {
        var differing = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3])
            {
                differing++;
            }
        }

        return differing / (a.Length / 4.0);
    }

    public static void SavePng(FrameworkElement root, string path)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(Render(root)));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static RenderTargetBitmap Render(FrameworkElement root)
    {
        RenderTargetBitmap bitmap = new((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        return bitmap;
    }

    private static void Bind(ShipViewControl view, DependencyProperty property, string path, BindingMode mode = BindingMode.OneWay) =>
        view.SetBinding(property, new Binding(path) { Mode = mode });
}
