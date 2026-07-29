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
end

home = Nokogiri::HTML(File.read(File.join(root, "index.html")))
card_count = home.css("[data-article]").size
abort "Expected #{post_sources.size} article cards, found #{card_count}" unless card_count == post_sources.size
abort "CSS-native interface icons are missing" if home.css(".ui-search, .ui-theme").size < 2

%w[404.html feed.xml llms.txt offline.html robots.txt sitemap.xml sw.js].each do |endpoint|
  abort "Missing generated endpoint: /#{endpoint}" unless File.file?(File.join(root, endpoint))
end

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

puts "Verified #{post_sources.size} legacy article URLs, #{card_count} cards, PWA files, comments, structured data, feed XML, metadata endpoints, and no legacy theme output."
