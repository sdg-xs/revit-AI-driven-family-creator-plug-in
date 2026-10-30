# Preview, create and load implementation plan

> Execute inline using the executing-plans skill. The user has authorized planning and implementation in this session.

**Goal:** Turn attached references and a prompt into a native geometry preview, then explicitly create and load one named family/type without saving an RFA or producing a Dynamo graph.

**Architecture:** A provider-independent geometry recipe describes colored solid lofts with circular or polygonal profiles in millimetres. OpenAI Responses returns validated recipes, never executable code. A Revit ExternalEvent builds native solids and returns their tessellation for the WPF preview; the same recipe builds FreeFormElements in a temporary family document when the user selects Create family. Load directly into the captured project and close the temporary document without saving.

**Tech stack:** .NET 10, WPF, Revit 2027 APIs, OpenAI Responses over HttpClient.

**Spec:** User's 2026-10-30 request in this chat: references → preview → category/family/type prompt → create and load, no Dynamo deliverable and no mandatory RFA save.

## Constraints and scope
- Keep the existing navy styling, connection choices and attachment composer.
- API-key generation only initially; no undocumented ChatGPT inference endpoint or silent credential fallback.
- User enters the API model explicitly; never substitute models.
- Bundle the skill's reference interpretation, assembly and verification principles with the add-in, adapting delivery to native geometry. Do not modify the user's standalone skill.
- Initial geometry is native freeform solids, not dimensional parametric families. Supported categories are non-hosted model families compatible with a Generic Model template.
- No project mutation during preview. Never execute model-generated Python/C#.
- No paid live generation during automated tests; use a fake HTTP handler. Real inference and live Revit loading must be reported separately from offline checks.

## Review focus
1. Malformed, non-finite or excessive geometry must fail before native execution.
2. Cancellation, failed generation and stale previews must never enable creation for an unreviewed revision.
3. Project switching/closing, name conflicts and load failures must leave existing families untouched.
4. Attachments must be bounded, supported and sent only on explicit Send; missing files must be reported.
5. Preview and creation must use identical native geometry construction; invalid lofts must identify the part.

## Tasks
- [x] 1. Add `FamilyCreator.Generation`: recipe types/validation, bundled workflow/schema, Responses client and bounded attachment encoding. Test invalid geometry, provider failures/refusals, payloads and cancellation.
- [x] 2. Add `FamilyBuildService`: ExternalEvent dispatcher, native loft builder, native tessellation, category list, template resolution, temporary family/type/material creation, direct loading, cleanup and conflict handling. Compile against installed Revit API.
- [x] 3. Add orbit/zoom native geometry viewer, prompt submission/cancel and revision state. Add a creation dialog with category/name/type/template fields after preview. Preserve conversation context and drafts; show provider/geometry failures clearly.
- [x] 4. Verify unit tests and WPF rendering/navigation. Perform independent code review and fix material findings. Update docs, build and install; record any live Revit/inference checks still requiring the restarted host.

## Verification record
Implemented and reviewed. 66 unit tests and the WPF harness pass. Native cylinder/bend geometry passed in Revit 2027.3; unsaved loading revealed a final-name issue, now corrected with load/rename in a TransactionGroup. The user requested finishing without rerunning the native test. Corrected end-to-end naming/type/material checks and paid live inference remain unverified. See ../../preview-create-load.md for the verification record.
