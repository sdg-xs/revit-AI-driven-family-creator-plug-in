# ChatGPT connection milestone

## Connection choice and API keys

The window now offers ChatGPT sign-in or an OpenAI API key. Selection is saved in the existing DPAPI-protected state file and restored when the window opens. Existing OAuth-only files remain readable. Both credentials can be retained, but the selected method is explicit; failures do not trigger automatic fallback or billing changes.

API-key mode stores a user-entered key in encrypted Windows-user storage, outside the repository. PasswordBox masks input; the field is cleared after save, method switching or window closure. Saving/replacing/removing a key preserves ChatGPT registrations. Removing the key is local only and does not revoke it remotely. API-key objects redact their value from their diagnostic string representation.

The separate Test connection action sends `GET https://api.openai.com/v1/models` with the saved key in a per-request Bearer header. Redirects are disabled. No key goes in the URL, logs, source code or error text; server error bodies are not reflected. No generation request is made. A passed check establishes models-list access only; a 403 may reflect a restricted key without that endpoint permission, so the saved key is preserved. Cancellation, network failure and rate limits also preserve it.

Current total: 44 automated tests pass. New coverage includes encrypted save/replace/removal, legacy-state migration, method persistence, OAuth preservation, missing/invalid input, exact test endpoint/header, cancellation and safe HTTP error messages. No real API key was available or requested in chat; live verification is user-driven in the Revit window.

Reference: [OpenAI models-list API](https://developers.openai.com/api/reference/resources/models/methods/list).

Audience: open-source add-in, confirmed by the user on 2026-10-30. License: MIT, explicitly selected by the user.

The implementation uses the official open-source sign-in route. It does not require an API key or embedded client secret. Availability for a particular account and permission to use its plan are determined by OpenAI's consent response; the UI distinguishes sign-in from plan permission. ChatGPT-token inference is not implemented. API-key inference is implemented separately; see preview-create-load.md.

## Implementation

- Revit's window starts system-browser authorization and a loopback listener bound only to `127.0.0.1`, on an OS-assigned port.
- Each attempt has fresh state, nonce and PKCE values. A five-minute limit, Cancel sign-in and window closure stop the wait.
- `FamilyCreator.Auth` checks callback state and registration identity, then exchanges the code. Microsoft IdentityModel validates signature, issuer, audience, expiry and signing algorithm; the app checks nonce and subject before accepting the result.
- Registration records remain separate by issued client ID. New sign-in does not replace the current connection until validated. Saved registrations can be selected for reauthorization; sign-out retains the registration but clears tokens.
- Windows DPAPI protects the entire account state for the current user. Atomic file replacement prevents partial credential writes. An exclusive file lease prevents concurrent processes from changing the account file during sign-in or sign-out.
- Sign-out attempts provider session revocation and retries a transient failure. Local tokens are cleared even when remote revocation cannot be confirmed; the UI reports that outcome.
- Credentials and authorization URLs are not logged. Authentication does not access Codex credential files, ChatGPT chats, project sources or product documents. The generation feature reads only files explicitly attached by the user when Send is pressed.

Storage: `%LOCALAPPDATA%\FamilyCreator\Auth\accounts.bin` and `session.lock`. The encrypted state includes the stable host identifier, saved registrations and tokens. Do not copy this state into source control. Copying the encrypted file to another Windows user does not transfer a working connection.

## Verified locally

Release build: zero warnings/errors. Twenty-five automated cases cover PKCE's published test vector, callback mismatch/duplicates/denial, missing or changed client IDs, signed token rejection, real loopback callback and cancellation, encrypted storage, exclusive locking, simulated exchange, granted scope interpretation, previous-account preservation, and local/remote sign-out behavior. A WPF smoke render confirms the window and sign-in controls load.

## Real-account acceptance still required

### Business sign-in denial — 2026-10-30

The user reported `3p_delegated_access_policy_denied` and confirmed the selected workspace was Business. The exact code is not explained in the official pages checked. Account/workspace eligibility or policy is the leading explanation, not a confirmed root cause. The app's authorization endpoint, requested scopes, stable UUID host identifier, callback path, resource and PKCE parameters match the documented open-source flow. No identity-only fallback, credential copying or policy bypass was introduced.

Local error handling previously discarded provider diagnostics. A failing regression test reproduced that loss; the fix retains a recognized error code and safe request ID, provides administrator/support guidance, and avoids recommending identical retries for policy denial. Token-exchange failures use the same safe handling. If the browser never redirects, the user can paste its error details in sign-in help; raw text is cleared and never logged. These tests verify error handling, not successful organization authorization.

The test suite now has 32 passing cases. Navy-themed WPF layouts were rendered at normal/minimum sizes and with the denial state. Real authorization remains unresolved pending an eligibility/policy answer from the workspace administrator or OpenAI support. Do not label this sign-in restriction fixed.

Support question to send manually: “Our open-source Revit add-in uses the documented dynamic-agent Sign in with ChatGPT flow. Selecting Business returns `3p_delegated_access_policy_denied`. Is delegated ChatGPT plan access supported for this workspace, and which administrator policy or enablement is required?” Include the request ID from the browser error when contacting support.

1. Save work and restart Revit, review the local unsigned add-in if prompted, and open Add-Ins > Family Creator.
2. Select Add a ChatGPT account, then Continue with ChatGPT. Complete authentication and consent yourself in the official browser page.
3. Confirm the account shown in Revit matches the browser account. Confirm whether plan permission was granted; do not infer model access from this status.
4. Cancel another attempt and confirm the prior connection is preserved. Close/reopen the window and confirm the saved registration remains available.
5. Sign out and confirm tokens are cleared. If remote revocation fails, follow the displayed guidance to disconnect in ChatGPT settings.

This milestone does not refresh tokens automatically or send inference requests. Expired credentials prompt another sign-in. Before inference is added, implement serialized refresh-token rotation, restricted skill runtime configuration, and a real model-access test. Live Revit load, other add-ins' dependency compatibility and real OpenAI account eligibility remain unverified.

## Official references checked 2026-10-30

- [Open-source registration and sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [Accounts, storage and sign-out](https://developers.openai.com/siwc/token-sharing-open-source/profiles-and-sessions)
- [UI guidelines](https://developers.openai.com/siwc/ui-ux-guidelines)
- [Integration eligibility](https://developers.openai.com/siwc/quickstart)
