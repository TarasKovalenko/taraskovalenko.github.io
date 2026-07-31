(() => {
  const root = document.documentElement;
  const body = document.body;
  const isEnglish = root.lang === "en";
  const ui = isEnglish ? {
    lightTheme: "Use light theme",
    darkTheme: "Use dark theme",
    loading: "LOADING...",
    retry: "TRY AGAIN",
    closeMenu: "Close menu",
    openMenu: "Open menu",
    recent: "Recent searches",
    repeatSearch: "Search again",
    quickCommands: "Quick commands",
    tracks: "Topic tracks",
    guidedLearning: "Guided learning →",
    topics: "Browse topics",
    categories: "Categories and technologies →",
    changeTheme: "Change theme",
    noResults: "Nothing found. Try another technology or topic.",
    copy: "COPY",
    copied: "COPIED",
    error: "ERROR",
    linkCopied: "Link copied",
    copyLink: "Copy link",
    copyLinkFailed: "Unable to copy link",
    markdownCopied: "Markdown copied",
    markdownFailed: "Unable to copy"
  } : {
    lightTheme: "Увімкнути світлу тему",
    darkTheme: "Увімкнути темну тему",
    loading: "ЗАВАНТАЖЕННЯ...",
    retry: "СПРОБУВАТИ ЩЕ РАЗ",
    closeMenu: "Закрити меню",
    openMenu: "Відкрити меню",
    recent: "Недавні запити",
    repeatSearch: "Повторити пошук",
    quickCommands: "Швидкі команди",
    tracks: "Тематичні треки",
    guidedLearning: "Guided learning →",
    topics: "Переглянути теми",
    categories: "Категорії та технології →",
    changeTheme: "Змінити тему",
    noResults: "Нічого не знайдено. Спробуйте іншу технологію або тему.",
    copy: "КОПІЮВАТИ",
    copied: "СКОПІЙОВАНО",
    error: "ПОМИЛКА",
    linkCopied: "Посилання скопійовано",
    copyLink: "Скопіювати посилання",
    copyLinkFailed: "Не вдалося скопіювати посилання",
    markdownCopied: "Markdown скопійовано",
    markdownFailed: "Не вдалося скопіювати"
  };
  const writeClipboard = async (text) => {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      return;
    }
    const input = document.createElement("textarea");
    input.value = text;
    input.setAttribute("readonly", "");
    input.style.position = "fixed";
    input.style.opacity = "0";
    body.appendChild(input);
    input.select();
    document.execCommand("copy");
    input.remove();
  };

  const escapeHtml = (value) => String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");

  const themeToggle = document.querySelector("[data-theme-toggle]");
  const updateThemeControl = () => {
    if (!themeToggle) return;
    const dark = root.dataset.theme === "dark";
    themeToggle.setAttribute("aria-pressed", String(dark));
    themeToggle.setAttribute("aria-label", dark ? ui.lightTheme : ui.darkTheme);
  };
  updateThemeControl();
  themeToggle?.addEventListener("click", () => {
    const next = root.dataset.theme === "dark" ? "light" : "dark";
    root.dataset.theme = next;
    try { localStorage.setItem("tk-theme", next); } catch (error) {}
    updateThemeControl();
    document.dispatchEvent(new CustomEvent("tk-theme-change", { detail: { theme: next } }));
    const utterances = document.querySelector(".utterances-frame");
    utterances?.contentWindow?.postMessage({
      type: "set-theme",
      theme: next === "dark" ? "github-dark" : "github-light"
    }, "https://utteranc.es");
  });

  const commentsButton = document.querySelector("[data-comments-load]");
  commentsButton?.addEventListener("click", () => {
    commentsButton.disabled = true;
    commentsButton.textContent = ui.loading;

    const script = document.createElement("script");
    script.src = "https://utteranc.es/client.js";
    script.async = true;
    script.crossOrigin = "anonymous";
    script.setAttribute("repo", commentsButton.dataset.repo);
    script.setAttribute("issue-term", commentsButton.dataset.issueTerm);
    script.setAttribute("label", "comment");
    script.setAttribute("theme", root.dataset.theme === "dark" ? "github-dark" : "github-light");
    script.addEventListener("load", () => commentsButton.remove());
    script.addEventListener("error", () => {
      commentsButton.disabled = false;
      commentsButton.textContent = ui.retry;
    });
    commentsButton.parentElement.append(script);
  });

  const menuToggle = document.querySelector("[data-menu-toggle]");
  const mobileMenu = document.querySelector("[data-mobile-menu]");
  const setMobileMenuState = (open) => {
    menuToggle?.setAttribute("aria-expanded", String(open));
    menuToggle?.setAttribute("aria-label", open ? ui.closeMenu : ui.openMenu);
    mobileMenu?.classList.toggle("is-open", open);
    mobileMenu?.setAttribute("aria-hidden", String(!open));
    if (mobileMenu) mobileMenu.inert = !open;
    body.classList.toggle("menu-open", open);
  };
  setMobileMenuState(false);
  menuToggle?.addEventListener("click", () => {
    const open = menuToggle.getAttribute("aria-expanded") !== "true";
    setMobileMenuState(open);
  });
  window.addEventListener("resize", () => {
    if (window.innerWidth > 760 && menuToggle?.getAttribute("aria-expanded") === "true") {
      setMobileMenuState(false);
    }
  });

  const dialog = document.querySelector("[data-search-dialog]");
  const searchInput = document.querySelector("[data-search-input]");
  const results = document.querySelector("[data-search-results]");
  const searchItems = [...document.querySelectorAll("[data-search-item]")];
  const searchButtons = [...document.querySelectorAll("[data-search-open]")];
  let previousFocus = null;
  let activeSearchIndex = -1;

  const readRecentSearches = () => {
    try {
      return JSON.parse(localStorage.getItem("tk-recent-searches") || "[]").slice(0, 4);
    } catch {
      return [];
    }
  };

  const rememberSearch = (query) => {
    const value = query.trim();
    if (!value) return;
    const recent = [value, ...readRecentSearches().filter((item) => item.toLocaleLowerCase("uk") !== value.toLocaleLowerCase("uk"))].slice(0, 4);
    try { localStorage.setItem("tk-recent-searches", JSON.stringify(recent)); } catch (error) {}
  };

  const searchTokens = (query) => query
    .trim()
    .toLocaleLowerCase("uk")
    .split(/\s+/)
    .filter(Boolean);

  const escapeRegExp = (value) => value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const highlightMatch = (value, tokens) => {
    let output = escapeHtml(value);
    tokens.forEach((token) => {
      output = output.replace(new RegExp(`(${escapeRegExp(escapeHtml(token))})`, "gi"), "<mark>$1</mark>");
    });
    return output;
  };

  const searchOptions = () => [...results?.querySelectorAll("[data-search-option]") || []];
  const setActiveSearchOption = (index) => {
    const options = searchOptions();
    if (!options.length) {
      activeSearchIndex = -1;
      searchInput?.removeAttribute("aria-activedescendant");
      return;
    }
    activeSearchIndex = (index + options.length) % options.length;
    options.forEach((option, optionIndex) => {
      if (!option.id) option.id = `search-option-${optionIndex}`;
      const active = optionIndex === activeSearchIndex;
      option.classList.toggle("is-active", active);
      option.setAttribute("aria-selected", String(active));
    });
    searchInput?.setAttribute("aria-activedescendant", options[activeSearchIndex].id);
    options[activeSearchIndex].scrollIntoView({ block: "nearest" });
  };

  const renderSearchStart = () => {
    if (!results) return;
    const recent = readRecentSearches();
    const recentMarkup = recent.length
      ? `<div class="search-group"><span>${ui.recent}</span>${recent.map((query) => `<button type="button" data-search-option data-search-suggestion="${escapeHtml(query)}"><strong>${escapeHtml(query)}</strong><small>${ui.repeatSearch}</small></button>`).join("")}</div>`
      : "";
    results.innerHTML = `
      ${recentMarkup}
      <div class="search-group">
        <span>${ui.quickCommands}</span>
        <a id="search-option-paths" role="option" data-search-option href="${isEnglish ? "/en/paths/" : "/paths/"}"><strong>${ui.tracks}</strong><small>${ui.guidedLearning}</small></a>
        <a id="search-option-categories" role="option" data-search-option href="/categories/"><strong>${ui.topics}</strong><small>${ui.categories}</small></a>
        <button id="search-option-theme" role="option" type="button" data-search-option data-search-command="theme"><strong>${ui.changeTheme}</strong><small>Light / dark mode</small></button>
      </div>`;
    activeSearchIndex = -1;
    searchInput?.removeAttribute("aria-activedescendant");
  };

  const openSearch = () => {
    if (!dialog) return;
    previousFocus = document.activeElement;
    dialog.hidden = false;
    body.classList.add("search-open");
    searchButtons.forEach((button) => button.setAttribute("aria-expanded", "true"));
    if (!searchInput?.value.trim()) renderSearchStart();
    window.setTimeout(() => searchInput?.focus(), 30);
  };

  const closeSearch = () => {
    if (!dialog) return;
    dialog.hidden = true;
    body.classList.remove("search-open");
    searchButtons.forEach((button) => button.setAttribute("aria-expanded", "false"));
    if (previousFocus instanceof HTMLElement) previousFocus.focus();
  };

  searchButtons.forEach((button) => button.addEventListener("click", openSearch));
  document.querySelectorAll("[data-search-close]").forEach((button) => button.addEventListener("click", closeSearch));
  document.addEventListener("keydown", (event) => {
    if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
      event.preventDefault();
      openSearch();
    }
    if (event.key === "Escape") {
      if (dialog && !dialog.hidden) {
        closeSearch();
      } else if (menuToggle?.getAttribute("aria-expanded") === "true") {
        setMobileMenuState(false);
        menuToggle.focus();
      }
    }
    if (event.key === "Tab" && dialog && !dialog.hidden) {
      const focusable = [...dialog.querySelectorAll("button:not([disabled]), [href], input:not([disabled])")]
        .filter((element) => !element.closest("[hidden]"));
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last?.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first?.focus();
      }
    }
  });

  const renderSearch = (query) => {
    if (!results) return;
    const cleanQuery = query.trim().toLocaleLowerCase("uk");
    if (!cleanQuery) {
      renderSearchStart();
      return;
    }
    const tokens = searchTokens(cleanQuery);
    const matches = searchItems
      .map((item) => {
        const title = item.dataset.title.toLocaleLowerCase("uk");
        const topics = item.dataset.topics.toLocaleLowerCase("uk");
        const path = item.dataset.path.toLocaleLowerCase("uk");
        const level = item.dataset.level.toLocaleLowerCase("uk");
        const excerpt = item.textContent.trim().toLocaleLowerCase("uk");
        let score = 0;
        if (title === cleanQuery) score += 120;
        if (title.startsWith(cleanQuery)) score += 70;
        tokens.forEach((token) => {
          if (title.includes(token)) score += 35;
          if (topics.includes(token)) score += 22;
          if (path.includes(token)) score += 18;
          if (level.includes(token)) score += 8;
          if (excerpt.includes(token)) score += 5;
        });
        if (tokens.every((token) => `${title} ${topics} ${path} ${excerpt}`.includes(token))) score += 20;
        return { item, score };
      })
      .filter(({ score }) => score > 0)
      .sort((left, right) => right.score - left.score)
      .slice(0, 8);

    results.innerHTML = matches.length
      ? matches.map(({ item }, index) => `
          <a class="search-result" id="search-result-${index}" role="option" aria-selected="false" data-search-option href="${escapeHtml(item.getAttribute("href"))}">
            <span>${escapeHtml(item.dataset.date)} · ${highlightMatch(item.dataset.topics.split(" ").slice(0, 3).join(" "), tokens)}</span>
            <strong>${highlightMatch(item.dataset.title, tokens)}</strong>
            <p>${highlightMatch(item.textContent.trim(), tokens)}</p>
          </a>`).join("")
      : `<p class="search-hint">${ui.noResults}</p>`;
    activeSearchIndex = -1;
    searchInput.removeAttribute("aria-activedescendant");
  };

  searchInput?.addEventListener("input", (event) => renderSearch(event.target.value));
  searchInput?.addEventListener("keydown", (event) => {
    if (event.key === "ArrowDown") {
      event.preventDefault();
      setActiveSearchOption(activeSearchIndex + 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActiveSearchOption(activeSearchIndex - 1);
    } else if (event.key === "Enter" && activeSearchIndex >= 0) {
      event.preventDefault();
      const option = searchOptions()[activeSearchIndex];
      rememberSearch(searchInput.value);
      option?.click();
    }
  });
  results?.addEventListener("click", (event) => {
    const option = event.target.closest("[data-search-option]");
    if (!option) return;
    if (option.dataset.searchSuggestion && searchInput) {
      event.preventDefault();
      searchInput.value = option.dataset.searchSuggestion;
      renderSearch(searchInput.value);
      searchInput.focus();
      return;
    }
    if (option.dataset.searchCommand === "theme") {
      event.preventDefault();
      themeToggle?.click();
      closeSearch();
      return;
    }
    rememberSearch(searchInput?.value || "");
  });

  const filterButtons = [...document.querySelectorAll("[data-filter]")];
  const articles = [...document.querySelectorAll("[data-article]")];
  const noResults = document.querySelector("[data-no-results]");
  filterButtons.forEach((button) => {
    button.addEventListener("click", () => {
      filterButtons.forEach((item) => {
        const active = item === button;
        item.classList.toggle("is-active", active);
        item.setAttribute("aria-pressed", String(active));
      });
      const filter = button.dataset.filter;
      let visible = 0;
      articles.forEach((article) => {
        const topics = article.dataset.topics;
        const show = filter === "all"
          || topics.includes(filter)
          || (filter === "cloud" && ["azure", "aws", "application gateway"].some((topic) => topics.includes(topic)));
        article.hidden = !show;
        if (show) visible += 1;
      });
      if (noResults) noResults.hidden = visible !== 0;
    });
  });

  const articleContent = document.querySelector("[data-article-content]");
  const toc = document.querySelector("[data-toc]");
  if (articleContent && toc) {
    const headings = [...articleContent.querySelectorAll("h2, h3")];
    headings.forEach((heading, index) => {
      if (!heading.id) heading.id = `section-${index + 1}`;
      const link = document.createElement("a");
      link.href = `#${heading.id}`;
      link.textContent = heading.textContent;
      link.className = heading.tagName === "H3" ? "toc-h3" : "toc-h2";
      toc.appendChild(link);
    });

    if ("IntersectionObserver" in window) {
      const links = [...toc.querySelectorAll("a")];
      const observer = new IntersectionObserver((entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          links.forEach((link) => link.classList.toggle("is-active", link.hash === `#${entry.target.id}`));
        });
      }, { rootMargin: "-18% 0px -72% 0px" });
      headings.forEach((heading) => observer.observe(heading));
    }
  }

  const languageNames = {
    bash: "Bash",
    c: "C",
    cpp: "C++",
    cs: "C#",
    css: "CSS",
    diff: "Diff",
    html: "HTML",
    http: "HTTP",
    java: "Java",
    javascript: "JavaScript",
    js: "JavaScript",
    json: "JSON",
    markup: "Markup",
    powershell: "PowerShell",
    python: "Python",
    ruby: "Ruby",
    shell: "Shell",
    sql: "SQL",
    text: "Text",
    ts: "TypeScript",
    typescript: "TypeScript",
    xml: "XML",
    yaml: "YAML",
    yml: "YAML"
  };

  document.querySelectorAll(".article-content div.highlighter-rouge, .article-content > pre").forEach((originalBlock) => {
    if (originalBlock.querySelector("code.language-mermaid")) return;

    let block = originalBlock;
    const pre = block.matches("pre") ? block : block.querySelector(".rouge-code pre, pre");
    if (!pre || block.querySelector(".code-toolbar")) return;

    if (block.matches("pre")) {
      const wrapper = document.createElement("div");
      wrapper.className = "code-frame";
      block.before(wrapper);
      wrapper.append(block);
      block = wrapper;
    } else {
      block.classList.add("code-frame");
    }

    const languageClass = [...originalBlock.classList, ...[...originalBlock.querySelectorAll("code")].flatMap((code) => [...code.classList])]
      .find((name) => name.startsWith("language-"));
    const language = languageClass?.replace("language-", "") || "text";
    const toolbar = document.createElement("div");
    toolbar.className = "code-toolbar";
    const label = document.createElement("span");
    label.className = "code-language";
    label.textContent = languageNames[language] || language.toUpperCase();

    const button = document.createElement("button");
    button.className = "code-copy";
    button.type = "button";
    button.textContent = ui.copy;
    button.addEventListener("click", async () => {
      try {
        await writeClipboard(pre.innerText);
        button.textContent = ui.copied;
        setTimeout(() => {
          button.textContent = ui.copy;
        }, 1400);
      } catch {
        button.textContent = ui.error;
        setTimeout(() => {
          button.textContent = ui.copy;
        }, 1400);
      }
    });
    toolbar.append(label, button);
    block.prepend(toolbar);
  });

  const progress = document.querySelector("[data-reading-progress]");
  if (progress && articleContent) {
    const updateProgress = () => {
      const start = articleContent.offsetTop;
      const distance = articleContent.offsetHeight - window.innerHeight;
      const value = Math.max(0, Math.min(1, (window.scrollY - start + 140) / distance));
      progress.style.transform = `scaleX(${value})`;
    };
    document.addEventListener("scroll", updateProgress, { passive: true });
    updateProgress();
  }

  document.querySelector("[data-share]")?.addEventListener("click", async () => {
    try {
      if (navigator.share) {
        await navigator.share({ title: document.title, url: location.href });
      } else {
        await writeClipboard(location.href);
      }
    } catch (error) {
      if (error?.name !== "AbortError") console.error("Unable to share this page.", error);
    }
  });

  document.querySelector("[data-copy-link]")?.addEventListener("click", async (event) => {
    const button = event.currentTarget;
    try {
      await writeClipboard(location.href);
      button.classList.add("is-copied");
      button.setAttribute("aria-label", ui.linkCopied);
      setTimeout(() => {
        button.classList.remove("is-copied");
        button.setAttribute("aria-label", ui.copyLink);
      }, 1400);
    } catch {
      button.setAttribute("aria-label", ui.copyLinkFailed);
    }
  });

  document.querySelector("[data-copy-markdown]")?.addEventListener("click", async (event) => {
    const button = event.currentTarget;
    const originalLabel = button.textContent;
    button.disabled = true;
    try {
      const response = await fetch(button.dataset.markdownUrl, { headers: { Accept: "text/markdown" } });
      if (!response.ok) throw new Error(`Markdown request failed: ${response.status}`);
      await writeClipboard(await response.text());
      button.textContent = ui.markdownCopied;
    } catch {
      button.textContent = ui.markdownFailed;
    }
    window.setTimeout(() => {
      button.disabled = false;
      button.textContent = originalLabel;
    }, 1600);
  });

  if ("serviceWorker" in navigator && !["localhost", "127.0.0.1"].includes(location.hostname)) {
    window.addEventListener("load", () => {
      navigator.serviceWorker.register("/sw.js").catch((error) => {
        console.error("Service worker registration failed.", error);
      });
    });
  }
})();
