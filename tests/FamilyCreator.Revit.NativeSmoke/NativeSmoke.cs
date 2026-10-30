using Profile = FamilyCreator.Generation.Profile;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using FamilyCreator.Generation;
using FamilyCreator.Revit;
using System.Text.Json;

namespace FamilyCreator.NativeSmoke;

public sealed class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FAMILYCREATOR_NATIVE_TEST_REPORT"))) application.Idling += Run;
        return Result.Succeeded;
    }
    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;

    private void Run(object? sender, IdlingEventArgs e)
    {
        var app = (UIApplication)sender!;
        app.Idling -= Run;
        var report = Environment.GetEnvironmentVariable("FAMILYCREATOR_NATIVE_TEST_REPORT")!;
        var results = new List<string>();
        Document? project = null;
        string? temporaryTemplate = null;
        try
        {
            project = app.Application.NewProjectDocument(UnitSystem.Metric);
            var profiles = new[]
            {
                new Profile("circle", [0,0,0], [0,0,1], 20, []),
                new Profile("circle", [0,0,100], [0,0,1], 20, [])
            };
            var body = new SolidPart("Cylinder", "#226688", profiles);
            using (var solid = NativeGeometry.Build(body))
            {
                var actualMm3 = solid.Volume * Math.Pow(304.8, 3);
                var expectedMm3 = Math.PI * 400 * 100;
                if (Math.Abs(actualMm3 - expectedMm3) / expectedMm3 > 0.001) throw new Exception("Cylinder volume differs from the expected native volume.");
                if (NativeGeometry.Tessellate(solid, body.Color).Vertices.Length == 0) throw new Exception("Native preview is empty.");
                results.Add("Native cylinder volume and tessellation passed.");
            }
            var bendProfiles = Enumerable.Range(0, 7).Select(i =>
            {
                var angle = i * Math.PI / 18;
                return new Profile("circle", [150 * (1 - Math.Cos(angle)), 0, 150 * Math.Sin(angle)], [Math.Sin(angle), 0, Math.Cos(angle)], 10, []);
            }).ToArray();
            using (var bend = NativeGeometry.Build(new SolidPart("Bend", "#778899", bendProfiles)))
            {
                if (bend.Volume <= 0 || NativeGeometry.Tessellate(bend, "#778899").Vertices.Length == 0) throw new Exception("Bent tube failed.");
                results.Add("Native bent tube crossing circle-frame threshold passed.");
            }
            var template = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk", "RVT 2027", "Family Templates", "English", "Metric Generic Model.rft");
            // Exercise the template-with-types case with a temporary template outside the user's library.
            var templateDoc = app.Application.NewFamilyDocument(template);
            try
            {
                using var transaction = new Transaction(templateDoc, "Seed template types");
                transaction.Start();
                templateDoc.FamilyManager.NewType("Existing fixture type");
                transaction.Commit();
                var rfa = Path.Combine(Path.GetDirectoryName(report)!, "native-smoke-template-" + Guid.NewGuid().ToString("N") + ".rfa");
                templateDoc.SaveAs(rfa);
                templateDoc.Close(false);
                templateDoc = null;
                temporaryTemplate = Path.ChangeExtension(rfa, ".rft");
                File.Move(rfa, temporaryTemplate);
            }
            finally { if (templateDoc is { IsValidObject: true }) templateDoc.Close(false); }
            var recipe = new FamilyRecipe("Native test fixture", "FC Native Smoke", "100 mm", "Generic Models", [body]);
            var preview = new NativePreview(recipe, [], project.Title);
            var options = new FamilyCreationOptions((long)BuiltInCategory.OST_GenericModel, recipe.FamilyName, recipe.TypeName, temporaryTemplate);
            var result = FamilyBuildService.CreateInProject(app.Application, project, preview, options, default);
            var family = new FilteredElementCollector(project).OfClass(typeof(Family)).Cast<Family>().Single(f => f.Name == recipe.FamilyName);
            var symbols = family.GetFamilySymbolIds().Select(id => (FamilySymbol)project.GetElement(id)).ToArray();
            if (symbols.Length != 1 || symbols[0].Name != recipe.TypeName) throw new Exception("Family/type names or type count are incorrect.");
            var edited = project.EditFamily(family);
            try
            {
                var forms = new FilteredElementCollector(edited).OfClass(typeof(FreeFormElement)).Cast<FreeFormElement>().ToArray();
                if (forms.Length != 1) throw new Exception("Expected one native freeform solid.");
                var material = (Material)edited.GetElement(forms[0].get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM).AsElementId());
                if (material.Color.Red != 0x22 || material.Color.Green != 0x66 || material.Color.Blue != 0x88) throw new Exception("Family material color differs from preview.");
            }
            finally { edited.Close(false); }
            results.Add("Unsaved family creation/load, family/type names, single type and material passed: " + result);
            try
            {
                FamilyBuildService.CreateInProject(app.Application, project, preview, options, default);
                throw new Exception("Duplicate family was not rejected.");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("already exists")) { results.Add("Existing-family protection passed."); }
            foreach (var category in new[] { BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems, BuiltInCategory.OST_SpecialityEquipment, BuiltInCategory.OST_MechanicalEquipment, BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_ElectricalEquipment })
            {
                var categoryOptions = options with { CategoryId = (long)category, FamilyName = "FC " + category };
                FamilyBuildService.CreateInProject(app.Application, project, preview, categoryOptions, default);
                var created = new FilteredElementCollector(project).OfClass(typeof(Family)).Cast<Family>().Single(f => f.Name == categoryOptions.FamilyName);
                if (created.FamilyCategory.Id.Value != (long)category) throw new Exception("Loaded family has the wrong category.");
            }
            results.Add("All seven offered model categories created and loaded successfully.");
            var probe = app.Application.NewFamilyDocument(temporaryTemplate);
            var temporaryTitle = probe.Title;
            probe.Close(false);
            using (var rename = new Transaction(project, "Create temporary-name collision fixture"))
            {
                rename.Start(); family.Name = temporaryTitle; rename.Commit();
            }
            FamilyBuildService.CreateInProject(app.Application, project, preview, options with { FamilyName = "FC After Temporary Name Collision" }, default);
            if (!family.IsValidObject || family.Name != temporaryTitle) throw new Exception("An existing temporary-name family was changed.");
            results.Add("Temporary-name collision fixture preserved the existing family and loaded the new named family.");
            File.WriteAllText(report, JsonSerializer.Serialize(new { passed = true, results }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception) { File.WriteAllText(report, JsonSerializer.Serialize(new { passed = false, results, error = exception.ToString() }, new JsonSerializerOptions { WriteIndented = true })); }
        finally
        {
            if (project is { IsValidObject: true }) project.Close(false);
            if (temporaryTemplate is not null && File.Exists(temporaryTemplate)) File.Delete(temporaryTemplate);
        }
    }
}
