# Unity docs lookup reference

Everything here is pinned to Unity **6000.3**. Never use unversioned `docs.unity3d.com/Manual/...` or `/ScriptReference/...` URLs: they currently show Unity 6.6.

## Offline docs (search here first; exact official text)

- **Location:** `C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Data\Documentation\en\`, which holds `Manual\` and `ScriptReference\`. It was installed with the Hub "Documentation" module.
- **Full-text search:** use the Grep tool (ripgrep) on `Manual\` or `ScriptReference\`. A search takes about 0.4 s.
  - `Manual\docdata\index.json` also matches most queries. Skip it and read the HTML pages instead.
- **API file names:** `ScriptReference\<Class>.<Method>.html`. Properties use `<Class>-<property>.html`.
- **Cost:** pages are HTML, so they cost more tokens than the Markdown mirror. Use this copy to find pages and to cross-check wording. Read long pages as `.md` from the web.
- **Updates:** this copy is tied to the 6000.3.2f1 Editor install. Re-add the module after an Editor upgrade.

## Engine API and Manual as Markdown (compact reading)

- **API:** `https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/<class>/<member>.md`
  - Lowercase, e.g. `.../unityengine/charactercontroller/move.md`.
  - A class page is `.../unityengine/<class>.md`.
- **Manual:** slugs are deeply nested paths, so don't guess them.
  - Grep `.claude/docs/unity-6000.3-urls.txt` for the topic, then fetch `<url>.md`.
- **Fetching:** use `curl -fsSL <url>.md` when the exact wording matters. WebFetch runs pages through a summarizer.
  - Always pass `-f`. Without it a missing page doesn't fail: docs.unity3d.com returns a generic 8 KB Manual page that looks like real content, and docs.unity.com returns the text `Not found`.
- **Links inside pages** are site-relative (`/engine/6000.3/...`). Prefix them with `https://docs.unity.com`.
- **Known glitch:** the Markdown export sometimes swaps glossary terms silently. For example, a Manual page says "Editor physics" where the real page says "Rigidbody". Cross-check any Manual sentence an answer depends on against the offline HTML (above) or the canonical web HTML.

## Canonical HTML (cross-check and tiebreaker)

- **API:** `https://docs.unity3d.com/6000.3/Documentation/ScriptReference/<Class>.<Method>.html`
  - Properties use a dash: `<Class>-<property>.html` (e.g. `CharacterController-isGrounded.html`).
- **Manual:** `https://docs.unity3d.com/6000.3/Documentation/Manual/<page>.html`

## Packages

1. **Local, exact installed version:** `Library/PackageCache/<pkg>@<hash>/Documentation~/**/*.md`, plus the package's C# source.
   - Search recursively. For example, Animation Rigging's constraint docs are in `Documentation~/constraints/`.
   - Covers Cinemachine 3.1.7, Input System 1.17.0 and Animation Rigging 1.4.1.
   - URP's docs moved into the 6000.3 Manual, so look there for URP.
2. **Web, only if the local docs don't cover it:**
   - Cinemachine: `https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/<Page>.html` and `/api/Unity.Cinemachine.<Type>.html`
   - Input System: `.../com.unity.inputsystem@1.17/manual/<Page>.html` and `/api/UnityEngine.InputSystem.<Type>.html`
   - Animation Rigging: `.../com.unity.animation.rigging@1.4/manual/<Page>.html` and `/api/UnityEngine.Animations.Rigging.<Type>.html`
   - These URLs serve the latest patch release, which can drift ahead of the installed version after Unity publishes a patch. Exact-patch URLs return 404. If the web and local docs conflict, local wins.

## Other sources

- **Signature check for this exact build:** grep `C:\Program Files\Unity\Hub\Editor\6000.3.2f1\Editor\Data\Managed\UnityEngine\*.xml` for `[MPFT]:UnityEngine.<Class>.<Member>`.
  - The prefix is `M:` for methods, `P:` for properties, `F:` for fields and `T:` for types.
  - Types in sub-namespaces look like `UnityEngine.Rendering.<Class>`.
  - These files hold one-line summaries only.
- **Managed engine source** at the exact build: `https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3.2f1/<path>`. Native `extern` methods have no source there.
- **Final IK:**
  - Read the source in `Assets/Plugins/RootMotion/FinalIK/` first.
  - Then `http://www.root-motion.com/finalikdox/html/`. It is HTTP only because the HTTPS certificate is broken, so use curl.
  - Or use Context7 `/websites/root-motion_finalikdox_html`.
  - The bundled User Manual PDF is only a list of links.

## Context7 (discovery only, then verify above)

- **OK:** `/websites/unity3d_packages_com_unity_cinemachine_3_1` and `/websites/root-motion_finalikdox_html`.
- **Unversioned, verify everything:** `/websites/unity3d_manual` and `/websites/unity3d_scriptreference`. They are not pinned to 6000.3, and some of their examples are synthesized.
- **Don't use:** `/websites/unity`, `/websites/unity_en-us`, `/llmstxt/unity_llms_txt`. They index Unity services, not the engine.
- `/unity-technologies/graphics` tracks GitHub master, which is newer than URP 17.3.

## Rebuilding the URL index

`unity-6000.3-urls.txt` is gitignored and takes about 5 seconds to rebuild. Run this in PowerShell from the repo root:

```powershell
$shards = (Invoke-WebRequest https://docs.unity.com/sitemap.xml).Content | Select-String -Pattern 'https://docs\.unity\.com/sitemap/engine/\d+\.xml' -AllMatches | ForEach-Object { $_.Matches.Value }; $urls = foreach ($s in $shards) { (Invoke-WebRequest $s).Content | Select-String -Pattern 'https://docs\.unity\.com/en-us/engine/6000\.3/[^<]+' -AllMatches | ForEach-Object { $_.Matches.Value } }; $urls | Sort-Object -Unique | Set-Content .claude\docs\unity-6000.3-urls.txt
```
