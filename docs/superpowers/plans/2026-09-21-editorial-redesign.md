# Editorial Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the noisy "engineering signal" UI with a calm, text-first editorial design without breaking any existing feature.

**Architecture:** Delete the 3051-line `assets/css/site.css` and replace it with `assets/css/site.scss` that `@use`s small focused partials in `_sass/` (Jekyll's built-in Sass converter compiles it to the same `/assets/css/site.css` URL). Simplify Liquid layouts/includes; keep every `data-*` hook that `assets/js/site.js` and `assets/js/mermaid.js` depend on. `tools/verify_site.rb` is the test suite: each task first adds failing assertions there, then implements.

**Tech Stack:** Jekyll 4.4, Liquid, Dart Sass (`sass-embedded` via `jekyll-sass-converter`, already in `Gemfile.lock`), vanilla JS (unchanged), Nokogiri-based verifier, html-proofer, headless Chrome for screenshots.

**Spec:** `docs/superpowers/specs/2026-09-21-editorial-redesign-design.md`

## Global Constraints

- Reading column `--measure: 680px`; page shell `--shell: 1080px`; 16px side gutter on mobile; no horizontal scroll at 390px.
- One accent color (blue). Track accents only as thin left borders.
- Inter for UI/body; JetBrains Mono only for code. No uppercase mono labels, no `.eyebrow`.
- Body 17px / 1.75 in articles, 16px elsewhere. Post h1 ~40px desktop / ~30px mobile.
- Both themes (`[data-theme="dark"]` set by existing toggle) and both languages (UA default, EN under `/en/`) must work.
- Do NOT rename or remove any JS hook: `data-theme-toggle`, `data-menu-toggle`, `data-mobile-menu`, `data-header`, `data-search-open`, `data-search-dialog`, `data-search-input`, `data-search-results`, `data-search-item`, `data-search-close`, `data-filter`, `data-article` (+`data-topics`), `data-no-results`, `data-article-content`, `data-toc`, `data-reading-progress`, `data-share`, `data-copy-link`, `data-copy-markdown` (+`data-markdown-url`), `data-comments-load`, `.utterances-frame`, `.article-content div.highlighter-rouge`.
- Classes emitted by JS that CSS must style: `is-open` (mobile nav), `is-active` (filter chips, TOC links, search options), `is-copied`, `toc-h2`, `toc-h3`, `code-frame`, `code-toolbar`, `code-language`, `code-copy`, `search-group`, `search-result`, `search-hint`, `mark`, `mermaid-figure`, `mermaid-viewport`, `mermaid-source`, `mermaid-status`, `has-error`.
- JS mobile-menu breakpoint is `760px` (`site.js:135`); CSS nav breakpoint must match.
- Reading progress JS sets `transform: scaleX(n)` on `[data-reading-progress]`.
- Classes the verifier requires and must survive: `.article-language-switch`, `.ui-search`, `.ui-theme`, `.path-card`, `.learning-path-callout`, `.related-articles a`.
- Out of scope: post content, `_plugins/`, `_data/`, feeds, `llms*.txt`, `assets/js/*` (no JS edits).

## Environment note

In this machine's agent shell the `bundle` zsh function recurses. Prefix commands with the rbenv shims:

```bash
export PATH="$HOME/.rbenv/shims:$PATH"
```

Fast loop (build + verifier only):

```bash
JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site
```

Full suite (build + html-proofer + verifier): `bash tools/test.sh`

Baseline: full suite passes on `main` at `d094e41` ("Verified 21 Ukrainian and 21 English articles...").

## File Map

| File | Action | Responsibility |
|---|---|---|
| `assets/css/site.css` | Delete | Legacy stylesheet |
| `assets/css/site.scss` | Create | Entry point, `@use` partials only |
| `_sass/_tokens.scss` | Create | Color/size/font custom properties, light + dark |
| `_sass/_base.scss` | Create | Reset, typography, shells, buttons, icons, page hero, section heading, track accents |
| `_sass/_chrome.scss` | Create | Header, mobile nav, footer, search dialog |
| `_sass/_code.scss` | Create | Code blocks, toolbar, Rouge palette, Mermaid |
| `_sass/_home.scss` | Create | Intro, tracks grid, filter chips, post list |
| `_sass/_article.scss` | Create | Post header, TOC layout, prose, article end, related, author, nav, comments |
| `_sass/_pages.scss` | Create | Tracks page, categories, tags, archive, simple lists, 404/offline |
| `_layouts/default.html` | Modify | Fonts, theme-color, stylesheet cache-bust |
| `_includes/header.html` | Modify | Plain-text brand, 4 nav links |
| `_includes/footer.html` | Modify | Single-row footer with Archive + RSS |
| `_layouts/home.html` | Rewrite | Intro, tracks, filtered post list, RSS line |
| `_layouts/post.html` | Rewrite header/layout/end | Meta line, TOC grid, actions at end |
| `_layouts/paths.html`, `archives.html`, `categories.html`, `category.html`, `tags.html`, `tag.html`, `page.html` | Modify | Remove eyebrows/index numbers/arrows |
| `404.html`, `offline.html` | Rewrite | Simple message pages |
| `sw.js` | Modify | Bump cache version so returning visitors get new CSS |
| `tools/verify_site.rb` | Modify | New assertions per task |
| `tools/screenshots.sh` | Create | Headless Chrome visual check helper |

---

### Task 1: Design foundation and global chrome

**Files:**
- Create: `tools/screenshots.sh`, `assets/css/site.scss`, `_sass/_tokens.scss`, `_sass/_base.scss`, `_sass/_chrome.scss`, `_sass/_code.scss`
- Delete: `assets/css/site.css` (after extracting the Rouge palette)
- Modify: `_layouts/default.html`, `_includes/header.html`, `_includes/footer.html`, `sw.js`, `tools/verify_site.rb`

**Interfaces:**
- Produces: CSS custom properties used by all later partials: `--bg`, `--bg-raised`, `--bg-subtle`, `--text`, `--text-muted`, `--border`, `--border-strong`, `--accent`, `--accent-soft`, `--inline-code-bg`, `--code-bg`, `--code-raised`, `--code-border`, `--code-text`, `--code-muted`, `--header-bg`, `--track-runtime`, `--track-architecture`, `--track-cloud`, `--track-ai`, `--track` (per-element), `--shell`, `--measure`, `--sans`, `--mono`, `--radius`, `--space-section`. Shared classes: `.shell`, `.shell-narrow`, `.button`, `.button-primary`, `.text-link`, `.icon-button`, `.page-hero`, `.back-link`, `.section-heading`, `.path-runtime|architecture|cloud|ai`.
- Produces: `tools/screenshots.sh <out-dir> [path ...]` → PNGs named `<slug>-<width>-<theme>.png`.

- [ ] **Step 1: Create the screenshot helper**

Create `tools/screenshots.sh`:

```bash
#!/usr/bin/env bash
#
# Capture headless Chrome screenshots of the built site (_site) for visual review.
#
# Usage: bash tools/screenshots.sh <out-dir> [url-path ...]

set -euo pipefail

OUT_DIR="${1:?Usage: bash tools/screenshots.sh <out-dir> [url-path ...]}"
shift
if (($# == 0)); then
  set -- / /en/ /posts/result-pattern/ /posts/cli-jit-il/ /posts/mongodb-encryption/ /paths/ /categories/ /archives/ /about/ /404.html
fi

CHROME="${CHROME:-/Applications/Google Chrome.app/Contents/MacOS/Google Chrome}"
PORT="${PORT:-4010}"

mkdir -p "$OUT_DIR"
python3 -m http.server "$PORT" -d _site >/dev/null 2>&1 &
SERVER_PID=$!
trap 'kill "$SERVER_PID"' EXIT
sleep 1

for path in "$@"; do
  name="$(echo "$path" | sed 's#^/##; s#/$##; s#[/.]#-#g')"
  name="${name:-home}"
  for width in 1440 390; do
    for theme in light dark; do
      flags=()
      [[ $theme == dark ]] && flags+=(--force-dark-mode)
      "$CHROME" --headless=new --disable-gpu --hide-scrollbars ${flags[@]+"${flags[@]}"} \
        --window-size="$width,2400" \
        --screenshot="$OUT_DIR/$name-$width-$theme.png" \
        "http://localhost:$PORT$path" >/dev/null 2>&1
    done
  done
done
echo "Screenshots saved to $OUT_DIR"
```

- [ ] **Step 2: Capture "before" screenshots from the current build**

```bash
export PATH="$HOME/.rbenv/shims:$PATH"
JEKYLL_ENV=production bundle exec jekyll b -q
bash tools/screenshots.sh "$TMPDIR/redesign/before" / /posts/result-pattern/
```

Expected: `Screenshots saved to ...`, 8 PNGs. Open `home-1440-dark.png` and `home-1440-light.png`: one must be dark, one light. If both are light, `--force-dark-mode` is ignored by this Chrome: replace the dark flag with `--blink-settings=preferredColorScheme=0` and re-run.

- [ ] **Step 3: Write the failing verifier assertions**

In `tools/verify_site.rb`, insert immediately before the line `sample_post_path = File.join(root, "posts", "result-pattern", "index.html")`:

```ruby
stylesheet_path = File.join(root, "assets", "css", "site.css")
abort "Compiled stylesheet is missing" unless File.file?(stylesheet_path)
stylesheet = File.read(stylesheet_path)
abort "Editorial design tokens are missing" unless stylesheet.include?("--measure")
abort "Legacy design tokens leaked into the stylesheet" if stylesheet.include?("--paper")
abort "Header brand mark should be removed" if home.at_css(".brand-mark")
abort "Footer archive link is missing" unless home.at_css('.site-footer a[href="/archives/"]')
abort "Footer RSS link is missing" unless home.at_css('.site-footer a[href="/feed.xml"]')
abort "English footer RSS link is missing" unless english_home.at_css('.site-footer a[href="/en/feed.xml"]')
```

- [ ] **Step 4: Run to verify it fails**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: FAIL with `Editorial design tokens are missing`

- [ ] **Step 5: Extract the Rouge palette, then delete the legacy stylesheet**

Lines 1767-1870 of the old file are the light + dark Rouge token colors (from `/* Rouge syntax palette for the light code surface. */` to `[data-theme="dark"] .highlight .gu { color: #d2a8ff; }`). Confirm the boundaries, then move them:

```bash
sed -n '1767p;1870p' assets/css/site.css   # expect the comment line, then the .gu line
sed -n '1767,1870p' assets/css/site.css > "$TMPDIR/rouge-palette.css"
git rm -q assets/css/site.css
```

- [ ] **Step 6: Create `_sass/_tokens.scss`**

```scss
:root {
  color-scheme: light;
  --bg: #fbfaf7;
  --bg-raised: #ffffff;
  --bg-subtle: #f2f0ea;
  --text: #1b1b19;
  --text-muted: #5c5b55;
  --border: #e4e1d9;
  --border-strong: #cfcbc1;
  --accent: #2451e6;
  --accent-soft: #e9eefd;
  --inline-code-bg: #efede6;
  --code-bg: #f7f8fa;
  --code-raised: #ffffff;
  --code-border: #e1e4e8;
  --code-text: #24292f;
  --code-muted: #6e7781;
  --header-bg: rgba(251, 250, 247, 0.86);
  --track-runtime: #6b5cf6;
  --track-architecture: #0e9f6e;
  --track-cloud: #d97706;
  --track-ai: #db2777;
  --shell: min(1080px, calc(100vw - 48px));
  --measure: min(680px, calc(100vw - 48px));
  --sans: "Inter", ui-sans-serif, -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
  --mono: "JetBrains Mono", ui-monospace, "SFMono-Regular", Consolas, "Liberation Mono", monospace;
  --radius: 8px;
  --space-section: clamp(56px, 8vw, 96px);
}

[data-theme="dark"] {
  color-scheme: dark;
  --bg: #111110;
  --bg-raised: #181817;
  --bg-subtle: #1f1f1d;
  --text: #ecebe6;
  --text-muted: #a3a199;
  --border: #2a2a27;
  --border-strong: #3d3d39;
  --accent: #7d9bff;
  --accent-soft: #1d2645;
  --inline-code-bg: #252523;
  --code-bg: #0d1117;
  --code-raised: #161b22;
  --code-border: #30363d;
  --code-text: #e6edf3;
  --code-muted: #8b949e;
  --header-bg: rgba(17, 17, 16, 0.86);
}

@media (max-width: 640px) {
  :root {
    --shell: calc(100vw - 32px);
    --measure: calc(100vw - 32px);
  }
}
```

- [ ] **Step 7: Create `_sass/_base.scss`**

```scss
*,
*::before,
*::after {
  box-sizing: border-box;
}

html {
  -webkit-text-size-adjust: 100%;
  scroll-behavior: smooth;
  scroll-padding-top: 88px;
}

body {
  background: var(--bg);
  color: var(--text);
  font-family: var(--sans);
  font-size: 16px;
  line-height: 1.6;
  margin: 0;
  text-rendering: optimizeLegibility;
  -webkit-font-smoothing: antialiased;
}

body.search-open,
body.menu-open {
  overflow: hidden;
}

a {
  color: inherit;
  text-decoration: none;
}

button,
input {
  color: inherit;
  font: inherit;
}

button {
  cursor: pointer;
}

img {
  display: block;
  height: auto;
  max-width: 100%;
}

h1,
h2,
h3 {
  letter-spacing: -0.02em;
  line-height: 1.2;
  text-wrap: balance;
}

::selection {
  background: var(--accent-soft);
}

:focus-visible {
  border-radius: 4px;
  outline: 2px solid var(--accent);
  outline-offset: 2px;
}

@media (prefers-reduced-motion: reduce) {
  html {
    scroll-behavior: auto;
  }

  *,
  *::before,
  *::after {
    animation: none !important;
    transition: none !important;
  }
}

.shell {
  margin-inline: auto;
  width: var(--shell);
}

.shell-narrow {
  margin-inline: auto;
  width: var(--measure);
}

.sr-only {
  clip: rect(0, 0, 0, 0);
  clip-path: inset(50%);
  height: 1px;
  overflow: hidden;
  position: absolute;
  white-space: nowrap;
  width: 1px;
}

.skip-link {
  background: var(--accent);
  border-radius: var(--radius);
  color: #fff;
  left: 16px;
  padding: 10px 16px;
  position: fixed;
  top: -60px;
  transition: top 0.2s;
  z-index: 1000;
}

.skip-link:focus {
  top: 16px;
}

.text-link {
  color: var(--accent);
  font-size: 15px;
  font-weight: 500;
}

.text-link:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}

.button {
  align-items: center;
  background: var(--bg-raised);
  border: 1px solid var(--border-strong);
  border-radius: var(--radius);
  display: inline-flex;
  font-size: 15px;
  font-weight: 500;
  gap: 8px;
  min-height: 40px;
  padding: 0 16px;
}

.button:hover {
  border-color: var(--text-muted);
}

.button-primary {
  background: var(--text);
  border-color: var(--text);
  color: var(--bg);
}

.button-primary:hover {
  border-color: var(--text);
  opacity: 0.88;
}

.icon-button {
  align-items: center;
  background: transparent;
  border: 0;
  border-radius: 6px;
  color: var(--text-muted);
  display: inline-flex;
  gap: 8px;
  height: 36px;
  justify-content: center;
  min-width: 36px;
  padding: 0 8px;
}

.icon-button:hover {
  background: var(--bg-subtle);
  color: var(--text);
}

.icon-button.is-copied {
  color: var(--accent);
}

.page-hero {
  padding: clamp(48px, 7vw, 80px) 0 32px;
}

.page-hero h1 {
  font-size: clamp(32px, 4.5vw, 44px);
  font-weight: 700;
  margin: 0 0 12px;
}

.page-hero p {
  color: var(--text-muted);
  font-size: 18px;
  margin: 0;
  max-width: 620px;
}

.back-link {
  color: var(--text-muted);
  font-size: 15px;
}

.back-link:hover {
  color: var(--text);
}

.section-heading {
  align-items: baseline;
  display: flex;
  gap: 16px;
  justify-content: space-between;
  margin-bottom: 20px;
}

.section-heading h2 {
  font-size: 22px;
  font-weight: 650;
  margin: 0;
}

.section-heading p {
  color: var(--text-muted);
  font-size: 15px;
  margin: 0;
}

.path-runtime { --track: var(--track-runtime); }
.path-architecture { --track: var(--track-architecture); }
.path-cloud { --track: var(--track-cloud); }
.path-ai { --track: var(--track-ai); }

/* CSS-drawn interface icons (verifier requires .ui-search and .ui-theme). */
.ui-icon {
  display: inline-block;
  flex: 0 0 auto;
  height: 18px;
  position: relative;
  width: 18px;
}

.ui-search::before {
  border: 1.7px solid currentColor;
  border-radius: 50%;
  content: "";
  height: 9px;
  left: 1px;
  position: absolute;
  top: 1px;
  width: 9px;
}

.ui-search::after {
  background: currentColor;
  content: "";
  height: 1.7px;
  left: 11px;
  position: absolute;
  top: 12px;
  transform: rotate(45deg);
  transform-origin: left center;
  width: 6px;
}

.ui-theme {
  border: 1.7px solid currentColor;
  border-radius: 50%;
  overflow: hidden;
}

.ui-theme::after {
  background: currentColor;
  content: "";
  height: 100%;
  position: absolute;
  right: 0;
  top: 0;
  width: 50%;
}

.ui-link::before,
.ui-link::after {
  border: 1.7px solid currentColor;
  border-radius: 999px;
  content: "";
  height: 6px;
  position: absolute;
  transform: rotate(-42deg);
  width: 11px;
}

.ui-link::before {
  left: 0;
  top: 8px;
}

.ui-link::after {
  right: 0;
  top: 3px;
}
```

- [ ] **Step 8: Create `_sass/_chrome.scss`**

```scss
/* Header */
.site-header {
  -webkit-backdrop-filter: saturate(180%) blur(12px);
  backdrop-filter: saturate(180%) blur(12px);
  background: var(--header-bg);
  border-bottom: 1px solid var(--border);
  position: sticky;
  top: 0;
  z-index: 50;
}

.header-inner {
  align-items: center;
  display: flex;
  gap: 24px;
  height: 60px;
}

.brand {
  font-size: 16px;
  font-weight: 650;
  letter-spacing: -0.01em;
  margin-right: auto;
}

.desktop-nav {
  display: flex;
  gap: 2px;
}

.desktop-nav a {
  border-radius: 6px;
  color: var(--text-muted);
  font-size: 15px;
  font-weight: 500;
  padding: 6px 10px;
}

.desktop-nav a:hover {
  background: var(--bg-subtle);
  color: var(--text);
}

.desktop-nav a[aria-current="page"] {
  color: var(--text);
}

.header-actions {
  align-items: center;
  display: flex;
  gap: 2px;
}

.language-switch {
  align-items: center;
  border-radius: 6px;
  color: var(--text-muted);
  display: inline-flex;
  font-size: 13px;
  font-weight: 600;
  height: 36px;
  justify-content: center;
  min-width: 36px;
  padding: 0 8px;
}

.language-switch:hover {
  background: var(--bg-subtle);
  color: var(--text);
}

.search-trigger kbd {
  border: 1px solid var(--border);
  border-radius: 4px;
  color: var(--text-muted);
  font: 500 11px/1 var(--mono);
  padding: 3px 5px;
}

.menu-toggle {
  background: transparent;
  border: 0;
  border-radius: 6px;
  color: var(--text);
  display: none;
  flex-direction: column;
  gap: 5px;
  height: 36px;
  justify-content: center;
  padding: 0 9px;
  width: 36px;
}

.menu-toggle > span:not(.sr-only) {
  background: currentColor;
  border-radius: 2px;
  display: block;
  height: 1.5px;
  transition: transform 0.2s;
}

.menu-toggle[aria-expanded="true"] > span:nth-child(1) {
  transform: translateY(3.25px) rotate(45deg);
}

.menu-toggle[aria-expanded="true"] > span:nth-child(2) {
  transform: translateY(-3.25px) rotate(-45deg);
}

.mobile-nav {
  display: none;
}

@media (max-width: 760px) {
  .desktop-nav,
  .search-trigger kbd {
    display: none;
  }

  .menu-toggle {
    display: inline-flex;
  }

  .mobile-nav.is-open {
    display: flex;
    flex-direction: column;
    padding: 4px 0 16px;
  }

  .mobile-nav a {
    border-top: 1px solid var(--border);
    font-size: 17px;
    font-weight: 500;
    padding: 12px 0;
  }
}

/* Footer */
.site-footer {
  border-top: 1px solid var(--border);
  color: var(--text-muted);
  font-size: 14px;
  padding: 28px 0 40px;
}

.footer-inner {
  align-items: center;
  display: flex;
  flex-wrap: wrap;
  gap: 12px 24px;
  justify-content: space-between;
}

.footer-inner nav {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 20px;
}

.footer-inner a:hover {
  color: var(--text);
}

/* Search dialog (markup in _includes/search.html, results rendered by site.js) */
.search-dialog {
  align-items: flex-start;
  display: flex;
  inset: 0;
  justify-content: center;
  padding: 12vh 16px 16px;
  position: fixed;
  z-index: 100;
}

.search-dialog[hidden] {
  display: none;
}

.search-backdrop {
  background: rgba(10, 10, 9, 0.45);
  border: 0;
  inset: 0;
  position: absolute;
}

.search-panel {
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-radius: 12px;
  box-shadow: 0 24px 64px rgba(0, 0, 0, 0.22);
  display: flex;
  flex-direction: column;
  max-height: 72vh;
  overflow: hidden;
  position: relative;
  width: min(640px, 100%);
}

.search-field {
  align-items: center;
  border-bottom: 1px solid var(--border);
  color: var(--text-muted);
  display: flex;
  gap: 12px;
  padding: 0 12px 0 16px;
}

.search-field input {
  background: transparent;
  border: 0;
  color: var(--text);
  flex: 1;
  font-size: 16px;
  height: 52px;
  min-width: 0;
  outline: none;
}

.search-field button {
  background: transparent;
  border: 1px solid var(--border);
  border-radius: 4px;
  color: var(--text-muted);
  font: 500 11px/1 var(--mono);
  padding: 4px 6px;
}

.search-results {
  overflow-y: auto;
  padding: 8px;
}

.search-hint {
  color: var(--text-muted);
  font-size: 14px;
  margin: 0;
  padding: 16px 12px;
}

.search-group > span {
  color: var(--text-muted);
  display: block;
  font-size: 12px;
  font-weight: 600;
  padding: 10px 12px 6px;
}

.search-group a,
.search-group button,
.search-result {
  background: transparent;
  border: 0;
  border-radius: 8px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 10px 12px;
  text-align: left;
  width: 100%;
}

.search-group a:hover,
.search-group button:hover,
.search-result:hover,
.search-results .is-active {
  background: var(--bg-subtle);
}

.search-group strong,
.search-result strong {
  font-size: 15px;
  font-weight: 600;
}

.search-group small,
.search-result span {
  color: var(--text-muted);
  font-size: 13px;
}

.search-result p {
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  color: var(--text-muted);
  display: -webkit-box;
  font-size: 14px;
  margin: 2px 0 0;
  overflow: hidden;
}

.search-results mark {
  background: var(--accent-soft);
  border-radius: 2px;
  color: inherit;
}

.search-shortcuts {
  border-top: 1px solid var(--border);
  color: var(--text-muted);
  display: flex;
  font-size: 12px;
  gap: 16px;
  padding: 10px 16px;
}

.search-shortcuts kbd {
  border: 1px solid var(--border);
  border-radius: 4px;
  font: 500 11px/1 var(--mono);
  margin-right: 2px;
  padding: 2px 4px;
}

@media (max-width: 640px) {
  .search-dialog {
    padding-top: 16px;
  }

  .search-shortcuts {
    display: none;
  }
}
```

- [ ] **Step 9: Create `_sass/_code.scss`**

Write the new rules first, then append the extracted palette:

```scss
/* Inline code */
.article-content :not(pre) > code,
.prose-page :not(pre) > code {
  background: var(--inline-code-bg);
  border-radius: 4px;
  color: var(--text);
  font-family: var(--mono);
  font-size: 0.85em;
  padding: 0.15em 0.35em;
}

.article-content a code,
.prose-page a code {
  color: inherit;
}

/* Code blocks (before site.js wraps them in .code-frame, and for no-JS) */
.article-content .highlight,
.article-content pre,
.prose-page .highlight,
.prose-page pre {
  background: var(--code-bg);
  border: 1px solid var(--code-border);
  border-radius: var(--radius);
  color: var(--code-text);
  font-family: var(--mono);
  font-size: 13.5px;
  line-height: 1.65;
  margin: 28px 0;
  overflow: auto;
  padding: 18px 20px;
}

.article-content .highlight pre,
.prose-page .highlight pre {
  background: transparent;
  border: 0;
  margin: 0;
  overflow: visible;
  padding: 0;
}

.article-content div.highlighter-rouge {
  position: relative;
}

.article-content .code-frame {
  background: var(--code-bg);
  border: 1px solid var(--code-border);
  border-radius: var(--radius);
  margin: 28px 0;
  overflow: hidden;
}

.article-content .code-frame > .highlight,
.article-content .code-frame > pre {
  background: transparent;
  border: 0;
  border-radius: 0;
  margin: 0;
  padding: 0;
}

.article-content .code-frame > .highlight > pre,
.article-content .code-frame > pre {
  max-height: min(720px, 75vh);
  overflow: auto;
  padding: 16px 20px 18px;
  scrollbar-color: var(--code-muted) transparent;
  scrollbar-width: thin;
}

.code-toolbar {
  align-items: center;
  background: var(--code-raised);
  border-bottom: 1px solid var(--code-border);
  display: flex;
  justify-content: space-between;
  min-height: 38px;
  padding: 0 8px 0 16px;
}

.code-language {
  color: var(--code-muted);
  font: 500 12px/1 var(--mono);
}

.code-copy {
  background: transparent;
  border: 0;
  border-radius: 5px;
  color: var(--code-muted);
  font: 500 12px/1 var(--sans);
  padding: 7px 9px;
}

.code-copy:hover,
.code-copy:focus-visible {
  background: color-mix(in srgb, var(--code-muted) 16%, transparent);
  color: var(--code-text);
}

.article-content .rouge-table {
  border: 0;
  display: table;
  font-size: inherit;
  margin: 0;
  overflow: visible;
  width: 100%;
}

.article-content .rouge-table td {
  border: 0;
  padding: 0;
}

.article-content .rouge-table .rouge-gutter {
  border-right: 1px solid var(--code-border);
  color: var(--code-muted);
  padding-right: 14px;
  text-align: right;
  user-select: none;
  width: 1%;
}

.article-content .rouge-table .rouge-code {
  padding-left: 16px;
  width: 99%;
}

/* Mermaid (figure built by assets/js/mermaid.js) */
.mermaid-figure {
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  margin: 32px 0;
  overflow: hidden;
}

.mermaid-viewport {
  align-items: center;
  background: var(--bg-raised);
  display: flex;
  justify-content: center;
  min-height: 220px;
  overflow: auto;
  padding: 24px;
}

.mermaid-viewport svg {
  height: auto;
  max-width: 100%;
}

.mermaid-viewport:fullscreen {
  background: var(--bg);
  padding: clamp(24px, 5vw, 80px);
}

.mermaid-viewport:fullscreen svg {
  max-height: 100%;
  max-width: 100%;
}

.mermaid-status {
  color: var(--text-muted);
  font-size: 14px;
  margin: 0;
  text-align: center;
}

.mermaid-figure.has-error {
  border-color: #c55;
}

.mermaid-source {
  border-top: 1px solid var(--border);
}

.mermaid-source summary {
  color: var(--text-muted);
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  padding: 12px 16px;
}

.article-content .mermaid-source pre {
  border: 0;
  border-radius: 0;
  margin: 0;
  max-height: 360px;
}

@media (max-width: 640px) {
  .article-content .code-frame,
  .article-content .mermaid-figure {
    border-left: 0;
    border-radius: 0;
    border-right: 0;
    margin-left: -16px;
    margin-right: -16px;
  }

  .article-content .code-frame > .highlight > pre,
  .article-content .code-frame > pre {
    padding: 16px;
  }

  .mermaid-viewport {
    justify-content: flex-start;
    padding: 20px 16px;
  }
}

```

Then append the palette unchanged:

```bash
cat "$TMPDIR/rouge-palette.css" >> _sass/_code.scss
```

- [ ] **Step 10: Create `assets/css/site.scss`**

Empty front matter is required for Jekyll to compile it.

```scss
---
---

@use "tokens";
@use "base";
@use "chrome";
@use "code";
```

- [ ] **Step 11: Update `_layouts/default.html` head**

Replace:
```html
    <meta name="theme-color" content="#f3f0e8">
```
with:
```html
    <meta name="theme-color" content="#fbfaf7" media="(prefers-color-scheme: light)">
    <meta name="theme-color" content="#111110" media="(prefers-color-scheme: dark)">
```

Replace:
```html
    <link rel="stylesheet" href="{{ '/assets/css/site.css' | relative_url }}">
```
with:
```html
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap">
    <link rel="stylesheet" href="{{ '/assets/css/site.css' | relative_url }}?v={{ site.time | date: '%s' }}">
```

- [ ] **Step 12: Update `_includes/header.html`**

Replace the brand anchor:
```html
    <a class="brand" href="{{ home_url | relative_url }}">
      <span class="brand-mark" aria-hidden="true">TK</span>
      <span class="brand-copy">
        <strong>Taras Kovalenko</strong>
        <span>Software engineer</span>
      </span>
    </a>
```
with:
```html
    <a class="brand" href="{{ home_url | relative_url }}">Taras Kovalenko</a>
```

In `desktop-nav`, delete the Archive link line:
```html
      <a href="{{ '/archives/' | relative_url }}" {% if page.url == '/archives/' %}aria-current="page"{% endif %}>{% if is_en %}Archive{% else %}Архів{% endif %}</a>
```
Keep the Archive link in `mobile-nav` unchanged. Nothing else in the header changes: the search and theme buttons already carry `icon-button` (styled in `_base.scss`), and all data attributes stay.

- [ ] **Step 13: Replace `_includes/footer.html`**

```html
{% assign footer_feed_url = '/feed.xml' %}
{% if is_en %}{% assign footer_feed_url = '/en/feed.xml' %}{% endif %}
<footer class="site-footer">
  <div class="shell footer-inner">
    <span>© {{ 'now' | date: '%Y' }} Taras Kovalenko</span>
    <nav aria-label="{% if is_en %}Footer navigation{% else %}Навігація у футері{% endif %}">
      <a href="{{ '/archives/' | relative_url }}">{% if is_en %}Archive{% else %}Архів{% endif %}</a>
      <a href="{{ footer_feed_url | relative_url }}">RSS</a>
      <a href="https://github.com/TarasKovalenko" target="_blank" rel="noopener">GitHub</a>
      <a href="https://www.linkedin.com/in/taras-kovalenko" target="_blank" rel="noopener">LinkedIn</a>
      <a href="https://bsky.app/profile/tkovalenko.bsky.social" target="_blank" rel="noopener">Bluesky</a>
    </nav>
  </div>
</footer>
```

- [ ] **Step 14: Bump the service worker cache**

In `sw.js` change `const CACHE_VERSION = "tk-notes-v6";` to `const CACHE_VERSION = "tk-notes-v7";`.

- [ ] **Step 15: Run to verify it passes**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: PASS, ends with `Verified 21 Ukrainian and 21 English articles, ...`. Sass must print no errors (deprecation warnings from `@import` must not appear; we only use `@use`).

- [ ] **Step 16: Visual check of chrome**

```bash
bash tools/screenshots.sh "$TMPDIR/redesign/task1" / /posts/result-pattern/
```
Open the 4 home PNGs. Expected: header shows plain "Taras Kovalenko" left, 4 nav links, search/EN/theme right; at 390px only brand + icons + hamburger; footer is one row. Page bodies look unstyled - that is expected until Tasks 2-4.

- [ ] **Step 17: Commit**

```bash
git add tools/screenshots.sh tools/verify_site.rb assets/css/site.scss _sass _layouts/default.html _includes/header.html _includes/footer.html sw.js
git commit -m "feat(ui): editorial design tokens, header, footer and code styles

Replace the legacy 3k-line stylesheet with Sass partials.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Home page

**Files:**
- Rewrite: `_layouts/home.html`
- Create: `_sass/_home.scss`
- Modify: `assets/css/site.scss`, `tools/verify_site.rb`

**Interfaces:**
- Consumes: tokens, `.section-heading`, `.text-link`, `.path-*` accents (Task 1).
- Produces: `.track-link` (also referenced nowhere else), `.post-item` list markup with `data-article` + `data-topics`, `.filter-bar`/`.filter-chip` with `data-filter`, `[data-no-results]`.

- [ ] **Step 1: Write the failing verifier assertions**

In `tools/verify_site.rb`, insert directly after the line `abort "English search index is incomplete" unless ...`:

```ruby
abort "Decorative home sections should be removed" if home.at_css(".signal-card, .topic-ticker, .generated-cover, .newsletter")
abort "Home post list is incomplete" unless home.css(".post-list > .post-item[data-article]").size == post_sources.size
abort "Home track links are missing" unless home.css(".tracks-grid > .track-link").size == 4
abort "Article filters are missing" unless home.css(".filter-bar [data-filter]").size == 5
image_posts = post_sources.count { |source| File.read(source).match?(/^image:\s*$/) }
thumbnail_count = home.css(".post-item-thumb img").size
abort "Expected #{image_posts} post thumbnails, found #{thumbnail_count}" unless thumbnail_count == image_posts
```

- [ ] **Step 2: Run to verify it fails**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: FAIL with `Decorative home sections should be removed`

- [ ] **Step 3: Rewrite `_layouts/home.html`**

Copy text is unchanged from the current page; only structure changes.

```liquid
---
layout: default
---
{% assign is_en = false %}
{% if page.lang == 'en' %}{% assign is_en = true %}{% endif %}
{% assign home_posts = site.posts %}
{% assign home_paths = site.data.learning_paths %}
{% assign home_paths_url = '/paths/' %}
{% assign home_feed_url = '/feed.xml' %}
{% if is_en %}
  {% assign home_posts = site.posts_en | sort: 'date' | reverse %}
  {% assign home_paths = site.data.learning_paths_en %}
  {% assign home_paths_url = '/en/paths/' %}
  {% assign home_feed_url = '/en/feed.xml' %}
{% endif %}
<section class="home-intro shell">
  <h1>{% if is_en %}Deconstruct complexity. Build what works.{% else %}Розбираю складне. Будую практичне.{% endif %}</h1>
  <p>{% if is_en %}Notes on .NET, system design, cloud platforms, and AI engineering - with code, trade-offs, and no noise.{% else %}Нотатки про .NET, системний дизайн, хмарні платформи та AI-інженерію - без шуму, з кодом і висновками.{% endif %}</p>
</section>

<section class="home-section shell" aria-labelledby="tracks-title">
  <header class="section-heading">
    <h2 id="tracks-title">{% if is_en %}Topic tracks{% else %}Тематичні треки{% endif %}</h2>
    <a class="text-link" href="{{ home_paths_url | relative_url }}">{% if is_en %}All tracks{% else %}Усі треки{% endif %} →</a>
  </header>
  <div class="tracks-grid">
    {% for path in home_paths %}
      <a class="track-link path-{{ path.accent }}" href="{{ home_paths_url | append: '#' | append: path.id | relative_url }}">
        <h3>{{ path.title }}</h3>
        <p>{{ path.description }}</p>
        <span>{{ path.level }} · {{ path.count }} {% if is_en %}{% if path.count == 1 %}article{% else %}articles{% endif %}{% else %}{% if path.count == 1 %}матеріал{% elsif path.count < 5 %}матеріали{% else %}матеріалів{% endif %}{% endif %}</span>
      </a>
    {% endfor %}
  </div>
</section>

<section class="home-section shell" id="latest" aria-labelledby="latest-title">
  <header class="section-heading">
    <h2 id="latest-title">{% if is_en %}Latest articles{% else %}Останні публікації{% endif %}</h2>
  </header>

  <div class="filter-bar" role="group" aria-label="{% if is_en %}Article filters{% else %}Фільтр статей{% endif %}">
    <button class="filter-chip is-active" type="button" aria-pressed="true" data-filter="all">{% if is_en %}All{% else %}Усі{% endif %} <sup>{{ home_posts.size }}</sup></button>
    <button class="filter-chip" type="button" aria-pressed="false" data-filter=".net">.NET</button>
    <button class="filter-chip" type="button" aria-pressed="false" data-filter="ai">AI</button>
    <button class="filter-chip" type="button" aria-pressed="false" data-filter="architecture">{% if is_en %}Architecture{% else %}Архітектура{% endif %}</button>
    <button class="filter-chip" type="button" aria-pressed="false" data-filter="cloud">Cloud</button>
  </div>

  <div class="post-list" data-articles-grid>
    {% for post in home_posts %}
      {% assign reading_minutes = post.content | number_of_words | divided_by: 200 | plus: 1 %}
      <article class="post-item"
               data-article
               data-topics="{{ post.categories | join: ' ' | downcase }} {{ post.tags | join: ' ' | downcase }}">
        <div class="post-item-body">
          <div class="post-item-meta">
            <span>{{ post.categories | first }}</span>
            <span aria-hidden="true">·</span>
            <time datetime="{{ post.date | date_to_xmlschema }}">{{ post.date | date: "%d.%m.%Y" }}</time>
            <span aria-hidden="true">·</span>
            <span>{{ reading_minutes }} {% if is_en %}min read{% else %}хв читання{% endif %}</span>
          </div>
          <h3><a href="{{ post.url | relative_url }}">{{ post.title }}</a></h3>
          <p>{{ post.excerpt | strip_html | strip_newlines | truncate: 200 }}</p>
        </div>
        {% if post.image.path %}
          <a class="post-item-thumb" href="{{ post.url | relative_url }}" tabindex="-1" aria-hidden="true">
            <img src="{{ post.image.path | relative_url }}" alt="" width="320" height="200" loading="lazy" decoding="async">
          </a>
        {% endif %}
      </article>
    {% endfor %}
  </div>
  <p class="no-results" hidden data-no-results>{% if is_en %}No articles match this filter yet.{% else %}За цим фільтром статей поки немає.{% endif %}</p>
</section>

<section class="home-rss shell">
  <p>{% if is_en %}New deep dives into architecture, .NET, and AI - in your feed reader without algorithms or spam.{% else %}Нові розбори архітектури, .NET і AI - у вашому feed reader без алгоритмів та спаму.{% endif %}
    <a class="text-link" href="{{ home_feed_url | relative_url }}">{% if is_en %}Subscribe via RSS{% else %}Підписатися на RSS{% endif %} →</a></p>
</section>
```

- [ ] **Step 4: Create `_sass/_home.scss`**

```scss
.home-intro {
  padding: clamp(56px, 9vw, 104px) 0 clamp(40px, 6vw, 64px);
}

.home-intro h1 {
  font-size: clamp(34px, 5vw, 52px);
  font-weight: 700;
  letter-spacing: -0.03em;
  margin: 0 0 16px;
  max-width: 18ch;
}

.home-intro p {
  color: var(--text-muted);
  font-size: clamp(17px, 2vw, 19px);
  margin: 0;
  max-width: 600px;
}

.home-section {
  padding-bottom: var(--space-section);
}

.tracks-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.track-link {
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-left: 3px solid var(--track, var(--accent));
  border-radius: var(--radius);
  display: block;
  padding: 18px 20px;
  transition: border-color 0.15s;
}

.track-link:hover {
  border-bottom-color: var(--border-strong);
  border-right-color: var(--border-strong);
  border-top-color: var(--border-strong);
}

.track-link h3 {
  font-size: 17px;
  font-weight: 600;
  margin: 0 0 4px;
}

.track-link p {
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  color: var(--text-muted);
  display: -webkit-box;
  font-size: 15px;
  line-height: 1.5;
  margin: 0 0 10px;
  overflow: hidden;
}

.track-link span {
  color: var(--text-muted);
  font-size: 13px;
}

.filter-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
}

