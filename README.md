# Taras Kovalenko - Engineering Notes

Personal engineering blog about .NET, software architecture, cloud platforms,
performance, and practical AI development.

[Read the blog](https://taraskovalenko.github.io/)

## Local development

```bash
rbenv exec bundle install
rbenv exec bundle exec jekyll serve --livereload
```

The site is available at [http://127.0.0.1:4000](http://127.0.0.1:4000).

## Verification

```bash
JEKYLL_ENV=production rbenv exec bundle exec jekyll build
rbenv exec bundle exec htmlproofer _site --disable-external
rbenv exec ruby tools/verify_site.rb _site
```

Article URLs follow the bilingual compatibility contract:

- Ukrainian originals keep their existing `/posts/:title/` URLs.
- English translations use `/en/posts/:title/`.
- Translation pairs share the same filename slug in `_posts/` and `_posts_en/`.

To create or refresh English first drafts after adding a Ukrainian article:

```bash
rbenv exec ruby tools/translate_posts.rb
```

The translation tool preserves Markdown, code blocks, Mermaid diagrams, and
internal link structure. Its output is a machine-assisted editorial first draft
and should be reviewed before publishing. Add `translation_status: reviewed`
to a finished English article so later runs do not overwrite it. Use `--force`
only when you intentionally want to regenerate reviewed translations.

## Content features

- Ukrainian and English learning paths are configured in
  `_data/learning_paths.yml` and `_data/learning_paths_en.yml`.
- Article scope, level, and freshness classification live in
  `_data/content_metadata.yml`.
- Related articles and path navigation are calculated during the Jekyll build.
- Every article is also emitted as a Markdown endpoint at
  `/posts/:title/index.md` or `/en/posts/:title/index.md`.
- `/llms.txt` and `/en/llms.txt` are compact AI indexes; `/llms-full.txt` and
  `/en/llms-full.txt` contain the complete language-specific Markdown corpora.
