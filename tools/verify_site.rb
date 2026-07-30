# frozen_string_literal: true

require "nokogiri"
require "json"
require "rexml/document"

root = File.expand_path(ARGV.fetch(0, "_site"))
source_root = File.expand_path("..", __dir__)
post_sources = Dir[File.join(source_root, "_posts", "*.md")]
english_post_sources = Dir[File.join(source_root, "_posts_en", "*.md")]
source_slugs = post_sources.map { |source| File.basename(source, ".md").sub(/^\d{4}-\d{2}-\d{2}-/, "") }.sort
english_slugs = english_post_sources.map { |source| File.basename(source, ".md") }.sort

abort "English article coverage does not match Ukrainian sources" unless english_slugs == source_slugs

post_sources.each do |source|
  slug = File.basename(source, ".md").sub(/^\d{4}-\d{2}-\d{2}-/, "")
  output = File.join(root, "posts", slug, "index.html")
  abort "Missing legacy article URL: /posts/#{slug}/" unless File.file?(output)
  markdown_output = File.join(root, "posts", slug, "index.md")
  abort "Missing Markdown endpoint: /posts/#{slug}/index.md" unless File.file?(markdown_output)
  abort "Markdown endpoint is empty: /posts/#{slug}/index.md" if File.size(markdown_output) < 100

  english_output = File.join(root, "en", "posts", slug, "index.html")
  abort "Missing English article URL: /en/posts/#{slug}/" unless File.file?(english_output)
  english_markdown = File.join(root, "en", "posts", slug, "index.md")
  abort "Missing English Markdown endpoint: /en/posts/#{slug}/index.md" unless File.file?(english_markdown)
  abort "English Markdown endpoint is empty: /en/posts/#{slug}/index.md" if File.size(english_markdown) < 100

  uk_document = Nokogiri::HTML(File.read(output))
  en_document = Nokogiri::HTML(File.read(english_output))
  abort "Ukrainian article language changed: #{slug}" unless uk_document.at_css("html")["lang"] == "uk"
  abort "English article language is missing: #{slug}" unless en_document.at_css("html")["lang"] == "en"
  abort "Ukrainian article is missing its English alternate: #{slug}" unless uk_document.at_css('link[hreflang="en"]')
  abort "English article is missing its Ukrainian alternate: #{slug}" unless en_document.at_css('link[hreflang="uk"]')
  abort "English article switcher is missing: #{slug}" unless en_document.at_css(".article-language-switch")
end

home = Nokogiri::HTML(File.read(File.join(root, "index.html")))
card_count = home.css("[data-article]").size
abort "Expected #{post_sources.size} article cards, found #{card_count}" unless card_count == post_sources.size
abort "CSS-native interface icons are missing" if home.css(".ui-search, .ui-theme").size < 2

english_home = Nokogiri::HTML(File.read(File.join(root, "en", "index.html")))
english_card_count = english_home.css("[data-article]").size
abort "Expected #{post_sources.size} English article cards, found #{english_card_count}" unless english_card_count == post_sources.size
abort "English home links leaked to Ukrainian articles" if english_home.css('[data-article] a[href^="/posts/"]').any?
abort "English search index is incomplete" unless english_home.css("[data-search-item]").size == english_post_sources.size

%w[404.html feed.xml llms.txt llms-full.txt offline.html paths/index.html robots.txt sitemap.xml sw.js].each do |endpoint|
  abort "Missing generated endpoint: /#{endpoint}" unless File.file?(File.join(root, endpoint))
end
%w[en/index.html en/feed.xml en/llms.txt en/llms-full.txt en/paths/index.html].each do |endpoint|
  abort "Missing generated English endpoint: /#{endpoint}" unless File.file?(File.join(root, endpoint))
end
abort "Full LLM corpus is unexpectedly small" if File.size(File.join(root, "llms-full.txt")) < 100_000
abort "English LLM corpus is unexpectedly small" if File.size(File.join(root, "en", "llms-full.txt")) < 100_000

utf8_bom = "\xEF\xBB\xBF".b
%w[llms.txt llms-full.txt en/llms.txt en/llms-full.txt].each do |endpoint|
  abort "#{endpoint} is missing its UTF-8 signature" unless File.binread(File.join(root, endpoint), 3) == utf8_bom
