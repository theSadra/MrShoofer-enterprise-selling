/**
 * Front landing vertical menu.
 * Front pages use front-main.js (no Menu init) — init Vuexy Menu here.
 */
'use strict';

(function () {
  var LOADING_CLEAR_MS = 2800;
  var loadingClearTimers = new WeakMap();

  function isVoidHref(href) {
    var value = (href || '').trim().toLowerCase();
    return !value || value === '#' || value.indexOf('javascript:') === 0;
  }

  function isNavigableMenuLink(link) {
    if (!link || link.classList.contains('menu-toggle')) return false;
    return !isVoidHref(link.getAttribute('href'));
  }

  function clearMenuLinkLoading(link) {
    if (!link) return;
    link.classList.remove('is-loading');
    link.removeAttribute('aria-busy');
    var spinner = link.querySelector('.menu-link-spinner');
    if (spinner) spinner.remove();
    var timer = loadingClearTimers.get(link);
    if (timer) {
      clearTimeout(timer);
      loadingClearTimers.delete(link);
    }
  }

  function clearAllMenuLinkLoading(root) {
    var scope = root || document;
    scope.querySelectorAll('.front-side-menu a.menu-link.is-loading').forEach(clearMenuLinkLoading);
  }

  function setMenuLinkLoading(link) {
    if (!link || link.classList.contains('is-loading')) return;

    clearAllMenuLinkLoading(link.closest('.front-side-menu') || document);

    link.classList.add('is-loading');
    link.setAttribute('aria-busy', 'true');

    if (!link.querySelector('.menu-link-spinner')) {
      var icon = link.querySelector('img.menu-icon-3d, i.menu-icon');
      var spinner = document.createElement('i');
      spinner.className = 'ti ti-loader-2 menu-link-spinner';
      spinner.setAttribute('aria-hidden', 'true');
      if (icon && icon.parentNode === link) {
        icon.insertAdjacentElement('afterend', spinner);
      } else {
        link.insertBefore(spinner, link.firstChild);
      }
    }

    var timer = setTimeout(function () {
      clearMenuLinkLoading(link);
    }, LOADING_CLEAR_MS);
    loadingClearTimers.set(link, timer);
  }

  function shouldShowNavLoading(event, link) {
    if (event.defaultPrevented) return false;
    if (event.button != null && event.button !== 0) return false;
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return false;
    if (link.target && link.target !== '' && link.target !== '_self') return false;
    return isNavigableMenuLink(link);
  }

  function bindMenuLinkLoading(layoutMenuEl) {
    layoutMenuEl.addEventListener('click', function (event) {
      var link = event.target.closest('a.menu-link');
      if (!link || !layoutMenuEl.contains(link)) return;
      if (!shouldShowNavLoading(event, link)) return;
      setMenuLinkLoading(link);
    });

    window.addEventListener('pageshow', function () {
      clearAllMenuLinkLoading(layoutMenuEl);
    });
  }

  /**
   * Vuexy Menu always attaches PerfectScrollbar to `.menu-inner`.
   * PS mis-measures this flex column (header + nav + account footer) and adds
   * ~100–150px of empty scroll. Prefer native overflow on `.menu-inner` only.
   */
  function neutralizeMenuPerfectScrollbar(menuInstance, layoutMenuEl) {
    function cleanup() {
      if (menuInstance && menuInstance._scrollbar) {
        try {
          menuInstance._scrollbar.destroy();
        } catch (err) {
          /* ignore */
        }
        menuInstance._scrollbar = null;
      }

      if (window.Helpers) {
        window.Helpers.menuPsScroll = null;
      }

      var inner = layoutMenuEl.querySelector('.menu-inner');
      if (!inner) return;

      inner.classList.remove('ps', 'ps__rtl', 'ps--active-y', 'ps--active-x');
      inner.querySelectorAll('.ps__rail-x, .ps__rail-y').forEach(function (rail) {
        rail.remove();
      });
    }

    cleanup();
    requestAnimationFrame(cleanup);

    if (!layoutMenuEl.dataset.fsmPsNeutralized) {
      layoutMenuEl.dataset.fsmPsNeutralized = '1';
      window.addEventListener('resize', function () {
        requestAnimationFrame(cleanup);
      });
    }
  }

  function initFrontSideMenu() {
    var layoutMenuEl = document.querySelector('#layout-menu');
    if (!layoutMenuEl || !layoutMenuEl.classList.contains('front-side-menu')) return;

    if (!layoutMenuEl.dataset.fsmNavLoadingBound) {
      layoutMenuEl.dataset.fsmNavLoadingBound = '1';
      bindMenuLinkLoading(layoutMenuEl);
    }

    if (typeof Menu === 'undefined') return;
    if (window.Helpers && window.Helpers.mainMenu) {
      neutralizeMenuPerfectScrollbar(window.Helpers.mainMenu, layoutMenuEl);
      return;
    }

    try {
      // eslint-disable-next-line no-new
      var menu = new Menu(layoutMenuEl, {
        orientation: 'vertical',
        closeChildren: false
      });

      neutralizeMenuPerfectScrollbar(menu, layoutMenuEl);

      if (window.Helpers) {
        window.Helpers.mainMenu = menu;
        if (window.Helpers.scrollToActive) {
          window.Helpers.scrollToActive(false);
        }
      }
    } catch (err) {
      console.warn('[front-side-menu] Menu init failed', err);
    }

    document.querySelectorAll('.layout-menu-toggle').forEach(function (el) {
      el.addEventListener('click', function (e) {
        e.preventDefault();
        if (window.Helpers && window.Helpers.toggleCollapsed) {
          window.Helpers.toggleCollapsed();
        }
      });
    });

    layoutMenuEl.querySelectorAll('a.menu-link:not(.menu-toggle)').forEach(function (link) {
      link.addEventListener('click', function () {
        if (window.matchMedia('(max-width: 991.98px)').matches) {
          window.Helpers && window.Helpers.setCollapsed && window.Helpers.setCollapsed(true, true);
        }
      });
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initFrontSideMenu);
  } else {
    initFrontSideMenu();
  }
})();
