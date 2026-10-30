# Development handoff — recovered source

Source: [ChatGPT Project Sources](https://chatgpt.com/g/g-p-6ae45e1cbd708191b5773211971f7e1b/project?tab=sources).
Recovered on 2026-10-30 from the saving chat, “Plan Revit family UI project”. That chat reports successful full readback. This copy has not been compared against the current live Project source.

Development handoff — 2026-10-30

STATUS AND SCOPE
This is the planning handoff for the existing ChatGPT Project “Revit Family Creator UI.” No Revit add-in, local integration project, installation, or end-to-end family creation has been implemented or verified in this work. The original Buddy/dot conversation has not been moved. Preserve any later user decisions over this snapshot. Continue with acknowledgement and planning only; do not begin implementation from this document alone.

USER’S REQUESTED OUTCOME
Integrate the existing revit-family-creator skill into local Revit. The desired experience is an Add-Ins ribbon button named Family Creator, opening a Revit window that offers official ChatGPT sign-in and uses only this skill. Inputs include FDVU/product specifications, manuals, images and product links; the workflow should support reviewing category, dimensions and materials, previewing geometry, and producing a reusable .rfa.

Audience is unresolved: Steven alone, other Hemy users, or a broader distribution. Sign-in feasibility and eligibility are not yet confirmed for that audience. “Only this skill” is a requested behavior, not an implemented security boundary.

RECOMMENDATIONS — NOT FINAL USER-APPROVED DESIGN
Launch from an ordinary Revit project, create geometry in a separate family document, preview and save the .rfa, then optionally load it into the active project. Support Family Editor refinement later if needed. The user asked whether to run inside a project or a family; this is the recommendation, not a completed design decision.

A possible implementation is a separate C# net10.0-windows add-in with a ribbon button, WPF dockable panel and ExternalEvent handlers for Revit operations. Revit’s installed PreviewControl documentation requires a modal host and permits only one active preview control, so native 3D preview should open in a separate modal window rather than directly inside the modeless docked panel. A simple Revit window remains consistent with the user’s initial concept; the dockable panel is a proposed option.

EXISTING SKILL AND REUSABLE BOUNDARY
Repository: https://github.com/sdg-xs/revitxgpt-family-creator-skill
Local skill: C:\Users\StevenGomba\.codex\skills\revit-family-creator\SKILL.md
Implementation guidance: C:\Users\StevenGomba\.codex\skills\revit-family-creator\references\implementation.md
Worked example: C:\Users\StevenGomba\.codex\skills\revit-family-creator\assets\airbike-family-example.dyn
Agent metadata: C:\Users\StevenGomba\.codex\skills\revit-family-creator\agents\openai.yaml

The read-only local audit on 2026-10-30 found commit 997f440 (“Record native solid verification and loft distortion guidance”) with a clean working tree. These are audit observations, not a claim that the checkout will remain unchanged. Recheck it before implementation.

The skill consists of instructions, implementation guidance and one product-specific AirBike Dynamo example, not a general application backend or an AI endpoint. The agent interprets product references; the example embeds product data and Python code. Its graph has seven connected nodes and six connectors, two directory selectors, a Boolean, result Watch and colored Watch 3D; it uses PythonNet3, Manual mode and default False. False previews without creating files; True creates a family.

Reusable concepts include shape_specs(), template lookup, make_solid(), millimeter conversion, explicit transactions, category and material assignment, one-type enforcement, measured family bounds, rollback/close behavior and preservation of existing RFAs. Product dimensions, names, categories, poses and geometry must be replaced for new products. Native Revit geometry and Dynamo preview use different engine implementations despite sharing shape descriptions, so visual equivalence must be tested.

The current skill contract targets one family and one type for Revit 2027.x, recognizable geometry and material colors, a suitable existing category, and model/dimension-based type naming. Additional physical/electrical/performance properties are included only when requested. Start from an empty non-hosted Generic Model template and verify compatibility before category assignment. Preserve existing output files.

VERIFIED LOCAL ENVIRONMENT — AUDITED 2026-10-30
• Revit 2027.3 is installed at C:\Program Files\Autodesk\Revit 2027. Revit.exe, RevitAPI.dll and RevitAPIUI.dll have file version 27.3.0.28; API assemblies report 27.3.0.0.
• Dynamo Core is 4.2.1.5887 under AddIns\DynamoForRevit; PythonNet3Engine is installed. DynamoRevitDS.dll reports 27.0.0.5893.
• .NET SDKs 10.0.204 and 10.0.402 and Windows Desktop runtime/reference packs are present. Visual Studio Community 2026 version 18.11.2 is installed; its managed-desktop workload was not positively detected. No build was run.
• Template exists: C:\ProgramData\Autodesk\RVT 2027\Family Templates\English\Metric Generic Model.rft.
• Installed RevitAPIUI.xml documents IDockablePaneProvider, RegisterDockablePane, ExternalEvent, IExternalEventHandler and PreviewControl. API paths are under the Revit 2027 installation above.
• Existing Autodesk MCP, another Revit MCP plugin, RevitLookup, analytics and USD add-ins were observed, but not exercised. The proposed native add-in need not depend on MCP.
• No separate Family Creator UI project was found in the inspected project list/Development paths; discovery was not exhaustive. Optional future local folder: C:\Users\StevenGomba\OneDrive - HEMY AS\Desktop\Development\revit-family-creator-ui. It was not created; the user’s actual request was for this ChatGPT Project.

COMPATIBILITY AND VERIFICATION LIMITS
Only the installed 2027.3 environment was inspected. Compatibility across all Revit 2027 releases is a target, not proven. Select the earliest supported API baseline and test relevant releases before claiming 2027-wide support.

The skill guidance records historical arc-order corrections: Dynamo uses start/middle/end; Revit Arc.Create uses endpoints followed by an interior point. The AirBike record reports 133 native curved-path checks, while full standalone native-solid tests crashed; final in-app Watch 3D and .rfa creation remain unverified in that record. The MicroSense investigation reports 96 passing native arc checks, a suspected housing-loft distortion, and an unverified revised construction. Do not turn these historical notes into currently reproduced defects, proven engine faults, or verified fixes.

No end-to-end native family creation was performed during the audit. An independent illustration is not native-solid validation. Future checks must inspect actual per-part solids/bounds and relevant sections, as well as overall bounds, type count, materials, template/category, preview-only behavior and existing-file protection. Read implementation.md before adapting geometry. Follow applicable local AGENTS.md instructions, including the active-session development-model preference.

AUTHENTICATION RESEARCH TO REVALIDATE BEFORE IMPLEMENTATION
Prior research identified an official Sign in with ChatGPT (SIWC) route for third-party applications. Eligibility and distribution constraints must be checked for Steven-only, internal Hemy and any commercial use before choosing it. Do not state that an API key is the sole option or promise subscription authentication for every distribution.

Use the official system-browser OAuth/consent flow; never collect ChatGPT passwords in the Revit window. Login does not automatically import this Project, its chats, or installed skills. The runtime must explicitly receive the skill and supporting resources. Restrict runtime tools/configuration and validate deterministic geometry actions; a prompt or skill alone is not security isolation. No authentication integration or credential creation has happened.

Starting references for fresh verification:
https://developers.openai.com/siwc/quickstart
https://developers.openai.com/siwc/token-sharing-open-source/sign-in
https://developers.openai.com/siwc/token-sharing-open-source/codex-app-server
https://learn.chatgpt.com/docs/app-server

POTENTIAL FIRST MILESTONE AND NEXT STEPS
A small first vertical slice could accept a local structured product recipe, review category/dimensions/materials/template/output, build deterministic validated family geometry, preview the actual native result, and save exactly one family/type without overwriting an existing .rfa. Full AI/PDF/image interpretation and broader integration would follow. This milestone is proposed, not implemented.

Next: confirm audience and authentication eligibility; inspect/extract the reusable skill boundary; agree the MVP and interaction design; then prepare an implementation plan and, once implementation is requested, build with appropriate tests and live Revit verification. Preserve the working skill and examples while developing the separate integration. Record what was actually tested and keep unresolved assumptions explicit.
