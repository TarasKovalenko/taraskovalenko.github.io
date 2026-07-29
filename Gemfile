# frozen_string_literal: true

source "https://rubygems.org"

gem "jekyll", "~> 4.4"

gem "html-proofer", "~> 5.0", group: :test

group :jekyll_plugins do
  gem "jekyll-archives", "~> 2.3"
  gem "jekyll-sitemap", "~> 1.4"
end

platforms :mingw, :x64_mingw, :mswin, :jruby do
  gem "tzinfo", ">= 1", "< 3"
  gem "tzinfo-data"
end

gem "wdm", "~> 0.2.0", :platforms => [:mingw, :x64_mingw, :mswin]
