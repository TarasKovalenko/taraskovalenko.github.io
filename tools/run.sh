#!/usr/bin/env bash

set -eu

production=false
host="127.0.0.1"

help() {
  echo "Run the local Jekyll development server"
  echo
  echo "Usage: bash $0 [options]"
  echo
  echo "Options:"
  echo "  -H, --host HOST   Host to bind to (default: 127.0.0.1)"
  echo "  -p, --production  Use the production environment"
  echo "  -h, --help        Show this help"
}

while (($#)); do
  case "$1" in
  -H | --host)
    host="$2"
    shift 2
    ;;
  -p | --production)
    production=true
    shift
    ;;
  -h | --help)
    help
    exit 0
    ;;
  *)
    echo "Unknown option: $1"
    help
    exit 1
    ;;
  esac
done

jekyll_args=(serve --livereload --host "$host")

if [[ -e /proc/1/cgroup ]] && grep -q docker /proc/1/cgroup; then
  jekyll_args+=(--force_polling)
fi

if $production; then
  JEKYLL_ENV=production bundle exec jekyll "${jekyll_args[@]}"
else
  bundle exec jekyll "${jekyll_args[@]}"
fi
