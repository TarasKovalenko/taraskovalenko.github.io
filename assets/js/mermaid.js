const blocks = [...document.querySelectorAll(".article-content pre > code.language-mermaid")];

if (blocks.length) {
  const diagrams = blocks.map((code, index) => {
    const source = code.textContent.trim();
    const figure = document.createElement("figure");
    figure.className = "mermaid-figure";
    figure.setAttribute("aria-label", `Діаграма ${index + 1}`);

    const viewport = document.createElement("div");
    viewport.className = "mermaid-viewport";
    viewport.innerHTML = '<p class="mermaid-status">Будуємо діаграму…</p>';

    const details = document.createElement("details");
    details.className = "mermaid-source";
    const summary = document.createElement("summary");
    summary.textContent = "Показати Mermaid-код";
    const sourceBlock = document.createElement("pre");
    const sourceCode = document.createElement("code");
    sourceCode.textContent = source;
    sourceBlock.append(sourceCode);
    details.append(summary, sourceBlock);

    figure.append(viewport, details);
    code.parentElement.replaceWith(figure);

    return { figure, source, viewport };
  });

  let mermaid;
  let generation = 0;

  const showError = (diagram) => {
    diagram.figure.classList.add("has-error");
    diagram.viewport.innerHTML = '<p class="mermaid-status"><strong>Не вдалося побудувати діаграму.</strong><br>Mermaid-код доступний нижче.</p>';
  };

  const render = async () => {
    if (!mermaid) return;
    generation += 1;
    const dark = document.documentElement.dataset.theme === "dark";
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: "strict",
      theme: dark ? "dark" : "base",
      fontFamily: "Inter, ui-sans-serif, system-ui, sans-serif",
      themeVariables: dark ? {
        background: "#17191c",
        primaryColor: "#24272b",
        primaryTextColor: "#f3f0e8",
        primaryBorderColor: "#737b86",
        lineColor: "#aeb5bf",
        secondaryColor: "#1f3f9f",
        tertiaryColor: "#202329",
        noteBkgColor: "#252a31",
        noteTextColor: "#f3f0e8"
      } : {
        background: "#f7f5ef",
        primaryColor: "#e8edff",
        primaryTextColor: "#11110f",
        primaryBorderColor: "#2458ff",
        lineColor: "#4c5564",
        secondaryColor: "#dfe6ff",
        tertiaryColor: "#f0eee7",
        noteBkgColor: "#fff7d6",
        noteTextColor: "#11110f"
      },
      flowchart: { htmlLabels: true, curve: "basis", useMaxWidth: true },
      sequence: { useMaxWidth: true, wrap: true },
      mindmap: { useMaxWidth: true }
    });

    await Promise.all(diagrams.map(async (diagram, index) => {
      try {
        diagram.figure.classList.remove("has-error");
        const result = await mermaid.render(`tk-mermaid-${generation}-${index}`, diagram.source);
        diagram.viewport.innerHTML = result.svg;
        result.bindFunctions?.(diagram.viewport);
        const svg = diagram.viewport.querySelector("svg");
        svg?.setAttribute("role", "img");
        svg?.setAttribute("aria-label", `Діаграма ${index + 1}`);
      } catch {
        showError(diagram);
      }
    }));
  };

  try {
    ({ default: mermaid } = await import("https://cdn.jsdelivr.net/npm/mermaid@11.16.0/dist/mermaid.esm.min.mjs"));
    await render();
  } catch {
    diagrams.forEach(showError);
  }

  document.addEventListener("tk-theme-change", render);
}
