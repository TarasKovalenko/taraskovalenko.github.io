# frozen_string_literal: true

require "fileutils"

module ContentIntelligence
  module_function

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
      posts = site.posts.docs
      posts_by_slug = posts.to_h { |post| [ContentIntelligence.slug_for(post), post] }

      attach_metadata(site, posts, posts_by_slug)
      attach_learning_paths(site, posts_by_slug)
      attach_related_posts(posts)
    end

    private

    def attach_metadata(site, posts, posts_by_slug)
      metadata = site.data.fetch("content_metadata", {})
      posts.each do |post|
        slug = ContentIntelligence.slug_for(post)
        post.data["content_meta"] = metadata.fetch(slug, {})
        post.data["source_slug"] = slug
        post.data["markdown_url"] = "/posts/#{slug}/index.md"
      end
      site.config["posts_by_slug"] = posts_by_slug
    end

    def attach_learning_paths(site, posts_by_slug)
      Array(site.data["learning_paths"]).each do |path|
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
    full_document = [
      "# #{site.config["title"]} — Full article corpus",
      "",
      "> #{site.config["description"].to_s.strip}",
      "",
      "Canonical site: #{site.config["url"]}#{site.config["baseurl"]}",
      ""
    ]

    site.posts.docs.each do |post|
      slug = ContentIntelligence.slug_for(post)
      markdown = ContentIntelligence.raw_markdown(post, site)
      destination = File.join(site.dest, "posts", slug, "index.md")
      FileUtils.mkdir_p(File.dirname(destination))
      File.write(destination, markdown)
      full_document << markdown
      full_document << "\n---\n"
    end

    File.write(File.join(site.dest, "llms-full.txt"), full_document.join("\n"))
  end
end
