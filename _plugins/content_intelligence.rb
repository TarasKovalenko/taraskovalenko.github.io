# frozen_string_literal: true

require "fileutils"

module ContentIntelligence
  module_function

  UTF8_BOM = "\xEF\xBB\xBF".b

  def write_utf8(path, content)
    File.binwrite(path, UTF8_BOM + content.b)
  end

  def ensure_utf8_bom(path)
    return unless File.file?(path)

    content = File.binread(path)
    File.binwrite(path, UTF8_BOM + content) unless content.start_with?(UTF8_BOM)
  end

  def slug_for(post)
    File.basename(post.relative_path, ".md").sub(/^\d{4}-\d{2}-\d{2}-/, "")
  end

  def normalized(values)
    Array(values).map { |value| value.to_s.downcase.strip }.reject(&:empty?)
  end

  class Generator < Jekyll::Generator
    safe true
    priority :low

    def generate(site)
      uk_posts = site.posts.docs
      en_posts = site.collections.fetch("posts_en").docs.sort_by(&:date).reverse
      uk_by_slug = uk_posts.to_h { |post| [ContentIntelligence.slug_for(post), post] }
      en_by_slug = en_posts.to_h { |post| [ContentIntelligence.slug_for(post), post] }

      attach_metadata(site, uk_posts, "uk", "/posts")
      attach_metadata(site, en_posts, "en", "/en/posts")
      attach_translations(uk_by_slug, en_by_slug)
      attach_learning_paths(site, uk_by_slug, "learning_paths")
      attach_learning_paths(site, en_by_slug, "learning_paths_en")
      attach_related_posts(uk_posts)
      attach_related_posts(en_posts)

      site.config["posts_by_slug"] = uk_by_slug
      site.config["english_posts"] = en_posts
    end

    private

    def attach_metadata(site, posts, language, url_prefix)
      metadata = site.data.fetch("content_metadata", {})
      posts.each do |post|
        slug = ContentIntelligence.slug_for(post)
        post.data["content_meta"] = metadata.fetch(slug, {})
        post.data["source_slug"] = slug
        post.data["lang"] = language
        post.data["markdown_url"] = "#{url_prefix}/#{slug}/index.md"
      end
    end

    def attach_translations(uk_by_slug, en_by_slug)
      uk_by_slug.each do |slug, post|
        counterpart = en_by_slug[slug]
        next unless counterpart

        post.data["translation_url"] = counterpart.url
        post.data["translation_lang"] = "en"
        counterpart.data["translation_url"] = post.url
        counterpart.data["translation_lang"] = "uk"
      end
    end

    def attach_learning_paths(site, posts_by_slug, data_key)
      Array(site.data[data_key]).each do |path|
        documents = Array(path["posts"]).filter_map { |slug| posts_by_slug[slug] }
        path["documents"] = documents
        path["count"] = documents.length

        documents.each_with_index do |post, index|
          post.data["learning_path"] = {
            "id" => path["id"],
            "title" => path["title"],
            "description" => path["description"],
            "level" => path["level"],
            "position" => index + 1,
            "total" => documents.length,
            "previous_post" => index.positive? ? documents[index - 1] : nil,
            "next_post" => documents[index + 1]
          }
        end
      end
    end

    def attach_related_posts(posts)
      posts.each do |post|
        categories = ContentIntelligence.normalized(post.data["categories"])
        tags = ContentIntelligence.normalized(post.data["tags"])
        path_id = post.data.dig("learning_path", "id")

        ranked = posts.filter_map do |candidate|
          next if candidate == post

          candidate_categories = ContentIntelligence.normalized(candidate.data["categories"])
          candidate_tags = ContentIntelligence.normalized(candidate.data["tags"])
          category_overlap = (categories & candidate_categories).length
          tag_overlap = (tags & candidate_tags).length
          same_path = path_id && candidate.data.dig("learning_path", "id") == path_id
          score = (category_overlap * 4) + (tag_overlap * 2) + (same_path ? 6 : 0)
          next if score.zero?

          [candidate, score]
        end

        post.data["related_posts"] = ranked
          .sort_by { |candidate, score| [-score, -candidate.date.to_i] }
          .first(3)
          .map(&:first)
      end
    end
  end

  def raw_markdown(post, site)
    source_path = site.in_source_dir(post.relative_path)
    source = File.read(source_path)
    body = source.split(/^---\s*$\n/, 3).fetch(2, source).sub(/\A\s+/, "")
    categories = Array(post.data["categories"]).join(", ")
    tags = Array(post.data["tags"]).join(", ")
    canonical = "#{site.config["url"]}#{site.config["baseurl"]}#{post.url}"

    <<~MARKDOWN
      # #{post.data["title"]}

      - Canonical URL: #{canonical}
      - Published: #{post.date.strftime("%Y-%m-%d")}
      - Categories: #{categories}
      - Tags: #{tags}

      #{body}
    MARKDOWN
  end

  Jekyll::Hooks.register :site, :post_write do |site|
    uk_full_document = [
      "# #{site.config["title"]} - Full article corpus",
      "",
      "> #{site.config["description"].to_s.strip}",
      "",
      "Canonical site: #{site.config["url"]}#{site.config["baseurl"]}",
      ""
    ]

    en_full_document = [
      "# #{site.config["title"]} - Full English article corpus",
      "",
      "> Practical engineering notes about .NET, architecture, cloud platforms, performance, and AI engineering.",
      "",
      "Canonical site: #{site.config["url"]}#{site.config["baseurl"]}/en/",
      ""
    ]

    document_sets = [
      [site.posts.docs, "posts", uk_full_document],
      [site.collections.fetch("posts_en").docs.sort_by(&:date).reverse, "en/posts", en_full_document]
    ]

    document_sets.each do |posts, prefix, corpus|
      posts.each do |post|
        slug = ContentIntelligence.slug_for(post)
        markdown = ContentIntelligence.raw_markdown(post, site)
        destination = File.join(site.dest, prefix, slug, "index.md")
        FileUtils.mkdir_p(File.dirname(destination))
        ContentIntelligence.write_utf8(destination, markdown)
        corpus << markdown
        corpus << "\n---\n"
      end
    end

    ContentIntelligence.write_utf8(File.join(site.dest, "llms-full.txt"), uk_full_document.join("\n"))
    FileUtils.mkdir_p(File.join(site.dest, "en"))
    ContentIntelligence.write_utf8(File.join(site.dest, "en", "llms-full.txt"), en_full_document.join("\n"))
    ContentIntelligence.ensure_utf8_bom(File.join(site.dest, "llms.txt"))
    ContentIntelligence.ensure_utf8_bom(File.join(site.dest, "en", "llms.txt"))
  end
end
