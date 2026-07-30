#!/usr/bin/env ruby
# frozen_string_literal: true

require "date"
require "digest"
require "fileutils"
require "json"
require "net/http"
require "time"
require "tmpdir"
require "uri"
require "yaml"

SOURCE_DIR = File.expand_path("../_posts", __dir__)
DESTINATION_DIR = File.expand_path("../_posts_en", __dir__)
CACHE_PATH = File.join(Dir.tmpdir, "taraskovalenko-uk-en-translation-cache.json")
TRANSLATE_URI = URI("https://clients5.google.com/translate_a/t?client=dict-chrome-ex&sl=uk&tl=en")
CYRILLIC = /[А-Яа-яІіЇїЄєҐґ]/
MAX_CHUNK = 3_500

def normalize_inline_code(text)
  text.gsub(/(?<!`)`([^`\n]+)`(?!`)/) do
    code = Regexp.last_match(1).strip
    before = Regexp.last_match.pre_match[-1]
    after = Regexp.last_match.post_match[0]
    token = "`#{code}`"
    token = " #{token}" if before&.match?(/[A-Za-z0-9]/)
    token = "#{token} " if after&.match?(/[A-Za-z0-9]/)
    token
  end
end

def polish_english(text)
  polished = text
    .gsub(%r{https://taraskovalenko\.github\.io/posts/}, "https://taraskovalenko.github.io/en/posts/")
    .gsub(%r{\]\(/posts/}, "](/en/posts/")
    .gsub(/(?<![A-Za-z0-9])\.net\b/i, ".NET")
    .gsub("Fluent Validation", "FluentValidation")
    .gsub("From chatbot to action subject", "From chatbot to an agent that can act")
    .gsub(
      "LLM can suggest an action, but there should not be a security boundary that decides whether this action is allowed to be performed.",
      "An LLM may suggest an action, but it must never be the security boundary that decides whether that action is allowed."
    )
    .gsub("Model error mostly affects", "A model error mostly affects")
    .gsub("read user appeals", "read support requests")
    .gsub("receive data about the client", "retrieve customer data")
    .gsub("The right to perform actions", "Permission to perform actions")
    .gsub('H["Man"]', 'H["Human"]')
    .gsub("Form available opportunity models", "Define the capabilities available to the model")
    .gsub("Medium period (2015-2019)", "Transitional period (2015–2019)")
    .gsub("Complication of code support", "Higher maintenance complexity")
    .gsub("Consider an example of the cycle `for`:", "Consider a `for` loop:")
    .gsub("certificate.First", "certificate.\n\nFirst")
    .gsub("life cycle.- The", "life cycle.\n\n- The")
    .gsub("side effects.It", "side effects. It")
    .gsub("### Detection and recovery- [ ]", "### Detection and recovery\n\n- [ ]")
    .gsub("Sensitive payloads are edited", "Sensitive payloads are redacted")
    .gsub("Abuse cases are triggered", "Abuse-case tests run")
    .gsub("Prompt injection is not important in itself", "Prompt injection is not dangerous on its own")
  normalize_inline_code(polished)
end

class Translator
  def initialize
    @cache = File.exist?(CACHE_PATH) ? JSON.parse(File.read(CACHE_PATH)) : {}
    @requests = 0
  rescue JSON::ParserError
    @cache = {}
    @requests = 0
  end

  attr_reader :requests

  def translate(text)
    return text unless text.match?(CYRILLIC)
    cache_key = text.include?("|") ? "pipe-protected-v2:#{text}" : text
    return @cache[cache_key] if @cache.key?(cache_key)

    protected_text, tokens = protect(text)
    translated = request(protected_text)
    restored = restore(translated, tokens)
    @cache[cache_key] = restored
    persist_cache
    restored
  end

  private

  def protect(text)
    tokens = {}
    protected_text = text.gsub(
      /`[^`\n]+`|https?:\/\/[^\s)>]+|\{:[^}\n]+\}|\{\{.*?\}\}|\{%.*?%\}|\{[^{}\n]{1,100}\}|\|/
    ) do |value|
      token = "ZXQTK#{tokens.length.to_s.rjust(5, "0")}QXZ"
      tokens[token] = value
      token
    end
    [protected_text, tokens]
  end

  def restore(text, tokens)
    tokens.each do |token, value|
      unless text.include?(token)
        flexible_token = token.chars.map { |character| Regexp.escape(character) }.join("\\s*")
        raise "Translation placeholder was changed: #{token}" unless text.match?(flexible_token)

        text = text.sub(/#{flexible_token}/, token)
      end
      text = text.gsub(token, value)
    end
    text
  end

  def request(text)
    attempts = 0
    begin
      attempts += 1
      request = Net::HTTP::Post.new(TRANSLATE_URI)
      request.set_form_data("q" => text)

      response = Net::HTTP.start(
        TRANSLATE_URI.host,
        TRANSLATE_URI.port,
        use_ssl: true,
        open_timeout: 15,
        read_timeout: 45
      ) { |http| http.request(request) }

      unless response.is_a?(Net::HTTPSuccess)
        raise "Translation request failed: HTTP #{response.code}"
      end

      @requests += 1
      payload = JSON.parse(response.body)
      sleep(0.12)
      if payload.is_a?(Array) && payload.first.is_a?(String)
        payload.join
      else
        payload.fetch(0).map { |segment| segment.fetch(0) }.join
      end
    rescue StandardError => error
      raise if attempts >= 5

      warn "Retrying translation after #{error.message} (attempt #{attempts})"
      sleep(2**attempts)
      retry
    end
  end

  def persist_cache
    File.write(CACHE_PATH, JSON.generate(@cache))
  end
end

def slug_for(path)
  File.basename(path, ".md").sub(/^\d{4}-\d{2}-\d{2}-/, "")
end

def reviewed_translation?(path)
  return false unless File.file?(path)

  source = File.read(path)
  match = source.match(/\A---\s*\n(.*?)^---\s*$/m)
  return false unless match

  data = YAML.safe_load(
    match[1],
    permitted_classes: [Date, Time],
    aliases: true
  ) || {}
  data["translation_status"] == "reviewed"
end

def chunks_for(text)
  return [text] if text.length <= MAX_CHUNK

  parts = text.split(/(\n{2,})/)
  chunks = []
  current = +""

  parts.each do |part|
    if !current.empty? && current.length + part.length > MAX_CHUNK
      chunks << current
      current = +""
    end

    if part.length > MAX_CHUNK
      part.scan(/.{1,#{MAX_CHUNK}}(?:\s+|\z)/m).each do |slice|
        chunks << current unless current.empty?
        current = +""
        chunks << slice
      end
    else
      current << part
    end
  end

  chunks << current unless current.empty?
  chunks
end

def translate_prose(text, translator)
  chunks_for(text).map { |chunk| translator.translate(chunk) }.join
end

def translate_mermaid(source, translator)
  translated = source.gsub(/"([^"\n]*#{CYRILLIC.source}[^"\n]*)"/) do
    %("#{translator.translate(Regexp.last_match(1))}")
  end
  translated = translated.gsub(/\[([^\]\n]*#{CYRILLIC.source}[^\]\n]*)\]/) do
    "[#{translator.translate(Regexp.last_match(1))}]"
  end
  translated = translated.gsub(/\{([^}\n]*#{CYRILLIC.source}[^}\n]*)\}/) do
    "{#{translator.translate(Regexp.last_match(1))}}"
  end
  translated = translated.gsub(/\|([^|\n]*#{CYRILLIC.source}[^|\n]*)\|/) do
    "|#{translator.translate(Regexp.last_match(1))}|"
  end
  translated.lines.map do |line|
    next line unless line.match?(CYRILLIC)

    content = line.chomp
    ending = line.end_with?("\n") ? "\n" : ""
    if content =~ /\A(\s*participant\s+\S+\s+as\s+)(.+)\z/
      "#{Regexp.last_match(1)}#{translator.translate(Regexp.last_match(2))}#{ending}"
    elsif content =~ /\A(\s*(?:Note\s+over\s+[^:]+|[A-Za-z0-9_,]+[-.>]+[A-Za-z0-9_,]+):\s*)(.+)\z/
      "#{Regexp.last_match(1)}#{translator.translate(Regexp.last_match(2))}#{ending}"
    elsif content =~ /\A(\s*subgraph\s+)(.+)\z/
      "#{Regexp.last_match(1)}#{translator.translate(Regexp.last_match(2))}#{ending}"
    else
      line
    end
  end.join
end

def translate_code(source, translator, language)
  translated = source.lines.map do |line|
    if %w[yaml yml].include?(language) && line.match?(CYRILLIC) && line =~ /(\s+#\s?)(.*)/
      prefix = line[0...Regexp.last_match.begin(1)] + Regexp.last_match(1)
      "#{prefix}#{translator.translate(Regexp.last_match(2))}\n"
    elsif line.match?(CYRILLIC) && line =~ %r{(\s*//+\s?)(.*)}
      prefix = Regexp.last_match(1)
      comment = Regexp.last_match(2)
      line.sub(%r{\s*//+\s?.*}, "#{prefix}#{translator.translate(comment)}")
    else
      line
    end
  end.join

  translated.gsub(/"((?:\\.|[^"\\])*#{CYRILLIC.source}(?:\\.|[^"\\])*)"/) do
    %("#{translator.translate(Regexp.last_match(1))}")
  end
end

def translate_body(body, translator)
  translated_body = body.split(/(^[ \t]*```.*?^[ \t]*```[ \t]*$)/m).map do |segment|
    next translate_prose(segment, translator) unless segment.lstrip.start_with?("```")

    language = segment.lines.first.strip.sub("```", "").strip.downcase
    translated = if language == "mermaid"
      translate_mermaid(segment, translator)
    elsif language == "text"
      lines = segment.lines
      "#{lines.first}#{translator.translate(lines[1...-1].join).rstrip}\n#{lines.last}"
    elsif %w[cs csharp javascript js typescript ts java cpp c python powershell bash shell yaml yml json il assembly].include?(language)
      translate_code(segment, translator, language)
    else
      segment
    end
    "\n\n#{translated.strip}\n\n"
  end.join

  translated_body = translated_body.gsub(/`([^`\n]*#{CYRILLIC.source}[^`\n]*)`/) do
    "`#{translator.translate(Regexp.last_match(1))}`"
  end

  in_fence = false
  translated_body.lines.map do |line|
    in_fence = !in_fence if line.match?(/^\s*```/)
    if !in_fence && line.match?(CYRILLIC)
      "#{translator.translate(line.chomp)}\n"
    else
      line
    end
  end.join
end

def english_front_matter(front_matter, slug, translator)
  data = YAML.safe_load(
    front_matter,
    permitted_classes: [Date, Time],
    aliases: true
  ) || {}
  data["title"] = polish_english(translator.translate(data.fetch("title").to_s))
  data["lang"] = "en"
  data["locale"] = "en_US"
  data["translation_key"] = slug
  data["permalink"] = "/en/posts/#{slug}/"
  YAML.dump(data).sub(/\A---\s*\n/, "")
end

translator = Translator.new
FileUtils.mkdir_p(DESTINATION_DIR)
force = ARGV.delete("--force")

Dir.glob(File.join(SOURCE_DIR, "*.md")).sort.each do |source_path|
  source = File.read(source_path)
  match = source.match(/\A---\s*\n(.*?)^---\s*$\n?(.*)\z/m)
  raise "Invalid front matter: #{source_path}" unless match

  slug = slug_for(source_path)
  destination = File.join(DESTINATION_DIR, "#{slug}.md")
  if reviewed_translation?(destination) && !force
    puts "Skipped reviewed translation #{slug}"
    next
  end

  front_matter = english_front_matter(match[1], slug, translator)
  body = polish_english(translate_body(match[2], translator))
  File.write(destination, "---\n#{front_matter}---\n\n#{body.sub(/\A\s+/, "")}")
  puts "Translated #{slug}"
end

puts "Created #{Dir.glob(File.join(DESTINATION_DIR, "*.md")).length} English posts"
puts "Translation requests: #{translator.requests}"