.filter-chip {
  background: transparent;
  border: 1px solid var(--border);
  border-radius: 999px;
  color: var(--text-muted);
  font-size: 14px;
  font-weight: 500;
  height: 32px;
  padding: 0 12px;
}

.filter-chip:hover {
  border-color: var(--border-strong);
  color: var(--text);
}

.filter-chip.is-active {
  background: var(--text);
  border-color: var(--text);
  color: var(--bg);
}

.filter-chip sup {
  font-size: 12px;
  margin-left: 2px;
  opacity: 0.7;
  vertical-align: baseline;
}

.post-item {
  align-items: start;
  border-bottom: 1px solid var(--border);
  display: grid;
  gap: 24px;
  grid-template-columns: minmax(0, 1fr) auto;
  padding: 28px 0;
}

/* site.js toggles [hidden]; display:grid would otherwise win. */
.post-item[hidden] {
  display: none;
}

.post-item-meta {
  color: var(--text-muted);
  display: flex;
  flex-wrap: wrap;
  font-size: 14px;
  gap: 4px 8px;
  margin-bottom: 6px;
}

.post-item h3 {
  font-size: clamp(19px, 2.2vw, 22px);
  font-weight: 650;
  line-height: 1.3;
  margin: 0 0 8px;
}

.post-item h3 a:hover {
  color: var(--accent);
}

