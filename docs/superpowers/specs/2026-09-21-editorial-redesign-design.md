# Editorial Redesign - Design Spec

Date: 2026-09-21
Branch: `feat/editorial-redesign`
Status: Approved (direction, scope, approach)

## Goal

Replace the current "engineering signal" look with a calm, text-first editorial
design (reference feel: overreacted.io, Stripe blog, Linear changelog). The site
must read as clean and modern, with less visual noise, while keeping every
existing feature working in both languages (UA / EN) and both themes.

## Problems being fixed

Home:
- Generated placeholder covers (grid + `.net` box + random shapes) dominate cards,
  carry no information, and clash with real images.
- Zig-zag wide-card layout followed by a 3-column grid - no consistent rhythm.
- Too many competing sections: hero, "Latest signal" card, focus ticker, tracks,
  filters, grid, RSS block.
- Tiny uppercase mono labels everywhere (eyebrows, `№ 21`, post numbers, `01 / LEVEL`).

Post:
- Oversized title (5 lines on desktop).
- Crowded header: breadcrumb + tag pills + author row + 4-cell facts table +
  4 tool buttons.
- TOC text too small; inline code highlighted too loudly (blue background).

## Approach

Rewrite `assets/css/site.css` from scratch (target ~1000 lines, down from 3051)
and simplify the layouts. Patching the existing stylesheet was rejected (leaves
dead rules and specificity conflicts); switching to a third-party theme was
rejected (breaks bilingual, tracks, and LLM features).

## Design system

- **Widths:** reading column `--measure: 680px`; page shell `--shell: 1080px`;
  16px side gutter on mobile, no horizontal scroll at 390px.
- **Color:** neutral grays + one accent (blue, kept from current site).
  Light: warm off-white background, near-black text. Dark: near-black background,
  off-white text. Tokens on `:root` and `[data-theme="dark"]` (existing theme
  toggle sets `data-theme`). Track accent colors kept only as thin left borders.
- **Type:** Inter for UI and body; JetBrains Mono (fallback system mono) only for
  code. No uppercase mono labels. Body 17px / 1.7 in articles, 16px elsewhere.
  Scale: h1 post ~40px desktop / 30px mobile; section h2 ~24px.
- **Spacing:** 4/8px scale; one consistent vertical rhythm between sections.
- **Motion:** minimal - hover color changes only; respect `prefers-reduced-motion`.

## Components

### Header (`_includes/header.html`)
- Left: plain text name "Taras Kovalenko" (no "TK" block, no subtitle).
- Right: Articles, Tracks, Topics, About; then search button (icon + `⌘K`),
  UA/EN switch, theme icon. Archive moves to footer.
- Thin bottom border, sticky, translucent background.
- Mobile: menu toggle with same links + Archive.

### Home (`_layouts/home.html`)
1. **Intro:** one heading line + one sentence of focus. Remove "Latest signal"
   card, status pulse, and focus-stack ticker.
2. **Tracks:** 2x2 grid (1 column on mobile) of plain text blocks - colored thin
   left border, title, one-line description, article count. Link "All tracks".
3. **Latest articles:** section heading + filter chips (quiet pill style, active =
   filled). Vertical list; each item:
   - meta line: topic · date · reading time
   - title (link)
   - 2-line excerpt (clamped)
   - optional small thumbnail on the right **only** when `post.image.path`
     exists; generated covers removed entirely.
4. **RSS:** one line at bottom ("Subscribe via RSS").

### Post (`_layouts/post.html`)
- Single meta line above title: `category · date · N min · level` +
  small "Read in English / Читати українською" link when translation exists.
- Title ~40px, max width = measure.
- Remove: breadcrumbs, tag pills block, author byline block, facts table.
  `content_meta.scope` technologies render as small text tags under the title
  (optional, only if present).
- Hero image (if any) at measure width.
- Layout: TOC sticky on left (readable 14px), content in measure column.
  TOC hidden below 1100px. Share rail removed from side.
- **Post footer:** share + copy link icons, then a quiet row of links:
  Copy Markdown · Download .md · LLM corpus. Then comments.
- Inline code: subtle gray background, no blue. Code blocks: keep toolbar and
  copy button, calmer border/background.
- Reading progress bar: kept, 2px, accent color.

### Other pages
Tracks (`paths.html`), categories, category, tags, tag, archives, page (about),
404: restyled via shared list/heading components. No structural changes beyond
removing eyebrow labels.

### Footer (`_includes/footer.html`)
One row: copyright, Archive, RSS, social links. Muted text.

## JS contract (must not break)

`assets/js/site.js` depends on these hooks; they stay unchanged:
`data-theme-toggle`, `data-menu-toggle`, `data-mobile-menu`, `data-header`,
`data-search-open`, `data-search-dialog`, `data-search-input`,
`data-search-results`, `data-search-item`, `data-search-close`, `data-filter`,
`data-article` (with `data-topics`), `data-no-results`, `data-article-content`,
`data-toc`, `data-reading-progress`, `data-share`, `data-copy-link`,
`data-copy-markdown` (with `data-markdown-url`), `data-comments-load`,
`.utterances-frame`, `.article-content div.highlighter-rouge`, `.code-toolbar`.
Any class names JS generates (search results, code toolbar) must get styles in
the new CSS.

## Out of scope

- Content changes, new features, new pages.
- Changes to `_plugins/`, data files, feeds, `llms.txt`, service worker logic
  (cache version bump allowed if CSS path caching requires it).

## Verification

- `bundle exec jekyll build` succeeds with no new warnings.
- Headless Chrome screenshots at 1440px and 390px for: home UA, home EN, a post
  with code + TOC, a post with hero image, tracks, categories, archive, about -
  in light and dark themes. No horizontal scroll at 390px.
- Manual checks in browser: filter chips, search dialog (⌘K, keyboard nav),
  theme toggle persistence, mobile menu, copy-code, copy markdown, share,
  mermaid diagram rendering, comments load.
- Contrast: body text and muted text meet WCAG AA in both themes.

## Delivery

Single PR from `feat/editorial-redesign`.
