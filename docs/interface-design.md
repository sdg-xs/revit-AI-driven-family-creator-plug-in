# Interface styling adaptation

Source: a user-provided local HTML design reference, inspected on 2026-10-30. The packed page's final CSS overrides determine the styling; its initial thumbnail is not the design specification. Organization branding was subsequently removed at the user's request.

Applied to the native WPF window:

- Sidebar/header navy `#002340`; selected navigation `#476782`.
- Canvas `#F7FAFF`, white cards, borders `#D8E4F3`/`#D1D5DB`.
- Dark navy headings, secondary text `#6B7280` and blue links `#1769E0`.
- A vector cube icon replaces the organization wordmark and also serves as the window icon, matching the ribbon's model icon.
- Card radius 12, button/input radius 8, and 44-unit minimum control height.
- Poppins with Arial fallback, matching the source's final font declaration. Poppins is not installed locally; no font files were redistributed.
- Scrollable content at compact window sizes, keyboard focus outlines, and a separate amber diagnostic state.

The sidebar shows the current connection step and a non-interactive workflow description. No future product-input or generation controls are presented as working features.

The conversation now includes a model field, reference attachments, cancellation, an orbit/zoom geometry preview and a Create family dialog. Creation asks for category, family name and type name, with template selection under an expander. The dialog shares the window styles; the ComboBox template displays each control's selected item rather than assuming an account label.

Model selector redesign: replaced the composer text field with a compact model chip and chevron. The picker uses the navy header, white rounded selection cards, checked selection rings, an In use badge, searchable names/IDs and a fixed confirmation footer. Manual IDs are an advanced option. Model choices are cached for the open workspace and refreshed after Settings navigation. Standalone WPF checks cover normal/compact layout, filtering, empty results, selection preservation, manual-entry replacement and explicit confirmation.