end

REXML::Document.new(File.read(File.join(root, "feed.xml")))
REXML::Document.new(File.read(File.join(root, "en", "feed.xml")))

manifest_path = File.join(root, "assets", "img", "favicons", "site.webmanifest")
manifest = JSON.parse(File.read(manifest_path))
abort "Web manifest is missing its application name" if manifest["name"].to_s.empty?
manifest.fetch("icons").each do |icon|
  icon_path = icon.fetch("src").sub(%r{\A/}, "")
  abort "Manifest icon does not exist: #{icon["src"]}" unless File.file?(File.join(root, icon_path))
end

home_schema = JSON.parse(home.at_css('script[type="application/ld+json"]').text)
abort "Homepage structured data is not a WebSite" unless home_schema["@type"] == "WebSite"
english_home_schema = JSON.parse(english_home.at_css('script[type="application/ld+json"]').text)
abort "English homepage language metadata is missing" unless english_home_schema["inLanguage"] == "en-US"

sample_post_path = File.join(root, "posts", "result-pattern", "index.html")
sample_post = Nokogiri::HTML(File.read(sample_post_path))
post_schema = JSON.parse(sample_post.at_css('script[type="application/ld+json"]').text)
abort "Article structured data is not a BlogPosting" unless post_schema["@type"] == "BlogPosting"
comments_button = sample_post.at_css("[data-comments-load]")
abort "Production comments are missing" unless comments_button
abort "Comments repository is missing" if comments_button["data-repo"].to_s.empty?
abort "Article metadata is incomplete" unless sample_post.at_css('meta[property="article:published_time"]')
abort "Article context metadata is missing" if sample_post.css(".article-facts > div").size < 4
abort "Markdown article tools are missing" unless sample_post.at_css("[data-copy-markdown][data-markdown-url]")
abort "Learning-path navigation is missing" unless sample_post.at_css(".learning-path-callout")
abort "Related articles are missing" if sample_post.css(".related-articles a").size < 3

paths_page = Nokogiri::HTML(File.read(File.join(root, "paths", "index.html")))
abort "Expected four learning paths" unless paths_page.css(".path-card").size == 4
abort "Not every article belongs to a learning path" unless paths_page.css(".path-card li a").size == post_sources.size

english_paths_page = Nokogiri::HTML(File.read(File.join(root, "en", "paths", "index.html")))
abort "Expected four English learning paths" unless english_paths_page.css(".path-card").size == 4
abort "Not every English article belongs to a learning path" unless english_paths_page.css(".path-card li a").size == english_post_sources.size

search_items = home.css("[data-search-item]")
abort "Search index is incomplete" unless search_items.size == post_sources.size
abort "Weighted search metadata is missing" if search_items.any? { |item| item["data-topics"].to_s.empty? }
site_javascript = File.read(File.join(root, "assets", "js", "site.js"))
abort "Keyboard search navigation is missing" unless site_javascript.include?("ArrowDown") && site_javascript.include?("activeSearchIndex")
abort "Recent search support is missing" unless site_javascript.include?("tk-recent-searches")

mermaid_post = Nokogiri::HTML(File.read(File.join(root, "posts", "cli-jit-il", "index.html")))
abort "Mermaid source blocks are missing" if mermaid_post.css("code.language-mermaid").empty?
abort "Mermaid renderer is missing" unless mermaid_post.at_css('script[type="module"][src^="/assets/js/mermaid.js"]')
abort "Mermaid renderer asset is missing" unless File.file?(File.join(root, "assets", "js", "mermaid.js"))

legacy_theme = %w[chi rpy].join
theme_reference = Dir.glob(File.join(root, "**", "*")).find do |path|
  File.file?(path) && File.binread(path).downcase.include?(legacy_theme)
rescue ArgumentError
  false
end
abort "Legacy theme reference leaked into generated output: #{theme_reference}" if theme_reference

puts "Verified #{post_sources.size} Ukrainian and #{english_post_sources.size} English articles, reciprocal language URLs, Markdown endpoints, bilingual cards, tracks, search, feeds, AI corpora, PWA files, comments, structured data, and no legacy theme output."
