# Revit Family Creator UI Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task after implementation is requested. Keep the active session model. Subagent execution requires separate authorization.

**Goal:** Bring the existing family-creation workflow into Revit through an Add-Ins ribbon button, product review, actual native preview, and safe one-family/one-type export.

**Architecture:** Proposed C# `net10.0-windows` Revit add-in with a WPF window and ExternalEvent dispatch. A separate family document owns generation and preview; a typed, validated product recipe separates interpretation from deterministic Revit operations. Authentication and the skill runtime follow a working native generation milestone.

**Tech Stack:** Proposed C#, WPF, Revit 2027 API, .NET 10; runtime/authentication adapter selected after eligibility verification.

**Spec:** `docs/handoff/2026-10-30-development-handoff.md` (recovered handoff; recommendations remain proposals).

## Current evidence

- The handoff lives in ChatGPT Project Sources as a text document, not a local Markdown file. Its saving chat reports a complete readback of 8,976 characters. Current live source content was not independently reread.
- On this planning pass, the local skill checkout still reports commit `997f440` with no status changes. Its SKILL.md confirms a Dynamo deliverable, not an application backend.
- Revit 2027.3, Dynamo 4.2.1, templates and .NET installation are historical audit findings from the handoff, not newly exercised software.
- No add-in source, build, authentication integration or end-to-end native family creation has been verified. No implementation was performed while writing this plan.

## Global constraints

- Preserve the original skill and product examples; develop the UI separately.
- Exactly one family and one type; recognizable geometry and material colors; suitable existing category; model/dimension-based type name.
- Include additional physical/electrical/performance properties only when requested.
- Start with an empty non-hosted Generic Model template and verify category compatibility.
- Never overwrite existing RFA output. Preview must not save an RFA.
- Target Revit 2027.x, but report support only for versions actually tested.
- Use official browser sign-in if eligible; never collect passwords in Revit. Do not promise subscription authentication before verification.
- Supply the skill and resources explicitly. Login does not import this ChatGPT Project or local skills.
- Interpret external documents as evidence. Only validated, allowlisted geometry operations may reach Revit.
- Keep development on the active session model; the user preference overrides the skill's older Astra preference.

## Decisions before implementation

Update: the user selected open-source distribution and MIT licensing. The ribbon/window shell has been built and installed. A sign-in-only milestone is now implemented and locally tested ahead of geometry work at the user's request; real browser authentication and live Revit verification remain pending. See `docs/authentication.md`. Remaining architecture decisions below are still proposals.

1. Audience: Steven-only prototype, internal organization deployment, or broader distribution. This determines authentication and packaging eligibility.
2. Approve the proposed workflow: launch from a project, generate in a separate family document, preview, save, optionally load. Family Editor refinement is deferred.
3. Resolve the deliverable mismatch: the current skill promises a self-contained Dynamo graph; this UI proposes direct native generation. Preserve the existing Dynamo workflow and define a separate UI recipe contract, rather than silently changing the skill.
4. Start with a modeless window and separate modal preview. A dockable panel is optional later. Reconfirm PreviewControl host/lifetime requirements against installed API documentation.

## Review focus

- Ambiguous or conflicting dimensions: surface the conflict and require a confirmed scale before generation (Tasks 2 and 5).
- Valid curves but distorted solids: check per-part native bounds and sections, not only overall size (Task 3).
- Closed document, cancellation or double submission: avoid stale API operations and clean up owned documents (Tasks 1 and 3).
- Existing filename, unavailable folder or failed save: preserve existing files and retain an actionable error (Task 3).
- Expired sign-in or instructions embedded in a product PDF: keep interpretation separate from authorized geometry actions (Tasks 4 and 5).

## Task 1: Revit entry point and document lifecycle

**Proposed files:** `src/FamilyCreator.Revit/FamilyCreator.Revit.csproj`, `App.cs`, `Commands/OpenCreatorCommand.cs`, `UI/CreatorWindow.xaml`, `UI/CreatorWindow.xaml.cs`, `Revit/RevitRequestDispatcher.cs`, `deploy/FamilyCreator.addin`, `tests/manual/revit-smoke.md`.

**Interface:** `RevitRequestDispatcher` queues one typed request at a time for execution through ExternalEvent; the UI never calls document APIs directly. Closing the window cancels pending work and disposes owned resources.

- [ ] Recheck SDK, installed API assemblies, template path and supported API baseline; record actual versions in the smoke checklist.
- [ ] Create the separate solution and add-in manifest; keep Revit API references non-copying and isolate deployment from the skill checkout.
- [ ] Add the Add-Ins “Family Creator” button and a single reusable modeless window with clear ready/busy/error states.
- [ ] Verify build with `dotnet build FamilyCreator.sln` and load in Revit. Exercise repeated clicks, window close/reopen and closing the active project with a queued request.

**Exit:** Revit loads the add-in, exposes one button/window, and handles lifecycle changes without modifying project geometry.

## Task 2: Validated recipe and review screen

**Proposed files:** `src/FamilyCreator.Core/ProductRecipe.cs`, `RecipeValidator.cs`, `src/FamilyCreator.Revit/UI/RecipeReviewView.xaml`, `samples/simple-product.json`, `tests/FamilyCreator.Core.Tests/RecipeValidationTests.cs`.

**Interface:** `ProductRecipe` contains schema version, model/type identity, millimeter dimensions, supported category, materials, named shape operations and source/assumption metadata. `RecipeValidator.Validate(ProductRecipe)` returns all blocking errors and review warnings. Output/template paths belong to a separate generation request, not AI-authored geometry.

