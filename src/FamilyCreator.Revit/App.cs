using Autodesk.Revit.UI;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace FamilyCreator.Revit;

public sealed class App : IExternalApplication
{
    private static CreatorWindow? window;

    public Result OnStartup(UIControlledApplication application)
    {
        var panel = application.CreateRibbonPanel("Family Creator");
        var button = new PushButtonData("FamilyCreator.Open", "Family\nCreator",
            typeof(App).Assembly.Location, typeof(OpenCreatorCommand).FullName!);
        button.ToolTip = "Open Family Creator";
        button.LongDescription = "Open the Family Creator workspace.";
        button.AvailabilityClassName = typeof(CreatorAvailability).FullName!;
        button.LargeImage = CreateIcon();
        panel.AddItem(button);
        return Result.Succeeded;
    }

    internal static void ShowWindow(UIApplication application)
    {
        if (window is null)
        {
            window = new CreatorWindow();
            new WindowInteropHelper(window).Owner = application.MainWindowHandle;
            window.Closed += (_, _) => window = null;
            window.Show();
        }
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        window?.Close();
        return Result.Succeeded;
    }

    private static DrawingImage CreateIcon()
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            var stroke = new Pen(new SolidColorBrush(Color.FromRgb(30, 110, 154)), 2);
            context.DrawGeometry(new SolidColorBrush(Color.FromRgb(223, 241, 248)), stroke,
                Geometry.Parse("M 16,2 L 29,9 L 29,23 L 16,30 L 3,23 L 3,9 Z"));
            context.DrawGeometry(null, stroke,
                Geometry.Parse("M 3,9 L 16,16 L 29,9 M 16,16 L 16,30 M 10,5 L 23,12"));
        }
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }
}
