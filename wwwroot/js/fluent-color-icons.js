/**
 * Load Fluent UI System Color Icons (Iconify) from the local pack.
 * Pack: @iconify-json/fluent-color → /vendor/iconify/fluent-color.json
 * Usage: <iconify-icon class="fc-icon menu-icon" icon="fluent-color:people-24" width="22" height="22" aria-hidden="true"></iconify-icon>
 */
(function () {
  var scriptEl = document.currentScript;
  var dataPack = scriptEl && scriptEl.getAttribute("data-pack");

  function register(data) {
    if (!data || !data.prefix) return;
    if (typeof Iconify !== "undefined" && typeof Iconify.addCollection === "function") {
      Iconify.addCollection(data);
      return;
    }
    var api = window.IconifyIcon || window.Iconify;
    if (api && typeof api.addCollection === "function") {
      api.addCollection(data);
    }
  }

  function packUrl() {
    if (dataPack) return dataPack;
    var assets = document.documentElement.getAttribute("data-assets-path") || "/";
    if (assets.slice(-1) !== "/") assets += "/";
    return assets + "vendor/iconify/fluent-color.json";
  }

  function loadPack() {
    fetch(packUrl(), { credentials: "same-origin" })
      .then(function (r) { return r.json(); })
      .then(register)
      .catch(function () { /* Iconify API online fallback */ });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", loadPack);
  } else {
    loadPack();
  }
})();
