using Avalonia.Markup.Xaml;
using UVtools.Core.Operations;
using UVtools.WPF.Extensions;

namespace UVtools.WPF.Controls.Tools;

public class ToolDynamicLiftsControl : ToolControl
{
    public OperationDynamicLifts Operation => BaseOperation as OperationDynamicLifts;
    public ToolDynamicLiftsControl()
    {
        BaseOperation = new OperationDynamicLifts(SlicerFile);
        if (!ValidateSpawn()) return;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void ViewSmallestLayer(bool isBottom)
    {
        var layerFound = Operation.GetSmallestLayer(isBottom);
        if (layerFound is null) return;
        App.MainWindow.ActualLayer = layerFound.Index;
    }

    public void ViewLargestLayer(bool isBottom)
    {
        var layerFound = Operation.GetLargestLayer(isBottom);
        if (layerFound is null) return;
        App.MainWindow.ActualLayer = layerFound.Index;
    }
}