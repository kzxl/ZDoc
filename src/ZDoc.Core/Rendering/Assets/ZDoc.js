// ZeroDoc — client-side interactivity: theme, search, navigation.
(function () {
  "use strict";

  // ---- Theme (persisted, respects system preference) ----
  var THEME_KEY = "zerodoc-theme";
  function applyTheme(theme) {
    document.documentElement.setAttribute("data-theme", theme);
    try { localStorage.setItem(THEME_KEY, theme); } catch (e) {}
    var btn = document.getElementById("theme-toggle");
    if (btn) {
      btn.textContent = theme === "dark" ? "\u2600" : "\u263d";
      btn.setAttribute("aria-label", theme === "dark" ? "Switch to light theme" : "Switch to dark theme");
    }
  }
  function initTheme() {
    var stored = null;
    try { stored = localStorage.getItem(THEME_KEY); } catch (e) {}
    if (!stored) {
      stored = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
    }
    applyTheme(stored);
  }

  // ---- View routing: show one type at a time via hash ----
  function showView(id) {
    var panels = document.querySelectorAll(".content");
    var found = false;
    panels.forEach(function (p) {
      var match = p.id === id;
      p.classList.toggle("hidden", !match);
      if (match) found = true;
    });
    if (!found) {
      // Fall back to landing.
      var landing = document.getElementById("view-landing");
      if (landing) landing.classList.remove("hidden");
    }
    document.querySelectorAll(".nav-type").forEach(function (a) {
      a.classList.toggle("active", a.getAttribute("data-target") === id);
    });
    closeSidebar();
  }

  function routeFromHash() {
    var hash = window.location.hash.replace(/^#/, "");
    // Anchor to a member: keep the type view, let the browser scroll.
    var typeId = hash.indexOf("/") >= 0 ? "view-" + hash.split("/")[0] : (hash ? "view-" + hash : "view-landing");
    showView(typeId);
    if (hash.indexOf("/") >= 0) {
      var el = document.getElementById(hash.split("/")[1]);
      if (el) el.scrollIntoView();
    }
  }

  // ---- Fuzzy String Matching (Levenshtein + Acronym + Substring) ----
  function levenshtein(a, b) {
    if (a === b) return 0;
    if (a.length === 0) return b.length;
    if (b.length === 0) return a.length;
    var matrix = [];
    for (var i = 0; i <= b.length; i++) matrix[i] = [i];
    for (var j = 0; j <= a.length; j++) matrix[0][j] = j;
    for (var i = 1; i <= b.length; i++) {
      for (var j = 1; j <= a.length; j++) {
        if (b.charAt(i - 1) === a.charAt(j - 1)) {
          matrix[i][j] = matrix[i - 1][j - 1];
        } else {
          matrix[i][j] = Math.min(
            matrix[i - 1][j - 1] + 1, // substitution
            matrix[i][j - 1] + 1,     // insertion
            matrix[i - 1][j] + 1      // deletion
          );
        }
      }
    }
    return matrix[b.length][a.length];
  }

  function fuzzyMatch(query, target) {
    if (!query) return true;
    var q = query.toLowerCase();
    var t = target.toLowerCase();
    // 1. Direct substring
    if (t.indexOf(q) >= 0) return true;

    // 2. Acronym match (e.g. "HC" -> "HttpClient")
    var words = t.split(/[\s\.\-_]+/);
    if (words.length > 1) {
      var acronym = words.map(function (w) { return w[0] || ""; }).join("");
      if (acronym.indexOf(q) >= 0) return true;
    }

    // 3. Typo tolerance: Levenshtein distance <= 1 for short queries, <= 2 for longer
    var maxDist = q.length <= 4 ? 1 : 2;
    for (var k = 0; k < words.length; k++) {
      var w = words[k];
      if (Math.abs(w.length - q.length) <= maxDist) {
        if (levenshtein(q, w) <= maxDist) return true;
      }
    }
    return false;
  }

  // ---- Search (filters sidebar type list; matches name + summary) ----
  function initSearch() {
    var input = document.getElementById("search");
    if (!input) return;
    var items = Array.prototype.slice.call(document.querySelectorAll(".nav-type"));
    var namespaces = Array.prototype.slice.call(document.querySelectorAll(".nav-ns"));
    var noResults = document.getElementById("no-results");
    var activeIdx = -1;

    function getVisibleItems() {
      return items.filter(function (it) {
        return it.style.display !== "none";
      });
    }

    function setHighlight(idx) {
      var vis = getVisibleItems();
      vis.forEach(function (el) { el.classList.remove("highlighted"); });
      if (idx >= 0 && idx < vis.length) {
        activeIdx = idx;
        vis[activeIdx].classList.add("highlighted");
        if (vis[activeIdx].scrollIntoView) {
          vis[activeIdx].scrollIntoView({ block: "nearest" });
        }
      } else {
        activeIdx = -1;
      }
    }

    function run() {
      var q = input.value.trim().toLowerCase();
      var anyVisible = false;
      activeIdx = -1;
      items.forEach(function (it) {
        it.classList.remove("highlighted");
        var hay = (it.getAttribute("data-search") || it.textContent).toLowerCase();
        var show = q === "" || fuzzyMatch(q, hay);
        it.style.display = show ? "" : "none";
        if (show) anyVisible = true;
      });
      // Hide namespace headers with no visible children.
      namespaces.forEach(function (ns) {
        var visible = ns.querySelectorAll('.nav-type:not([style*="display: none"])').length > 0;
        ns.style.display = visible ? "" : "none";
      });
      if (noResults) noResults.style.display = anyVisible ? "none" : "block";
    }

    var t;
    input.addEventListener("input", function () {
      clearTimeout(t);
      t = setTimeout(run, 100); // debounce
    });

    // Keyboard navigation (Arrows, Enter) within search input
    input.addEventListener("keydown", function (e) {
      var vis = getVisibleItems();
      if (e.key === "ArrowDown") {
        e.preventDefault();
        if (vis.length > 0) {
          setHighlight(activeIdx < vis.length - 1 ? activeIdx + 1 : 0);
        }
      } else if (e.key === "ArrowUp") {
        e.preventDefault();
        if (vis.length > 0) {
          setHighlight(activeIdx > 0 ? activeIdx - 1 : vis.length - 1);
        }
      } else if (e.key === "Enter") {
        e.preventDefault();
        if (activeIdx >= 0 && activeIdx < vis.length) {
          vis[activeIdx].click();
          input.blur();
        } else if (vis.length > 0) {
          vis[0].click();
          input.blur();
        }
      }
    });

    // "/" focuses search; Esc clears.
    document.addEventListener("keydown", function (e) {
      if (e.key === "/" && document.activeElement !== input) {
        e.preventDefault(); input.focus(); input.select();
      } else if (e.key === "Escape" && document.activeElement === input) {
        input.value = ""; run(); input.blur();
      }
    });
  }

  // ---- Mobile sidebar ----
  function closeSidebar() {
    var sb = document.getElementById("sidebar");
    if (sb) sb.classList.remove("open");
  }
  function initMenu() {
    var btn = document.getElementById("menu-btn");
    var sb = document.getElementById("sidebar");
    if (btn && sb) {
      btn.addEventListener("click", function () { sb.classList.toggle("open"); });
    }
  }

  // ---- Copy buttons on code blocks ----
  function initCopy() {
    document.querySelectorAll("pre.code").forEach(function (pre) {
      var btn = document.createElement("button");
      btn.className = "copy-btn";
      btn.type = "button";
      btn.textContent = "Copy";
      btn.addEventListener("click", function () {
        var text = pre.querySelector("code") ? pre.querySelector("code").innerText : pre.innerText;
        navigator.clipboard && navigator.clipboard.writeText(text).then(function () {
          btn.textContent = "Copied"; setTimeout(function () { btn.textContent = "Copy"; }, 1200);
        });
      });
      pre.style.position = "relative";
      pre.appendChild(btn);
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    initTheme();
    initSearch();
    initMenu();
    initCopy();
    var toggle = document.getElementById("theme-toggle");
    if (toggle) toggle.addEventListener("click", function () {
      var cur = document.documentElement.getAttribute("data-theme");
      applyTheme(cur === "dark" ? "light" : "dark");
    });
    window.addEventListener("hashchange", routeFromHash);
    routeFromHash();
  });
})();
