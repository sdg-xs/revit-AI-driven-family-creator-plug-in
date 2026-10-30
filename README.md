# Revit Family Creator UI

An open-source, MIT-licensed Revit add-in with an Add-Ins ribbon panel and **Family Creator** button opening one modeless WPF window. Repeated clicks activate the existing window; closing it allows a fresh window next time. The window is owned by Revit and closes during add-in shutdown.

The first-run welcome screen offers **Log in with ChatGPT** above **Use API key**, keeping the existing navy styling. The login button starts browser sign-in; the API-key button opens key setup. **Other connection options** returns to the welcome screen. ChatGPT includes saved accounts, cancellation, status and sign-out. API-key mode includes masked entry, encrypted save/replace, a connection check and local removal. API-key generation now supports reference inputs, native geometry preview and explicit family creation/loading. ChatGPT sign-in is not connected to generation yet.

To use an API key: choose **Use API key**, paste your own key into the masked field, click **Save key**, then **Test connection**. The check uses the models-list endpoint only and does not verify model-generation permission or billing balance. Key removal clears the local copy; revoke the key in OpenAI Platform if needed. Neither method silently falls back to the other.

## Conversation workspace

After sign-in or saving/checking an API key, the window opens the conversation workspace. Later launches open it directly when the selected saved credential is available (a saved key is not a guarantee of API access). The top-right **Settings** button returns to connection settings; **Back to conversation** preserves the current draft. Expired/signed-out ChatGPT sessions and missing API keys stay on connection setup. The composer accepts multiline text and pasted product details at the bottom, while the response area scrolls above it. The workspace supports narrow windows down to 420 units wide; settings use the wider form layout.

Click the model chip beneath the composer to open the styled model picker. Search readable model names or exact API IDs, select a card, and choose **Use model**. Your saved model is marked **In use**. Manual ID entry is available under an expander, including when the catalog cannot be loaded. Attach references with **+**, describe the family, and press **Enter** or Send. **Shift+Enter** inserts a new line. The composer explains why Send is disabled when a connection, model or prompt is missing. The model must support Responses and structured outputs, plus image/PDF inputs when used. The app remembers the model ID without selecting or substituting one automatically. Send transmits the prompt, conversation context, previous recipe and currently attached files to OpenAI; API charges apply. Cancel stops waiting but cannot undo usage already incurred.

The bundled workflow interprets references and generates a validated geometry recipe. Revit builds native solid lofts without modifying the project, then the extension displays their tessellation. Drag to orbit, scroll to zoom, or use arrow and plus/minus keys. Inspect dimensions, proportions and joints; request revisions in the conversation. A failed revision disables creation of the previous preview.

Click **Create family** after reviewing the preview. Choose category, family name and type name, with suggested defaults. An installed non-hosted Generic Model template is required; the dialog finds the standard English template or lets you select one. The add-in builds one family/type in a temporary document, applies colors and loads it directly into the project. No output RFA or Dynamo graph is saved. Save the project to retain the family, then place it using Revit's normal tools. Existing family names are rejected instead of overwritten; changing the active project requires a fresh preview.

Initial support: native freeform solids made from circular/polygonal loft profiles, in non-hosted Generic Models, Furniture, Furniture Systems, Specialty Equipment, Mechanical Equipment, Plumbing Fixtures or Electrical Equipment categories. These are not dimensionally parametric families. Voids/cuts, connectors, hosted doors/windows, and automatic visual comparison are not implemented. The model labels approximations; native validity does not establish fidelity to the product. Product URLs are not fetched automatically: attach the source or paste its contents.

Drafts, references and conversation context survive Settings navigation but are not saved after closing. **New chat** clears the current conversation and preview.

## Build and install

Requires Windows, .NET 10 SDK and installed Revit 2027 APIs.

```powershell
dotnet build .\src\FamilyCreator.Revit\FamilyCreator.Revit.csproj -c Release
.\deploy\install.ps1
```

Run the installer in PowerShell 7. It copies the add-in and authentication dependencies into a content-versioned folder beneath `%APPDATA%\Autodesk\Revit\Addins\2027\FamilyCreator` and writes `FamilyCreator.addin` beside it. It does not copy Autodesk DLLs or overwrite an older loaded build. Save your work and restart Revit to load the update if it is already running. Revit may ask you to review the unsigned local add-in on first launch.

## Verification

On 2026-10-30, Release build passed with zero warnings and zero errors against installed Revit 2027 API assemblies. Automated tests passed (69 cases), including authentication, encrypted key storage and selected-credential workspace access. WPF smoke checks cover both connection choices, narrow/normal conversation layouts, settings navigation, draft preservation and credential removal. The user reported a successful live API-key connection check. A live GPT-6.1 Sol request was attempted: the provider rejected it with HTTP 429 and explicitly reported no credits remaining. This account limitation must be resolved before live generation can succeed; model listing alone does not prove available generation credits. The WPF harness also covers Enter submission, generation success/failure, preview rendering, model-picker rendering, the creation dialog and stale-preview invalidation. The native Revit test passed cylinder/bent-tube geometry, unsaved family loading, final family/type names, single-type output, material colors and duplicate-name protection. Native Revit verification is recorded separately in docs/preview-create-load.md.

```powershell
dotnet test .\tests\FamilyCreator.Auth.Tests -c Release
```

See [authentication notes](docs/authentication.md) for storage, supported scope and manual acceptance steps. Credentials are encrypted for the Windows user under `%LOCALAPPDATA%\FamilyCreator\Auth`, outside the repository. Existing Codex/ChatGPT credentials are not read or reused.

Live acceptance checklist:

- [ ] Start Revit 2027 and verify Add-Ins > Family Creator panel/button appears.
- [ ] Click Family Creator and verify the window is displayed and owned by Revit.
- [ ] Click again: the same window comes forward, with no duplicate.
- [ ] Close and reopen; minimize and click again to restore.
- [ ] Keep the window open while opening/closing a project; the shell remains usable.
- [ ] Close Revit normally and verify the owned window closes.

- [ ] Complete a real OpenAI browser sign-in, confirm account identity, then test cancellation and sign-out.

Revit geometry and family operations run through ExternalEvent. Temporary family documents use transactions and close without saving; the project must be editable with no active transaction.

## License

[MIT](LICENSE). Autodesk Revit and its API assemblies are separate prerequisites and are not distributed with this repository. OpenAI services retain their own eligibility and usage conditions.

Reference attachments: the + button selects multiple local images, PDFs, or text/Markdown files. Selected references appear above the prompt with individual remove buttons. Duplicate paths are ignored, and references survive Settings navigation in the current window. Files stay local until Send, then are included in the model request. The app allows at most 10 attachments, 10 MB per file and 20 MB total; text files are limited to 256 KB. Missing/unsupported files are reported. File contents are not copied or persisted locally by the add-in.