.post-item p {
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  color: var(--text-muted);
  display: -webkit-box;
  margin: 0;
  overflow: hidden;
}

.post-item-thumb {
  aspect-ratio: 16 / 10;
  background: var(--bg-subtle);
  border: 1px solid var(--border);
  border-radius: 6px;
  overflow: hidden;
  width: 160px;
}

.post-item-thumb img {
  height: 100%;
  object-fit: cover;
  width: 100%;
}

.no-results {
  color: var(--text-muted);
  padding: 32px 0;
}

.home-rss {
  border-top: 1px solid var(--border);
  color: var(--text-muted);
  padding: 32px 0 var(--space-section);
}

.home-rss p {
  margin: 0;
  max-width: 640px;
}

@media (max-width: 640px) {
  .tracks-grid {
    grid-template-columns: 1fr;
  }

  .post-item {
    gap: 16px;
    padding: 22px 0;
  }

  .post-item-thumb {
    aspect-ratio: 1;
    width: 72px;
  }
}
```

- [ ] **Step 5: Register the partial**

Append to `assets/css/site.scss`:
```scss
@use "home";
```

- [ ] **Step 6: Run to verify it passes**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: PASS.

- [ ] **Step 7: Visual + behavior check**

```bash
bash tools/screenshots.sh "$TMPDIR/redesign/task2" / /en/
```
Expected in all 8 PNGs: intro heading in one color, 2x2 tracks with colored left borders (1 column at 390), chips row, a single-column list with thin dividers, thumbnails only on the 10 posts that have images, no horizontal overflow at 390.

Then in a browser (`python3 -m http.server 4010 -d _site`, open `http://localhost:4010/`): click "AI" chip - only AI posts remain, chip is filled; click "All" - all 21 return.

