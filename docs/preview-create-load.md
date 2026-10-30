# Preview, create and load

Implemented 2026-10-30. The plan is in `superpowers/plans/2026-10-30-preview-create-load.md`.

## Runtime

`FamilyCreator.Generation` contains the integrated adaptation of the user's `revit-family-creator` skill, a bounded geometry recipe and the Responses client. The original standalone skill is not changed. No generated code is executed, and no Dynamo runtime is required.

The user's explicitly chosen API model receives the current references, prior recipe and conversation context. The client uses `POST /v1/responses`, `store:false`, strict JSON schema and no automatic retry or model fallback. The geometry schema accepts circular/polygonal loft profiles in millimetres and named colors. Empty parts represent a clarification, not a model ready to create. Coordinates, array sizes and geometry complexity are validated before Revit sees the recipe. A model refusal, incomplete response or invalid recipe cannot become a preview.

`FamilyBuildService` dispatches native work through ExternalEvent. `NativeGeometry` builds Revit solids, checks volume and tessellates their faces for WPF. Circular profile frames are transported along bends to avoid abrupt seam changes. Creation rebuilds the same recipe as native FreeFormElements with materials in an unsaved family document. The new type is created or retained before extra template types are removed; Revit forbids deleting the final type. The document is loaded directly into the captured project, then closed without saving. Existing family names are rejected. No placement is automatic.

The preview is native geometry rendered as triangles, not an independently invented illustration. Native validation is not visual verification of product accuracy. Users must inspect the preview and the resulting family. The app does not yet implement automatic reference-image comparison, boolean voids, connectors or dimensional family parameters.

## Verification

- Release build against installed Revit 2027 APIs: passed, zero warnings/errors.
- Unit tests: 69 passing, covering auth plus generation payloads, attachments, invalid recipes, cancellation, HTTP errors, incomplete/refused responses and name validation.
- WPF harness: passed. Includes a clearly synthetic preview fixture, creation dialog, narrow layouts and failed-revision invalidation. These fixtures do not prove native solids.
- Independent code review: fixed final-template-type deletion and circular-profile seam discontinuity.
- Live inference: attempted with the user-selected GPT-6.1 Sol. Both geometry and minimal Responses requests returned HTTP 429. The provider explicitly reported no API credits remaining. The app now shows this condition clearly and preserves the prompt/references. Model listing succeeds; it does not establish usable generation credits.
- Native Revit smoke test: passed in Revit 2027.3 using disposable documents (report-161bbb9857364ed691b1b8fe4a8333da.json under outputs/native-smoke). Checks covered cylinder volume/tessellation, bent-tube loft, unsaved family loading, exact family/type names, one type, material colors and duplicate-name rejection. The corrected load/rename TransactionGroup is now verified. An expanded category/temporary-title collision test was attempted but remained at startup approval; these extra checks are not claimed as passed. The disposable processes and temporary manifest have been removed.

Commands:

```powershell
dotnet test tests/FamilyCreator.Auth.Tests -c Release
dotnet run --project tests/FamilyCreator.Ui.Smoke -c Release
dotnet build tests/FamilyCreator.Revit.NativeSmoke -c Release
```

`tests/FamilyCreator.Revit.NativeSmoke/start.ps1` launches a separate Revit instance gated by a process-specific environment variable. It writes session/report paths under ignored `outputs/native-smoke`; the test manifest must be removed after testing. It does not open or edit the user's project. Its temporary template file tests templates that already contain a family type; the actual generated output family is loaded without saving an RFA.

Temporary unsaved titles are now checked against existing family names. Colliding temporary documents are held open briefly so Revit allocates a fresh title, then all are closed without saving. Code review found no material issue; the expanded live collision fixture remains unverified.

## Sources

- [OpenAI structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs)
- [OpenAI file inputs](https://developers.openai.com/api/docs/guides/file-inputs)
- [OpenAI image inputs](https://developers.openai.com/api/docs/guides/images-vision)
- [Autodesk document-to-document family loading](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/6a91dc8e-6c2b-52b9-dfc4-d56fa472852b.htm)
- Installed Revit 2027 `RevitAPI.xml`: `CreateLoftGeometry`, `FreeFormElement.Create`, `DeleteCurrentType`, `NewType`, `LoadFamily`.

## Composer and model selection follow-up

Enter sends; Shift+Enter adds a line. Choose opens a searchable API model list. Selection is explicit and persisted; the user chose gpt-6.1-sol. Readiness hints identify missing key/model/prompt. The model catalog and safe quota errors have unit coverage; a routed WPF Enter event reaches generation in the UI harness. API settings include a billing/credits link. No ChatGPT-token inference fallback is used.
