# frozen_string_literal: true

require "nokogiri"
require "json"
require "rexml/document"

root = File.expand_path(ARGV.fetch(0, "_site"))
source_root = File.expand_path("..", __dir__)
post_sources = Dir[File.join(source_root, "_posts", "*.md")]

post_sources.each do |source|
  slug = File.basename(source, ".md").sub(/^\d{4}-\d{2}-\d{2}-/, "")
  output = File.join(root, "posts", slug, "index.html")
  abort "Missing legacy article URL: /posts/#{slug}/" unless File.file?(output)
  markdown_output = File.join(root, "posts", slug, "index.md")
  abort "Missing Markdown endpoint: /posts/#{slug}/index.md" unless File.file?(markdown_output)
  abort "Markdown endpoint is empty: /posts/#{slug}/index.md" if File.size(markdown_output) < 100
end

home = Nokogiri::HTML(File.read(File.join(root, "index.html")))
card_count = home.css("[data-article]").size
abort "Expected #{post_sources.size} article cards, found #{card_count}" unless card_count == post_sources.size
abort "CSS-native interface icons are missing" if home.css(".ui-search, .ui-theme").size < 2

%w[404.html feed.xml llms.txt llms-full.txt offline.html paths/index.html robots.txt sitemap.xml sw.js].each do |endpoint|
  abort "Missing generated endpoint: /#{endpoint}" unless File.file?(File.join(root, endpoint))
end
abort "Full LLM corpus is unexpectedly small" if File.size(File.join(root, "llms-full.txt")) < 100_000

REXML::Document.new(File.read(File.join(root, "feed.xml")))

manifest_path = File.join(root, "assets", "img", "favicons", "site.webmanifest")
manifest = JSON.parse(File.read(manifest_path))
abort "Web manifest is missing its application name" if manifest["name"].to_s.empty?
manifest.fetch("icons").each do |icon|
  icon_path = icon.fetch("src").sub(%r{\A/}, "")
  abort "Manifest icon does not exist: #{icon["src"]}" unless File.file?(File.join(root, icon_path))
end

home_schema = JSON.parse(home.at_css('script[type="application/ld+json"]').text)
abort "Homepage structured data is not a WebSite" unless home_schema["@type"] == "WebSite"

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

search_items = home.css("[data-search-item]")
abort "Search index is incomplete" unless search_items.size == post_sources.size
abort "Weighted search metadata is missing" if search_items.any? { |item| item["data-topics"].to_s.empty? }
site_javascript = File.read(File.join(root, "assets", "js", "site.js"))
abort "Keyboard search navigation is missing" unless site_javascript.include?("ArrowDown") && site_javascript.include?("activeSearchIndex")
abort "Recent search support is missing" unless site_javascript.include?("tk-recent-searches")

mermaid_post = Nokogiri::HTML(File.read(File.join(root, "posts", "cli-jit-il", "index.html")))
abort "Mermaid source blocks are missing" if mermaid_post.css("code.language-mermaid").empty?
abort "Mermaid renderer is missing" unless mermaid_post.at_css('script[type="module"][src="/assets/js/mermaid.js"]')
abort "Mermaid renderer asset is missing" unless File.file?(File.join(root, "assets", "js", "mermaid.js"))

legacy_theme = %w[chi rpy].join
theme_reference = Dir.glob(File.join(root, "**", "*")).find do |path|
  File.file?(path) && File.binread(path).downcase.include?(legacy_theme)
rescue ArgumentError
  false
end
abort "Legacy theme reference leaked into generated output: #{theme_reference}" if theme_reference

puts "Verified #{post_sources.size} legacy URLs and Markdown endpoints, #{card_count} cards, four learning paths, related content, command search, AI corpus, PWA files, comments, structured data, and no legacy theme output."