- [ ] **Step 8: Commit**

```bash
git add _layouts/home.html _sass/_home.scss assets/css/site.scss tools/verify_site.rb
git commit -m "feat(ui): calm editorial home page with post list

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Post page

**Files:**
- Modify: `_layouts/post.html` (header, layout, share aside, article footer eyebrows)
- Create: `_sass/_article.scss`
- Modify: `assets/css/site.scss`, `tools/verify_site.rb`

**Interfaces:**
- Consumes: tokens, `.icon-button`, `.ui-link`, `.button` (Task 1); code styles from `_code.scss`.
- Produces: `.prose-page` prose rules shared with `page.html` (Task 4 relies on `.prose-page` being styled here).

Deviation from spec, approved as part of this plan: the TOC is hidden below **1200px** (spec said 1100px) because at 1100px each side column is only ~170px, which wraps TOC items to 3 lines. Step 7 amends the spec.

- [ ] **Step 1: Write the failing verifier assertions**

In `tools/verify_site.rb`:

Inside the `post_sources.each` loop, directly after `abort "English article switcher is missing: #{slug}" unless en_document.at_css(".article-language-switch")`, add:
```ruby
  abort "English article switcher should sit in the meta line: #{slug}" unless en_document.at_css(".article-meta .article-language-switch")
