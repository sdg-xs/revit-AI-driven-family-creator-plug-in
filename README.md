# Revit Family Creator

An open-source Revit 2027 add-in for turning product references into a geometry preview, then creating and loading a family directly into a project. The interface opens from **Add-Ins → Family Creator**.

**Current status:** API-key generation, reference attachments, model selection, native geometry preview and family loading are implemented. ChatGPT browser sign-in is available, but **does not yet support generation**. Live AI generation requires an OpenAI API key with model access and available credits. No Dynamo installation or graph is required.

## Installation

### Requirements

- Windows with Autodesk Revit **2027** installed. Other Revit versions are not supported by this build.
- **.NET 10 SDK** to build the add-in; the runtime alone is insufficient.
- **PowerShell 7** to run the installer.
- Git to clone the repository, or an extracted copy of the repository ZIP.
- An installed **non-hosted Generic Model family template** (`.rft`) for family creation.
- Internet access for restoring NuGet packages and using OpenAI services.

For AI generation, you also need your own OpenAI API key, available API credits and a model supporting Responses and structured outputs. Image/PDF references additionally require compatible input support. A successful sign-in or model-list check does not establish generation access or billing balance.

### Build and install from source

1. Save your work and close Revit.
2. Open **PowerShell 7** and clone the repository:

   ```powershell
   git clone https://github.com/sdg-xs/revit-AI-driven-family-creator-plug-in.git
   cd revit-AI-driven-family-creator-plug-in
   ```

   If you downloaded a ZIP, extract it and open PowerShell in the extracted repository folder instead.

3. Check that the required SDK is installed, then run the installer:

   ```powershell
   dotnet --list-sdks
   .\deploy\install.ps1
   ```

   The SDK list must include a `10.x` version. The installation script builds the Release configuration, checks the result, copies the add-in and its dependencies, and writes the Revit add-in manifest. It stops if the build fails.

4. Start Revit 2027. If Revit displays an unsigned-add-in prompt, review **Family Creator** and choose whether to load this local build.
5. Open an editable project and click **Add-Ins → Family Creator**.

The installation is per Windows user. Files are installed under:

```text
%APPDATA%\Autodesk\Revit\Addins\2027\FamilyCreator.addin
%APPDATA%\Autodesk\Revit\Addins\2027\FamilyCreator\build-<hash>\
```

The project expects Revit at `C:\Program Files\Autodesk\Revit 2027`. For a different installation location, set `RevitInstallDir` in the same PowerShell session before running the installer:

```powershell
$env:RevitInstallDir = 'D:\Autodesk\Revit 2027'
.\deploy\install.ps1
```

Use your actual installation directory. Autodesk API DLLs are referenced from that directory and are not distributed with this repository.

If your organization blocks PowerShell scripts, use its approved execution method or contact your administrator; installation does not require changing machine-wide execution policy.

### Updating

Save your work and close Revit, then run these commands from the repository folder:

```powershell
git pull --ff-only
.\deploy\install.ps1
```

Reopen Revit to load the new build. The installer uses versioned folders and updates one manifest; it does not replace a DLL already loaded into a running Revit process. Older build folders do not create additional ribbon extensions.

### Uninstalling

Close Revit and remove **only** `FamilyCreator.addin` from the installation folder above. You can also remove the adjacent `FamilyCreator` directory to reclaim its build files. Leave other add-ins and their manifests untouched.

Uninstalling does not erase saved credentials. To remove them through the app, use **Settings → Remove key** and/or **Sign out** before uninstalling. Local removal of an API key does not revoke the key at OpenAI; revoke it in your OpenAI account if needed. To erase all local Family Creator settings after closing the app, remove `%LOCALAPPDATA%\FamilyCreator`.

## First-run setup

1. Open **Add-Ins → Family Creator** and choose **Use API key**.
2. Paste your key into the masked field and click **Save key**. The app opens the conversation workspace.
3. Open **Settings** and use **Test connection** if you want to check model-list access. Use **Back to conversation** to return. This test does not check generation credits or model-generation permission.
4. Click the **model chip** below the prompt. Search the available models, select a card, and click **Use model**. The saved choice is marked **In use**. You can enter an exact ID under **Enter a model ID manually** if necessary.

