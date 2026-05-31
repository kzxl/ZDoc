// DocLens — client-side interactivity: theme, search, navigation.
(function () {
  "use strict";

  // ---- Theme (persisted, respects system preference) ----
  var THEME_KEY = "doclens-theme";
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

  // ---- Search (filters sidebar type list; matches name + summary) ----
  function initSearch() {
    var input = document.getElementById("search");
    if (!input) return;
    var items = Array.prototype.slice.call(document.querySelectorAll(".nav-type"));
    var namespaces = Array.prototype.slice.call(document.querySelectorAll(".nav-ns"));
    var noResults = document.getElementById("no-results");

    function run() {
      var q = input.value.trim().toLowerCase();
      var anyVisible = false;
      items.forEach(function (it) {
        var hay = (it.getAttribute("data-search") || it.textContent).toLowerCase();
        var show = q === "" || hay.indexOf(q) >= 0;
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
      t = setTimeout(run, 120); // debounce
    });

    // "/" focuses search; Esc clears.
    document.addEventListener("keydown", function (e) {
      if (e.key === "/" && document.activeElement !== input) {
        e.preventDefault(); input.focus();
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