```

Replace these two lines:
```ruby
abort "Article context metadata is missing" if sample_post.css(".article-facts > div").size < 4
abort "Markdown article tools are missing" unless sample_post.at_css("[data-copy-markdown][data-markdown-url]")
```
with:
```ruby
abort "Crowded article header elements should be removed" if sample_post.at_css(".article-facts, .article-byline, .breadcrumbs, .article-share")
abort "Article meta line is missing its date" unless sample_post.at_css(".article-meta time[datetime]")
abort "Article meta line is missing its level" unless sample_post.at_css(".article-meta [data-level]")&.text&.strip == "Intermediate"
abort "Article scope tags are missing" unless sample_post.css(".article-scope li").map { |item| item.text.strip } == [".NET", "error handling"]
abort "Article share actions are missing" unless sample_post.at_css(".article-end [data-share]") && sample_post.at_css(".article-end [data-copy-link]")
abort "Markdown article tools are missing" unless sample_post.at_css(".article-end .article-tools [data-copy-markdown][data-markdown-url]")
abort "Table of contents container is missing" unless sample_post.at_css(".article-toc [data-toc]")
```

- [ ] **Step 2: Run to verify it fails**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: FAIL with `English article switcher should sit in the meta line: <slug>` (first slug in glob order)

- [ ] **Step 3: Rewrite the post header and layout in `_layouts/post.html`**

Replace everything from `<article class="article">` down to and including the closing `</div>` of `<div class="article-layout shell">` (i.e. the old header, hero, TOC aside, content, and share aside - currently lines 15-84) with:

```liquid
<article class="article">
  {% assign primary_category = page.categories | first %}
  {% assign primary_category_slug = primary_category | slugify %}
  <header class="article-header shell-narrow">
    <div class="article-meta">
      <a href="{{ '/categories/' | append: primary_category_slug | append: '/' | relative_url }}">{{ primary_category }}</a>
      <span aria-hidden="true">·</span>
      <time datetime="{{ page.date | date_to_xmlschema }}">{{ page.date | date: "%d.%m.%Y" }}</time>
      <span aria-hidden="true">·</span>
      <span>{{ content | number_of_words | divided_by: 200 | plus: 1 }} {% if is_en %}min read{% else %}хв читання{% endif %}</span>
      {% if page.content_meta.level %}
        <span aria-hidden="true">·</span>
        <span data-level>{{ page.content_meta.level }}</span>
      {% endif %}
      {% if page.translation_url %}
        <a class="article-language-switch" href="{{ page.translation_url | relative_url }}" hreflang="{{ page.translation_lang }}">{% if is_en %}Читати українською{% else %}Read in English{% endif %}</a>
      {% endif %}
    </div>
    <h1>{{ page.title }}</h1>
    {% if page.content_meta.scope %}
      {% assign scope_items = page.content_meta.scope | split: ', ' %}
      <ul class="article-scope" aria-label="{% if is_en %}Technologies{% else %}Технології{% endif %}">
        {% for item in scope_items %}<li>{{ item }}</li>{% endfor %}
      </ul>
    {% endif %}
  </header>

  {% if page.image.path %}
    <figure class="article-hero shell-narrow">
      <img src="{{ page.image.path | relative_url }}" alt="{{ page.title | escape }}" loading="lazy" decoding="async">
    </figure>
  {% endif %}

  <div class="article-layout">
    <aside class="article-toc">
      <div class="toc-wrap">
        <span class="toc-label">{% if is_en %}In this article{% else %}У цій статті{% endif %}</span>
        <nav class="toc" aria-label="{% if is_en %}Table of contents{% else %}Зміст статті{% endif %}" data-toc></nav>
      </div>
    </aside>
    <div class="article-content" data-article-content>
      {% assign lazy_content = content | replace: '<img ', '<img loading="lazy" decoding="async" ' %}
      {{ lazy_content }}
    </div>
  </div>

  <div class="article-end shell-narrow">
    <div class="article-share-buttons">
      <button class="icon-button" type="button" data-share aria-label="{% if is_en %}Share article{% else %}Поділитися статтею{% endif %}">
        <svg class="ui-icon ui-share" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M12 16V4m0 0L7.5 8.5M12 4l4.5 4.5M5 13v6.25c0 .966.784 1.75 1.75 1.75h10.5A1.75 1.75 0 0 0 19 19.25V13" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
      </button>
      <button class="icon-button" type="button" data-copy-link aria-label="{% if is_en %}Copy link{% else %}Скопіювати посилання{% endif %}"><span class="ui-icon ui-link" aria-hidden="true"></span></button>
    </div>
    <div class="article-tools" aria-label="{% if is_en %}Article tools{% else %}Інструменти статті{% endif %}">
      <button type="button" data-copy-markdown data-markdown-url="{{ page.markdown_url | relative_url }}">{% if is_en %}Copy Markdown{% else %}Копіювати Markdown{% endif %}</button>
      <a href="{{ page.markdown_url | relative_url }}" download>{% if is_en %}Download .md{% else %}Завантажити .md{% endif %}</a>
      <a href="{{ corpus_url | relative_url }}">LLM corpus</a>
    </div>
  </div>
