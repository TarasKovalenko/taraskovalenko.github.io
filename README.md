# Taras Kovalenko — Engineering Notes

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

Article URLs follow the compatibility contract `/posts/:title/`.