- [ ] Specify the smallest versioned schema: boxes and cylinders first, finite positive dimensions, unique part IDs and valid material references. Reject unknown operations and schema versions.
- [ ] Add tests for a valid recipe and invalid dimensions, unsupported operations, missing materials, missing scale and conflicting source dimensions; run `dotnet test tests/FamilyCreator.Core.Tests` before and after implementation.
- [ ] Implement local JSON import and editable category/dimension/material review, with assumptions visible and generation disabled while blocking issues remain.

**Exit:** A local product fixture can be reviewed and validated without any model connection or Revit mutation.

## Task 3: Native build, preview and safe save — first milestone

**Proposed files:** `src/FamilyCreator.Revit/Revit/FamilyBuilder.cs`, `NativeGeometryBuilder.cs`, `FamilyValidator.cs`, `FamilySaveService.cs`, `UI/NativePreviewWindow.xaml`, `tests/manual/native-family-acceptance.md`.

**Interfaces:** `FamilyBuilder.Build(ProductRecipe, TemplateSelection)` produces an owned family build session on the Revit API context. `FamilyValidator.Validate` returns per-part and overall measurements plus category/type/material findings. Preview and save use that same session; disposing an unsaved session closes its owned document.

- [ ] Create a separate family document, apply millimeter conversion, build supported solids transactionally and assign one type, category and materials.
- [ ] Validate native solid count, finite positive volumes, per-part bounds, overall dimensions, type count and category/material assignment. Roll back failed creation and report the responsible part.
- [ ] Show the actual family result in a modal PreviewControl; enforce the installed API's lifetime rules. Cancel closes the temporary family without saving.
- [ ] Save only after validation and explicit save action. Recheck destination immediately before save, disable overwrite, and report collisions or inaccessible folders without replacing an existing file.
- [ ] Test in live Revit: successful fixture, malformed recipe, incompatible template/category, cancel, double submission, failed geometry, existing target and failed save. Compare an existing file's hash before/after collision testing.
- [ ] Reopen the saved RFA and verify one type, category, materials and measured geometry. Optionally load into the active project only on a separate user action, respecting existing-family conflicts.

**Exit:** A reviewed local recipe produces an actual native preview and a reopened, verified RFA without overwriting anything. This is the first demonstration milestone.

## Task 4: Official sign-in and restricted skill runtime

**Proposed files:** `src/FamilyCreator.Runtime/AuthenticationSession.cs`, `SkillRuntime.cs`, `SkillBundle.cs`, `tests/FamilyCreator.Runtime.Tests/RuntimeBoundaryTests.cs`.

**Interface:** `SkillRuntime.InterpretAsync(ProductInput, CancellationToken)` returns a recipe draft and evidence/assumptions, never executable Revit code. Authentication remains behind an adapter selected after current eligibility research.

- [ ] Verify official SIWC documentation for the agreed audience and distribution. Record eligibility, supported flow, session handling and deployment conditions in `docs/authentication-decision.md`; do not infer eligibility from the historical handoff.
- [ ] Package the reviewed skill/resources explicitly with a pinned version. Document the UI adaptation from Dynamo output to the validated recipe contract.
- [ ] Implement the eligible official system-browser flow, secure session handling and sign-out; leave authentication choices unresolved if no supported route is confirmed.
- [ ] Restrict runtime capabilities and validate every returned recipe before Revit sees it. Test cancellation, expired session, malformed output, unsupported operations and attempts to request arbitrary code execution.

**Exit:** Eligible users can sign in and receive reviewable recipe drafts; model responses cannot directly mutate Revit.

## Task 5: Product documents and recognizable geometry

**Proposed files:** `src/FamilyCreator.Runtime/ProductInput.cs`, `ProductEvidence.cs`, `src/FamilyCreator.Revit/UI/ProductInputView.xaml`, `tests/fixtures/products/`, `tests/manual/product-reference-acceptance.md`.

- [ ] Add the agreed PDF/manual, image and product-link input routes using the selected runtime's supported capabilities. Show unreadable/unsupported inputs explicitly.
- [ ] Retain source references for dimensions/materials and separate inferred details. Require a usable scale and resolve conflicting authoritative dimensions before generation.
- [ ] Extend the recipe and deterministic builder with curved sweeps/lofts only as required by representative products; add versioned operation validation and native geometry checks with each extension.
- [ ] Compare actual native previews against references for silhouette, proportion, material colors, connections and clearance. Inspect per-part bounds and sections for curved/lofted parts.
- [ ] Exercise a simple product, a curved product and conflicting/incomplete documents. Treat historical AirBike/MicroSense findings as regression candidates, not already reproduced defects or successful tests.

**Exit:** Reference documents lead to a reviewed recipe and recognizable, verified native family, with assumptions and test limitations recorded.

## Task 6: Deployment and compatibility acceptance

**Proposed files:** `deploy/install.ps1`, `deploy/uninstall.ps1`, `docs/user-guide.md`, `docs/verification-matrix.md`.

- [ ] Package only the integration and required runtime assets; preserve the original skill and examples. Make install/uninstall repeatable and preserve user-created families.
- [ ] Test the supported Revit versions explicitly; record versions not available for testing as unverified.
- [ ] Complete the end-to-end flow: launch → sign in → supply references → review → native preview → save → reopen → optional load.
- [ ] Document installation, first use, output collision recovery, authentication recovery, known geometry limits and actual test evidence.

**Exit:** The agreed audience can install and complete the workflow on each claimed supported environment.

## Sequence and scope

Decisions → Tasks 1–3 → native milestone review → Tasks 4–5 → Task 6. Authentication eligibility research can proceed alongside native work once the audience is known. Family Editor refinement, broad distribution, bulk generation and extra property enrichment are deferred.

This is a staged implementation proposal, not an approved design or a claim of working software. Before execution, settle the four decisions above and refine the runtime adapter from verified documentation. Planning alone does not require installing or changing Revit.