```

The `reading-progress` div above `<article>` and the `learning-path-callout` section below stay as they are.

- [ ] **Step 4: Remove eyebrows from the article footer**

In `_layouts/post.html` delete these two lines:
```html
        <p class="eyebrow">Continue exploring</p>
```
```html
      <p class="eyebrow">Discuss / Learn together</p>
```
Also add the button class to the comments button: change `<button class="comments-load" type="button"` to `<button class="comments-load button" type="button"`.

- [ ] **Step 5: Create `_sass/_article.scss`**

```scss
.reading-progress {
  height: 2px;
  left: 0;
  pointer-events: none;
  position: fixed;
  right: 0;
  top: 0;
  z-index: 60;
}

.reading-progress span {
  background: var(--accent);
  display: block;
  height: 100%;
  transform: scaleX(0);
  transform-origin: left center;
}

/* Header */
.article-header {
  padding: clamp(40px, 7vw, 72px) 0 32px;
}

.article-meta {
  align-items: center;
  color: var(--text-muted);
  display: flex;
  flex-wrap: wrap;
  font-size: 14px;
  gap: 4px 8px;
  margin-bottom: 16px;
}

.article-meta a:hover {
  color: var(--text);
}

.article-meta .article-language-switch {
  color: var(--accent);
  font-weight: 500;
  margin-left: auto;
}

.article-header h1 {
  font-size: clamp(30px, 4.4vw, 42px);
  font-weight: 700;
  letter-spacing: -0.025em;
  line-height: 1.15;
  margin: 0;
}

.article-scope {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  list-style: none;
  margin: 20px 0 0;
  padding: 0;
}

.article-scope li {
  background: var(--bg-subtle);
  border-radius: 4px;
  color: var(--text-muted);
  font-size: 13px;
  padding: 2px 8px;
}

.article-hero {
  margin-bottom: 40px;
  margin-top: 0;
}

.article-hero img {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  width: 100%;
}

/* Layout: TOC | content | spacer */
.article-layout {
  column-gap: 48px;
  display: grid;
  grid-template-columns: minmax(0, 1fr) var(--measure) minmax(0, 1fr);
}

.article-content {
  grid-column: 2;
  min-width: 0;
}

.article-toc {
  grid-column: 1;
  grid-row: 1;
  justify-self: end;
  max-width: 220px;
  width: 100%;
}

.toc-wrap {
  max-height: calc(100vh - 120px);
  overflow-y: auto;
  position: sticky;
  top: 88px;
}

.toc-wrap:has(.toc:empty) {
  display: none;
}

.toc-label {
  display: block;
  font-size: 13px;
  font-weight: 600;
  margin-bottom: 10px;
}

.toc a {
  border-left: 1px solid var(--border);
  color: var(--text-muted);
  display: block;
  font-size: 14px;
  line-height: 1.4;
  padding: 4px 0 4px 12px;
}

.toc a.toc-h3 {
  font-size: 13px;
  padding-left: 24px;
}

.toc a:hover {
  color: var(--text);
}

.toc a.is-active {
  border-left-color: var(--accent);
  color: var(--text);
}

@media (max-width: 1199px) {
  .article-layout {
    display: block;
    margin-inline: auto;
    width: var(--measure);
  }

  .article-toc {
    display: none;
  }
}

/* Prose (articles and plain pages) */
.article-content,
.prose-page {
  font-size: 17px;
  line-height: 1.75;
}

.article-content > :first-child,
.prose-page > :first-child {
  margin-top: 0;
}

.article-content h2,
.prose-page h2 {
  font-size: 26px;
  font-weight: 650;
  margin: 56px 0 16px;
}

.article-content h3,
.prose-page h3 {
  font-size: 20px;
  font-weight: 650;
  margin: 40px 0 12px;
}

.article-content h4,
.prose-page h4 {
  font-size: 17px;
  margin: 32px 0 8px;
}

.article-content p,
.article-content ul,
.article-content ol,
.prose-page p,
.prose-page ul,
.prose-page ol {
  margin: 0 0 20px;
}

.article-content li,
.prose-page li {
  margin: 6px 0;
}

.article-content li > ul,
.article-content li > ol,
.prose-page li > ul,
.prose-page li > ol {
  margin: 6px 0 0;
}

.article-content a,
.prose-page a {
  color: var(--accent);
  text-decoration: underline;
  text-decoration-color: color-mix(in srgb, var(--accent) 40%, transparent);
  text-decoration-thickness: 1px;
  text-underline-offset: 3px;
}

.article-content a:hover,
.prose-page a:hover {
  text-decoration-color: var(--accent);
}

.article-content strong,
.prose-page strong {
  font-weight: 650;
}

.article-content hr,
.prose-page hr {
  border: 0;
  border-top: 1px solid var(--border);
  margin: 48px 0;
}

.article-content img,
.prose-page img {
  border-radius: var(--radius);
  margin: 28px auto;
}

.article-content blockquote,
.prose-page blockquote {
  border-left: 3px solid var(--border-strong);
  color: var(--text-muted);
  margin: 28px 0;
  padding: 2px 0 2px 20px;
}

.article-content blockquote > :last-child,
.prose-page blockquote > :last-child {
  margin-bottom: 0;
}

.article-content blockquote.prompt-info,
.article-content blockquote.prompt-warning {
  border-radius: 0 var(--radius) var(--radius) 0;
  color: var(--text);
  padding: 16px 20px;
}

.article-content blockquote.prompt-info {
  background: var(--accent-soft);
  border-left-color: var(--accent);
}

.article-content blockquote.prompt-warning {
  background: color-mix(in srgb, var(--track-cloud) 12%, var(--bg));
  border-left-color: var(--track-cloud);
}

.article-content table,
.prose-page table {
  border-collapse: collapse;
  display: block;
  font-size: 15px;
  line-height: 1.55;
  margin: 28px 0;
  overflow-x: auto;
  width: 100%;
}

.article-content th,
.article-content td,
.prose-page th,
.prose-page td {
  border-bottom: 1px solid var(--border);
  padding: 10px 12px;
  text-align: left;
  vertical-align: top;
}

.article-content th,
.prose-page th {
  border-bottom-color: var(--border-strong);
  font-weight: 600;
}

.article-content .footnotes {
  border-top: 1px solid var(--border);
  color: var(--text-muted);
  font-size: 15px;
  margin-top: 48px;
  padding-top: 24px;
}

/* End of article: share + tools */
.article-end {
  align-items: center;
  border-top: 1px solid var(--border);
  display: flex;
  flex-wrap: wrap;
  gap: 12px 24px;
  justify-content: space-between;
  margin-top: 48px;
  padding-top: 20px;
}

