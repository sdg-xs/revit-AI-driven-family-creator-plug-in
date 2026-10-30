using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace FamilyCreator.Revit;

[Transaction(TransactionMode.Manual)]
public sealed class OpenCreatorCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            App.ShowWindow(commandData.Application);
            return Result.Succeeded;
        }
        catch (Exception exception)
        {
            message = "Family Creator could not open: " + exception.Message;
            return Result.Failed;
        }
    }
}

public sealed class CreatorAvailability : IExternalCommandAvailability
{
    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) => true;
}