Create/manage keys in [OpenAI API keys](https://platform.openai.com/api-keys). Check credits in [OpenAI API billing](https://platform.openai.com/settings/organization/billing/); a billing link is also available in connection settings.

Saved connections open directly into the workspace on later launches. The app remembers your selected model without automatically choosing or substituting one. Model-list access does not guarantee that every listed model supports this workflow.

**ChatGPT sign-in:** **Settings → Other connection options → Log in with ChatGPT** starts browser authentication. This currently supports account connection only. It cannot replace the API key for generation, and organization/account policy may deny access. The app does not bypass that policy or silently switch credentials.

## Creating a family

### 1. Open a project and attach references

Start from an editable Revit **project**, not a family document. Finish any active Revit command or modal dialog before generating.

Use **+** beside the prompt to select product images, drawings, PDFs or text specifications. Each attachment has a remove button. Supported files:

| References | Formats |
| --- | --- |
| Images | PNG, JPG/JPEG, WEBP, GIF |
| Documents | PDF, TXT, Markdown (`.md`) |

Limits are **10 attachments**, **10 MB per file**, **20 MB total**, and **256 KB per text file**. The selected model must support the supplied inputs. References stay local until you press Send. Product links are not fetched automatically: attach the source document/image or paste its relevant contents.

### 2. Describe the family and send

Give known dimensions, units, materials and the features that matter. For example:

> Create a freestanding side table from the attached drawing. Overall size: 600 mm wide, 400 mm deep, 500 mm high. Use a 25 mm oak-colored top and four black cylindrical legs, 30 mm in diameter. Show a preview before creating the family.

- **Enter** or the **↑ Send button** submits the request.
- **Shift+Enter** inserts a new line.
- **Cancel** stops waiting for the current request; usage already incurred may still be billed.
- A hint below the composer explains why Send is disabled if a key, model or prompt is missing.

Send shares the prompt, current reference files, conversation context and previous geometry recipe with OpenAI. API charges apply. If essential details are missing, the assistant may ask a question instead of producing geometry.

### 3. Inspect and refine the preview

The add-in builds native Revit solids without adding a family to the project, then renders their tessellation in the conversation.

- Drag to orbit and scroll to zoom.
- With the preview focused, arrow keys rotate and plus/minus keys zoom.
- Check dimensions, proportions, connections and material colors.
- Send a follow-up request to refine the design. Each revision produces a replacement preview; a failed revision disables creation of the previous preview.

A valid solid does not prove that the model faithfully represents the reference. Inspect the geometry before creating it.

### 4. Create and load

Click **Create family…**, then choose:

- **Category** from the supported model categories.
- **Family name**, unique in the current project.
- **Type name**, with a suggested value you can edit.

The **Family template** expander lets you choose an installed non-hosted Generic Model `.rft`. The app finds standard English templates automatically when present. A common location is:

```text
C:\ProgramData\Autodesk\RVT 2027\Family Templates\English\Metric Generic Model.rft
```

Click **Create and load**. The add-in creates one family with one type, applies material colors, and loads it into the project. **No output `.rfa` file or Dynamo graph needs to be saved.** The temporary family document closes after loading.

Place the loaded family using Revit's normal tools. **Save the project** to retain the family. If you later need a separate `.rfa`, save/export it through Revit's normal family workflow. The add-in does not automatically place an instance or overwrite an existing family.

### Conversation and settings

**Settings** opens connection management without losing the current draft or attachments. **New chat** clears the conversation, references and preview. Conversation history, drafts and attachments are not restored after closing the window. If you switch the active Revit project, generate a fresh preview before creating the family.

## Supported scope

The current backend creates **native freeform solids** from circular and polygonal loft profiles. It supports non-hosted Generic Models, Furniture, Furniture Systems, Specialty Equipment, Mechanical Equipment, Plumbing Fixtures and Electrical Equipment categories.

These are not dimensionally parametric families. Hosted doors/windows, voids and boolean cuts, connectors, automatic reference-image comparison, and direct generation inside an existing family document are not implemented. The integrated workflow follows reference interpretation and assembly principles from `revit-family-creator`; the original standalone skill is not required at runtime.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Family Creator is missing from Add-Ins | Confirm Revit 2027, the Windows user used for installation, and the manifest path. Run the installer successfully and restart Revit. Check whether the unsigned add-in was declined. |
| The old interface still appears | Close all Revit processes and reopen. An already loaded assembly is not replaced by installation. |
| Two extensions ask to load | Check the names on the prompts. The production installer registers only **Family Creator**. **Family Creator Native Smoke** is a developer-only test add-in; close its test instance and remove its temporary `FamilyCreator.NativeSmoke.addin` manifest if left behind. Do not remove unrelated extensions. |
| Send is disabled | Save/select an API key, choose a model and enter a prompt. ChatGPT sign-in alone cannot generate. |
| The model list cannot load | Check network access and the key's model-list permission. Manual ID entry remains available when the catalog request fails. |
| HTTP 429 / no credits remaining | Check API billing, credits and project/model limits. A key may list models while having no generation credits. Add credits or resolve the reported limit, then retry. |
| HTTP 400/404 or unsupported input | Check the exact model ID and its Responses, structured-output and image/PDF capabilities. |
| Business ChatGPT sign-in is denied | Ask your workspace administrator or OpenAI support about third-party access eligibility. The add-in cannot override the policy. |
| Revit asks for an editable project | Open a project, exit the Family Editor, finish active commands and close modal dialogs. |
| A reference file cannot be read | Check that it still exists, is accessible, and meets the file-count/size limits. Remove and reattach it if needed. |
| A part cannot form a native solid | Read the part-specific error, then request simpler geometry or corrected dimensions/profiles. No family is loaded from the invalid preview. |
| No template is found | Install the appropriate Revit family content or select a non-hosted Generic Model `.rft` in the creation dialog. |
| A family name already exists | Choose a new name. Existing project families are preserved. |

## Privacy and local storage

API keys and account tokens are protected with Windows-user DPAPI under `%LOCALAPPDATA%\FamilyCreator\Auth`, outside the repository. The selected model ID is stored in `%LOCALAPPDATA%\FamilyCreator\model.txt`. The app does not read or reuse Codex/ChatGPT desktop credentials.

References are read when Send is pressed and included in the OpenAI request; the add-in does not persist copies of their contents locally. Requests use `store:false`, which is not a promise of zero provider retention. Review your OpenAI account's applicable data controls before submitting sensitive documents.

## Development and verification

Run from the repository root:

```powershell
dotnet build .\src\FamilyCreator.Revit -c Release
dotnet test .\tests\FamilyCreator.Auth.Tests -c Release
dotnet run --project .\tests\FamilyCreator.Ui.Smoke -c Release
```

As of 2026-10-30, the Release build has zero warnings/errors, 69 automated tests pass, and the standalone WPF harness verifies navigation, Enter submission, model-picker behavior and preview layouts. Native Revit checks passed cylinder/bent-tube geometry, unsaved family loading, final family/type names, single-type output, material colors and duplicate-name protection.

A live GPT-6.1 Sol request was attempted but rejected because the test account had no API credits. Successful live AI generation has **not** been verified. Additional category and temporary-name conflict fixtures were prepared but not completed live. See the [verification record](docs/preview-create-load.md) for the exact scope.

The `FamilyCreator.LiveCheck` and `FamilyCreator.Revit.NativeSmoke` projects are opt-in developer diagnostics, not normal installation steps. LiveCheck can make billable requests. NativeSmoke launches a separate Revit process and temporarily registers a test add-in; its manifest and test process must be cleaned up afterward.

Additional documentation:

- [Authentication and storage](docs/authentication.md)
- [Preview/create/load architecture and verification](docs/preview-create-load.md)
- [Interface design](docs/interface-design.md)

## License

[MIT](LICENSE). Autodesk Revit and its API assemblies are separate prerequisites and are not distributed with this repository. OpenAI services retain their own eligibility and usage conditions.