.article-share-buttons {
  display: flex;
  gap: 4px;
  margin-left: -8px;
}

.article-share-buttons .ui-share {
  height: 18px;
  width: 18px;
}

.article-tools {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 16px;
}

.article-tools a,
.article-tools button {
  background: none;
  border: 0;
  color: var(--text-muted);
  font-size: 14px;
  padding: 0;
}

.article-tools a:hover,
.article-tools button:hover {
  color: var(--text);
  text-decoration: underline;
  text-underline-offset: 3px;
}

/* Learning track callout */
.learning-path-callout {
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  margin-top: 48px;
  padding: 24px;
}

.learning-path-kicker {
  color: var(--text-muted);
  display: flex;
  font-size: 13px;
  justify-content: space-between;
  margin-bottom: 8px;
}

.learning-path-callout h2 {
  font-size: 20px;
  margin: 0 0 6px;
}

.learning-path-callout h2 a:hover {
  color: var(--accent);
}

.learning-path-callout p {
  color: var(--text-muted);
  font-size: 15px;
  margin: 0 0 16px;
}

.path-progress {
  background: var(--bg-subtle);
  border-radius: 999px;
  height: 4px;
  margin-bottom: 16px;
  overflow: hidden;
}

.path-progress span {
  background: var(--accent);
  display: block;
  height: 100%;
}

