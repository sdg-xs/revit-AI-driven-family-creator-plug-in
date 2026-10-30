using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyCreator.Generation;
using System.IO;

namespace FamilyCreator.Revit;

internal sealed class FamilyBuildService : IFamilyBuildService, IExternalEventHandler
{
    private readonly ExternalEvent externalEvent;
    private Action<UIApplication>? pending;
    private Action? cancelPending;
    private Document? previewTarget;
    private NativePreview? currentPreview;
    private bool disposed;
    private static readonly BuiltInCategory[] SupportedCategories =
    [
        BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems,
        BuiltInCategory.OST_SpecialityEquipment, BuiltInCategory.OST_MechanicalEquipment,
        BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_ElectricalEquipment
    ];

    public FamilyBuildService() => externalEvent = ExternalEvent.Create(this);
    public string GetName() => "Family Creator geometry and family loading";
    public void Execute(UIApplication app)
    {
        var action = pending;
        pending = null;
        cancelPending = null;
        action?.Invoke(app);
    }

    private Task<T> Run<T>(Func<UIApplication, T> action, CancellationToken token)
    {
        if (disposed) throw new ObjectDisposedException(nameof(FamilyBuildService));
        if (pending is not null) throw new InvalidOperationException("Revit is still processing the previous request.");
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = token.Register(() => completion.TrySetCanceled(token));
        pending = app =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                completion.TrySetResult(action(app));
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(token); }
            catch (Exception e) { completion.TrySetException(e); }
            finally { registration.Dispose(); }
        };
        cancelPending = () => { registration.Dispose(); completion.TrySetCanceled(); };
        if (externalEvent.Raise() != ExternalEventRequest.Accepted)
        {
            pending = null;
            cancelPending = null;
            registration.Dispose();
            completion.TrySetException(new InvalidOperationException("Revit cannot accept the operation now. Close any active dialog and try again."));
        }
        return completion.Task;
    }

    public Task<NativePreview> PreviewAsync(FamilyRecipe recipe, CancellationToken token) => Run(app =>
    {
        currentPreview = null;
        previewTarget = null;
        recipe.Validate();
        var target = Project(app);
        var meshes = new List<PreviewMesh>();
        var count = 0;
        foreach (var part in recipe.Parts)
        {
            token.ThrowIfCancellationRequested();
            using var solid = NativeGeometry.Build(part);
            var mesh = NativeGeometry.Tessellate(solid, part.Color);
            count += mesh.Vertices.Length;
            if (count > 900000) throw new InvalidOperationException("The preview exceeds 300,000 triangles. Simplify the model.");
            meshes.Add(mesh);
        }
        if (meshes.Count == 0) throw new InvalidOperationException("No geometry was supplied for preview.");
        previewTarget = target;
        currentPreview = new NativePreview(recipe, meshes.ToArray(), target.Title);
        return currentPreview;
    }, token);

    public Task<(FamilyCategoryChoice[] Categories, string TemplatePath)> GetOptionsAsync(CancellationToken token) => Run(app =>
    {
        var target = Project(app);
        var categories = SupportedCategories.Select(id => Category.GetCategory(target, id)).Where(c => c is not null)
            .Select(c => new FamilyCategoryChoice(c.Id.Value, c.Name)).OrderBy(c => c.Name).ToArray();
        var roots = new[] { app.Application.FamilyTemplatePath,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Autodesk", "RVT 2027", "Family Templates", "English") };
        var template = roots.Where(Directory.Exists).SelectMany(root => new[] { Path.Combine(root, "Metric Generic Model.rft"), Path.Combine(root, "Generic Model.rft") }).FirstOrDefault(File.Exists) ?? "";
        return (categories, template);
    }, token);

    public Task<string> CreateAsync(NativePreview preview, FamilyCreationOptions options, CancellationToken token) => Run(app =>
    {
        var target = Project(app);
        if (!ReferenceEquals(preview, currentPreview) || previewTarget is null || !previewTarget.IsValidObject || !ReferenceEquals(target, previewTarget))
            throw new InvalidOperationException("The project or preview has changed. Generate a fresh preview in the intended project.");
        var result = CreateInProject(app.Application, target, preview, options, token);
        currentPreview = null;
        return result;
    }, token);

    internal static string CreateInProject(Autodesk.Revit.ApplicationServices.Application application, Document target, NativePreview preview, FamilyCreationOptions options, CancellationToken token)
    {
        var familyName = FamilyRecipe.ValidateName(options.FamilyName, "family name");
        var typeName = FamilyRecipe.ValidateName(options.TypeName, "type name");
        if (!SupportedCategories.Any(c => (long)c == options.CategoryId)) throw new InvalidOperationException("Select a supported non-hosted model category.");
        var existingNames = new FilteredElementCollector(target).OfClass(typeof(Family)).Cast<Family>().Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existingNames.Contains(familyName))
            throw new InvalidOperationException("A family with this name already exists. Choose a different name; existing families are never overwritten.");
        if (!File.Exists(options.TemplatePath) || !string.Equals(Path.GetExtension(options.TemplatePath), ".rft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select an installed Generic Model family template (.rft).");
        Document? familyDoc = null;
        var reservedDocuments = new List<Document>();
        try
        {
            familyDoc = application.NewFamilyDocument(options.TemplatePath);
            // Unsaved LoadFamily uses the document title before the final target rename.
            // Keep colliding temporary documents open briefly so Revit allocates a new title.
            while (existingNames.Contains(familyDoc.Title))
            {
                if (reservedDocuments.Count >= 100) throw new InvalidOperationException("Could not allocate a unique temporary family name. No family was loaded.");
                reservedDocuments.Add(familyDoc);
                familyDoc = null;
                familyDoc = application.NewFamilyDocument(options.TemplatePath);
            }
            if (familyDoc.OwnerFamily.FamilyPlacementType != FamilyPlacementType.OneLevelBased)
                throw new InvalidOperationException("Use a non-hosted Generic Model template for this workflow.");
            using (var transaction = new Transaction(familyDoc, "Build previewed family"))
            {
                transaction.Start();
                var failures = transaction.GetFailureHandlingOptions().SetFailuresPreprocessor(new RollBackOnError()).SetClearAfterRollback(true);
                transaction.SetFailureHandlingOptions(failures);
                familyDoc.OwnerFamily.FamilyCategory = Category.GetCategory(familyDoc, new ElementId(options.CategoryId));
                var manager = familyDoc.FamilyManager;
                var selectedType = manager.Types.Cast<FamilyType>().FirstOrDefault(t => t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase)) ?? manager.NewType(typeName);
                foreach (FamilyType existing in manager.Types.Cast<FamilyType>().Where(t => t.Name != selectedType.Name).ToArray())
                {
                    manager.CurrentType = existing;
                    manager.DeleteCurrentType();
                }
                manager.CurrentType = selectedType;
                var materialIds = new Dictionary<string, ElementId>();
                foreach (var part in preview.Recipe.Parts)
                {
                    token.ThrowIfCancellationRequested();
                    using var solid = NativeGeometry.Build(part);
                    var element = FreeFormElement.Create(familyDoc, solid);
                    if (!materialIds.TryGetValue(part.Color, out var materialId))
                    {
                        materialId = Material.Create(familyDoc, "FC " + part.Color[1..] + " " + Guid.NewGuid().ToString("N")[..6]);
                        ((Material)familyDoc.GetElement(materialId)).Color = new Color(Convert.ToByte(part.Color.Substring(1, 2), 16), Convert.ToByte(part.Color.Substring(3, 2), 16), Convert.ToByte(part.Color.Substring(5, 2), 16));
                        materialIds.Add(part.Color, materialId);
                    }
                    var material = element.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                    if (material is null || material.IsReadOnly || !material.Set(materialId)) throw new InvalidOperationException($"Could not apply material to '{part.Name}'.");
                    element.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(part.Name);
                }
                if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit rejected the family geometry; nothing was loaded.");
            }
            token.ThrowIfCancellationRequested();
            using var group = new TransactionGroup(target, "Load generated family");
            group.Start();
            var loaded = familyDoc.LoadFamily(target) ?? throw new InvalidOperationException("Revit did not load the family. Existing families were not replaced.");
            using (var rename = new Transaction(target, "Name generated family"))
            {
                rename.Start();
                rename.SetFailureHandlingOptions(rename.GetFailureHandlingOptions().SetFailuresPreprocessor(new RollBackOnError()).SetClearAfterRollback(true));
                loaded.Name = familyName;
                if (rename.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Could not name the family. Loading was rolled back.");
            }
            if (group.Assimilate() != TransactionStatus.Committed) throw new InvalidOperationException("Could not finish loading the family.");
            return $"Loaded {loaded.Name} · {typeName} into {target.Title}. Save the project to retain it. No separate RFA was saved. The family is available for placement.";
        }
        finally
        {
            if (familyDoc is { IsValidObject: true }) familyDoc.Close(false);
            foreach (var document in reservedDocuments) if (document.IsValidObject) document.Close(false);
        }
    }

    private static Document Project(UIApplication app)
    {
        var doc = app.ActiveUIDocument?.Document;
        if (doc is null || doc.IsFamilyDocument || doc.IsReadOnly || doc.IsModifiable)
            throw new InvalidOperationException("Open an editable Revit project and finish any active command before continuing.");
        return doc;
    }

    private sealed class RollBackOnError : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor) => accessor.GetFailureMessages().Any(f => f.GetSeverity() == FailureSeverity.Error)
            ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
    }

    public void Dispose()
    {
        disposed = true;
        cancelPending?.Invoke();
        pending = null;
        externalEvent.Dispose();
    }
}