.learning-path-callout nav,
.post-navigation {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.learning-path-callout nav a,
.post-navigation a {
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 14px 16px;
}

.learning-path-callout nav a:hover,
.post-navigation a:hover {
  border-color: var(--border-strong);
}

.learning-path-callout nav a + a,
.post-navigation a + a {
  text-align: right;
}

.learning-path-callout nav span,
.post-navigation span {
  color: var(--text-muted);
  font-size: 13px;
}

.learning-path-callout nav strong,
.post-navigation strong {
  font-size: 15px;
  font-weight: 600;
  line-height: 1.35;
}

/* Article footer */
.article-footer {
  padding: 48px 0 var(--space-section);
}

.article-footer > * + * {
  margin-top: 48px;
}

.related-articles h2,
.comments h2 {
  font-size: 20px;
  margin: 0 0 16px;
}

.related-articles div {
  display: flex;
  flex-direction: column;
}

.related-articles a {
  border-bottom: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 14px 0;
}

.related-articles a:first-child {
  border-top: 1px solid var(--border);
}

.related-articles a span {
  color: var(--text-muted);
  font-size: 13px;
}

.related-articles a strong {
  font-weight: 600;
}

.related-articles a:hover strong {
  color: var(--accent);
}

.related-articles i {
  display: none;
}

.author-card {
  align-items: flex-start;
  background: var(--bg-subtle);
  border-radius: var(--radius);
  display: flex;
  gap: 16px;
  padding: 20px;
}

.author-card img {
  border-radius: 50%;
  height: 56px;
  width: 56px;
}

.author-card span {
  color: var(--text-muted);
  font-size: 13px;
}

.author-card h2 {
  font-size: 17px;
  margin: 2px 0 6px;
}

.author-card p {
  color: var(--text-muted);
  font-size: 15px;
  margin: 0 0 8px;
}

.author-card a {
  color: var(--accent);
  font-size: 14px;
  font-weight: 500;
}

.comments-intro,
.comments-preview {
  color: var(--text-muted);
  font-size: 15px;
}

@media (max-width: 640px) {
  .article-meta .article-language-switch {
    margin-left: 0;
    width: 100%;
  }

  .learning-path-callout nav,
  .post-navigation {
    grid-template-columns: 1fr;
  }

  .learning-path-callout nav a + a,
  .post-navigation a + a {
    text-align: left;
  }

  .author-card {
    flex-direction: column;
  }
}
```

- [ ] **Step 6: Register the partial**

Append to `assets/css/site.scss`:
```scss
@use "article";
```

- [ ] **Step 7: Amend the spec for the TOC breakpoint**

In `docs/superpowers/specs/2026-09-21-editorial-redesign-design.md` change `TOC hidden below 1100px.` to `TOC hidden below 1200px (at 1100px side columns are too narrow).`

- [ ] **Step 8: Run to verify it passes**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: PASS.

- [ ] **Step 9: Visual + behavior check**

```bash
bash tools/screenshots.sh "$TMPDIR/redesign/task3" /posts/result-pattern/ /posts/cli-jit-il/ /posts/mongodb-encryption/ /en/posts/result-pattern/
```
Expected: meta line + title of at most 3 lines at 1440; TOC left at 1440, gone at 390; inline code gray (not blue); code blocks full-bleed at 390; Mermaid diagram renders in `cli-jit-il`; hero image at column width in `mongodb-encryption`; `prompt-info` blockquotes tinted.

In a browser at `http://localhost:4010/posts/result-pattern/`: scroll - progress bar grows and TOC highlights current section; click copy on a code block - label changes; click the link icon - it turns accent color; "Копіювати Markdown" - label changes to copied state.

- [ ] **Step 10: Commit**

```bash
git add _layouts/post.html _sass/_article.scss assets/css/site.scss tools/verify_site.rb docs/superpowers/specs/2026-09-21-editorial-redesign-design.md
git commit -m "feat(ui): simplified article header, TOC layout and end-of-post tools

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Secondary pages, 404 and offline

**Files:**
- Modify: `_layouts/paths.html`, `_layouts/archives.html`, `_layouts/categories.html`, `_layouts/category.html`, `_layouts/tags.html`, `_layouts/tag.html`, `_layouts/page.html`
- Rewrite: `404.html`, `offline.html`
- Create: `_sass/_pages.scss`
- Modify: `assets/css/site.scss`, `tools/verify_site.rb`

**Interfaces:**
- Consumes: `.page-hero`, `.back-link`, `.button`, `.path-*` (Task 1); `.prose-page` prose rules (Task 3).
- Produces: nothing consumed later.

- [ ] **Step 1: Write the failing verifier assertions**

In `tools/verify_site.rb`, insert directly before the `legacy_theme = %w[chi rpy].join` line:

```ruby
Dir.glob(File.join(root, "**", "*.html")).each do |page_path|
  abort "Uppercase eyebrow label remains: #{page_path}" if Nokogiri::HTML(File.read(page_path)).at_css(".eyebrow")
end
categories_page = Nokogiri::HTML(File.read(File.join(root, "categories", "index.html")))
abort "Category cards should not show index numbers" if categories_page.at_css(".taxonomy-card > span, .taxonomy-card > i")
abort "Track cards should not show index numbers" if paths_page.css(".path-card header span").any? { |span| span.text.include?("/") }
```

- [ ] **Step 2: Run to verify it fails**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: FAIL with `Uppercase eyebrow label remains: .../404.html` (any page path is acceptable).

- [ ] **Step 3: Update `_layouts/paths.html`**

Delete `  <p class="eyebrow">Guided learning</p>`.
Replace `        <span>0{{ forloop.index }} / {{ path.level }}</span>` with `        <span>{{ path.level }}</span>`.

- [ ] **Step 4: Update `_layouts/archives.html`**

Delete `  <p class="eyebrow">Knowledge timeline</p>`.
Replace `      <span>{{ post.categories | first }} ↗</span>` with `      <span>{{ post.categories | first }}</span>`.

- [ ] **Step 5: Update `_layouts/categories.html`**

Delete `  <p class="eyebrow">Browse the knowledge base</p>`.
Replace the card body:
```liquid
      <span>0{{ forloop.index }}</span>
      <h2>{{ category[0] }}</h2>
      <p>{{ category[1].size }} {% if category[1].size == 1 %}стаття{% else %}статей{% endif %}</p>
      <i aria-hidden="true">↗</i>
```
with:
```liquid
      <h2>{{ category[0] }}</h2>
      <p>{{ category[1].size }}</p>
```

- [ ] **Step 6: Update `_layouts/category.html` and `_layouts/tag.html`**

In `category.html` replace:
```liquid
  <p class="eyebrow">Topic / {{ category_posts.size }} articles</p>
  <h1>{{ page.title }}</h1>
```
with:
```liquid
  <h1>{{ page.title }}</h1>
  <p>{{ category_posts.size }} {% if category_posts.size == 1 %}стаття{% elsif category_posts.size < 5 %}статті{% else %}статей{% endif %}</p>
```
In `tag.html` replace:
```liquid
  <p class="eyebrow">Tag / {{ tag_posts.size }} articles</p>
  <h1>{{ page.title }}</h1>
```
with:
```liquid
  <h1>{{ page.title }}</h1>
  <p>{{ tag_posts.size }} {% if tag_posts.size == 1 %}стаття{% elsif tag_posts.size < 5 %}статті{% else %}статей{% endif %}</p>
```
In both files delete the line `      <span>Читати ↗</span>`.

- [ ] **Step 7: Update `_layouts/tags.html` and `_layouts/page.html`**

In `tags.html` delete `  <p class="eyebrow">Index / A-Z</p>`.
In `page.html` delete `  <p class="eyebrow">Taras Kovalenko / Notes</p>`.

- [ ] **Step 8: Rewrite `404.html`**

```liquid
---
layout: default
title: Сторінку не знайдено
permalink: /404.html
---
<section class="not-found shell">
  <p class="not-found-code">404</p>
  <h1>Сторінку не знайдено</h1>
  <p>Можливо, посилання застаріло або сторінку було переміщено. Усі інженерні нотатки залишилися у базі знань.</p>
  <a class="button button-primary" href="{{ '/' | relative_url }}">На головну</a>
</section>
```

- [ ] **Step 9: Rewrite `offline.html`**

```liquid
---
layout: default
title: Ви офлайн
permalink: /offline.html
---
<section class="not-found shell">
  <p class="not-found-code">Offline</p>
  <h1>Немає з'єднання з мережею</h1>
  <p>Перевірте підключення та спробуйте ще раз. Раніше відкриті статті можуть залишатися доступними офлайн.</p>
  <a class="button button-primary" href="{{ '/' | relative_url }}">Спробувати знову</a>
</section>
```

- [ ] **Step 10: Create `_sass/_pages.scss`**

```scss
/* Tracks page */
.paths-grid {
  display: grid;
  gap: 20px;
  padding-bottom: var(--space-section);
}

.path-card {
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-left: 3px solid var(--track, var(--accent));
  border-radius: var(--radius);
  padding: 28px;
  scroll-margin-top: 88px;
}

.path-card header {
  color: var(--text-muted);
  display: flex;
  font-size: 14px;
  gap: 12px;
  justify-content: space-between;
  margin-bottom: 8px;
}

.path-card header strong {
  font-weight: 500;
}

.path-card h2 {
  font-size: 22px;
  margin: 0 0 6px;
}

.path-card > p {
  color: var(--text-muted);
  margin: 0 0 20px;
}

.path-card ol {
  list-style: none;
  margin: 0;
  padding: 0;
}

.path-card li {
  align-items: baseline;
  border-top: 1px solid var(--border);
  display: grid;
  gap: 12px;
  grid-template-columns: 24px minmax(0, 1fr) auto;
  padding: 10px 0;
}

.path-card li span,
.path-card li small {
  color: var(--text-muted);
  font-size: 13px;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.path-card li a {
  font-weight: 500;
}

.path-card li a:hover {
  color: var(--accent);
}

/* Categories */
.taxonomy-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  padding-bottom: var(--space-section);
}

.taxonomy-card {
  align-items: baseline;
  background: var(--bg-raised);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  display: flex;
  justify-content: space-between;
  padding: 16px 18px;
}

.taxonomy-card:hover {
  border-color: var(--border-strong);
}

.taxonomy-card h2 {
  font-size: 17px;
  font-weight: 600;
  letter-spacing: -0.01em;
  margin: 0;
}

.taxonomy-card p {
  color: var(--text-muted);
  font-size: 14px;
  margin: 0;
}

/* Tags */
.tag-cloud {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  padding-bottom: var(--space-section);
}

.tag-cloud a {
  align-items: baseline;
  border: 1px solid var(--border);
  border-radius: 999px;
  display: inline-flex;
  font-size: 14px;
  gap: 6px;
  padding: 6px 12px;
}

.tag-cloud a:hover {
  border-color: var(--border-strong);
}

.tag-cloud sup {
  color: var(--text-muted);
  font-size: 12px;
  vertical-align: baseline;
}

/* Archive */
.archive-list {
  padding-bottom: var(--space-section);
}

.archive-year {
  margin-bottom: 40px;
}

.archive-year h2 {
  font-size: 20px;
  margin: 0 0 8px;
}

.archive-row {
  align-items: baseline;
  border-bottom: 1px solid var(--border);
  display: grid;
  gap: 16px;
  grid-template-columns: 56px minmax(0, 1fr) auto;
  padding: 12px 0;
}

.archive-row time,
.archive-row span {
  color: var(--text-muted);
  font-size: 14px;
  font-variant-numeric: tabular-nums;
}

.archive-row strong {
  font-weight: 500;
}

.archive-row:hover strong {
  color: var(--accent);
}

/* Category / tag post lists */
.page-hero .back-link {
  display: inline-block;
  margin-top: 12px;
}

.simple-post-list {
  padding-bottom: var(--space-section);
}

.simple-post-list a {
  align-items: baseline;
  border-bottom: 1px solid var(--border);
  display: grid;
  gap: 16px;
  grid-template-columns: 104px minmax(0, 1fr);
  padding: 14px 0;
}

.simple-post-list time {
  color: var(--text-muted);
  font-size: 14px;
  font-variant-numeric: tabular-nums;
}

.simple-post-list h2 {
  font-size: 17px;
  font-weight: 500;
  letter-spacing: -0.01em;
  margin: 0;
}

.simple-post-list a:hover h2 {
  color: var(--accent);
}

/* About and other plain pages */
.prose-page {
  padding-bottom: var(--space-section);
}

/* 404 / offline */
.not-found {
  padding: clamp(72px, 12vw, 140px) 0;
}

.not-found-code {
  color: var(--accent);
  font: 500 14px/1 var(--mono);
  margin: 0 0 12px;
}

.not-found h1 {
  font-size: clamp(32px, 5vw, 48px);
  margin: 0 0 12px;
}

.not-found p:not(.not-found-code) {
  color: var(--text-muted);
  font-size: 18px;
  margin: 0 0 24px;
  max-width: 520px;
}

@media (max-width: 640px) {
  .path-card {
    padding: 20px;
  }

  .archive-row {
    grid-template-columns: 48px minmax(0, 1fr);
  }

  .archive-row span {
    display: none;
  }

  .simple-post-list a {
    gap: 4px;
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 11: Register the partial**

Append to `assets/css/site.scss`:
```scss
@use "pages";
```

- [ ] **Step 12: Run to verify it passes**

Run: `JEKYLL_ENV=production bundle exec jekyll b -q && bundle exec ruby tools/verify_site.rb _site`
Expected: PASS.

- [ ] **Step 13: Visual check**

```bash
bash tools/screenshots.sh "$TMPDIR/redesign/task4" /paths/ /en/paths/ /categories/ /categories/net/ /tags/ /archives/ /about/ /404.html /offline.html
```
Expected: every page uses the same hero (h1 + muted line), consistent dividers, no ↗ arrows or `01 /` numbers, readable at 390.

- [ ] **Step 14: Commit**

```bash
git add _layouts/paths.html _layouts/archives.html _layouts/categories.html _layouts/category.html _layouts/tags.html _layouts/tag.html _layouts/page.html 404.html offline.html _sass/_pages.scss assets/css/site.scss tools/verify_site.rb
git commit -m "feat(ui): restyle tracks, topics, archive and utility pages

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>"
```

---

### Task 5: Full verification and PR

**Files:** none modified unless a check fails (fix in the owning partial/layout, then re-run).

- [ ] **Step 1: Full test suite**

Run: `bash tools/test.sh`
Expected: `HTML-Proofer finished successfully.` and `Verified 21 Ukrainian and 21 English articles, ...`

- [ ] **Step 2: Full screenshot matrix**

```bash
bash tools/screenshots.sh "$TMPDIR/redesign/final"
```
Review all 40 PNGs against the spec. Specifically check at 390px: no content wider than the viewport (right edge of text, code frames, tables), header fits on one row.

- [ ] **Step 3: Horizontal overflow probe**

In the browser (DevTools console, width 390) on `/`, `/posts/cli-jit-il/`, `/archives/`:
```js
[...document.querySelectorAll("body *")].filter((el) => el.getBoundingClientRect().right > innerWidth + 1 && !el.closest("pre, table, .mermaid-viewport")).map((el) => el.className)
```
Expected: `[]`.

- [ ] **Step 4: Interactive checks (both themes, UA and EN)**

At `http://localhost:4010/` then `/en/`:
1. ⌘K opens search; type `grpc`/`.net`; ↑/↓ moves highlight; Enter opens result; Esc closes.
2. Theme toggle switches and persists after reload.
3. At 390px: hamburger opens menu with 5 links incl. Archive; X closes it.
4. Filter chips work (see Task 2 Step 7).
5. Post page: code copy, link copy, share, Copy Markdown, TOC highlight, progress bar, "Load comments" button is styled (production build).

- [ ] **Step 5: Contrast check**

With DevTools CSS overview or https://webaim.org/resources/contrastchecker/ verify ≥ 4.5:1 for: `--text-muted` on `--bg` and on `--bg-raised`, both themes; `--accent` on `--bg`, both themes. Values to check: light `#5c5b55` on `#fbfaf7`, `#2451e6` on `#fbfaf7`; dark `#a3a199` on `#111110`, `#7d9bff` on `#111110`. If one fails, darken (light) / lighten (dark) that token in `_sass/_tokens.scss` and re-run Step 1.

- [ ] **Step 6: Ask the user before pushing**

Show the user the before/after screenshots for home and one post (both themes). Only after they confirm, push and open the PR:

```bash
git push -u origin feat/editorial-redesign
gh pr create --title "feat: calm editorial redesign" --body "$(cat <<'EOF'
## Summary
- Replace the 3k-line legacy stylesheet with focused Sass partials and a small token system (one accent, Inter + JetBrains Mono, 680px reading column)
- Home: intro, compact topic tracks, filterable single-column post list; removed generated covers, ticker and signal card
- Post: one meta line, smaller title, TOC beside content (>=1200px), share + Markdown/LLM tools moved to the end of the article
- Secondary pages, 404 and offline restyled with the same components; no eyebrow labels
- Verifier extended to lock in the new structure; service worker cache bumped

Spec: docs/superpowers/specs/2026-09-21-editorial-redesign-design.md

## Test plan
- [x] `bash tools/test.sh` (build, html-proofer, verify_site.rb)
- [x] Screenshots at 1440/390, light/dark, UA/EN
- [x] Search, theme, mobile menu, filters, code copy, share, Markdown copy, Mermaid

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
