/**
 * Homepage / trips search: form validation + Jalali datepicker on #starttime.
 * Requires: jQuery, jdate (window.JDate), jalali-datepicker.js
 */
'use strict';

var HERO_SERVICE_META = {
  intercity: {
    title: 'سواری بین شهری',
    subtitle: 'مستر شوفر — سواری بین‌شهری و ترانسفر فرودگاهی با رانندگان تأییدشده',
    icon: 'ti-route'
  },
  airport: {
    title: 'ترانسفر فرودگاهی',
    subtitle: 'ترانسفر اختصاصی فرودگاهی با رانندگان تأییدشده',
    icon: 'ti-plane-departure'
  },
  vip: {
    title: 'بیزینس پلاس',
    subtitle: 'سرویس ویژه سازمانی با خودرو و راننده منتخب',
    icon: 'ti-briefcase'
  }
};

function ensureJalaliDatepicker() {
  var dateInput = document.getElementById('starttime');
  if (!dateInput || dateInput._jalaliDatepicker || !window.JalaliDatepicker) return;
  dateInput._jalaliDatepicker = new JalaliDatepicker(dateInput, { minDate: 'today' });
}

function setHeroStageArt(service) {
  var stage = document.querySelector('[data-hero-stage]');
  if (!stage) return;

  // Card tabs only toggle intercity ↔ airport art; other focuses keep intercity.
  var artKey = service === 'airport' ? 'airport' : 'intercity';
  if (stage.getAttribute('data-hero-art') === artKey) return;

  stage.setAttribute('data-hero-art', artKey);
  var layers = stage.querySelectorAll('[data-hero-art-layer]');
  layers.forEach(function (img) {
    var active = img.getAttribute('data-hero-art-layer') === artKey;
    img.classList.toggle('is-active', active);
    // Nudge decode if the inactive layer hasn't painted yet
    if (active && img.decode) {
      img.decode().catch(function () { /* ignore */ });
    }
  });
}

function setHeroService(service) {
  if (!HERO_SERVICE_META[service]) return;

  var tabs = document.querySelectorAll('[data-hero-service-tabs] [role="tab"]');
  tabs.forEach(function (tab) {
    var selected = tab.getAttribute('data-service') === service;
    tab.classList.toggle('active', selected);
    tab.setAttribute('aria-selected', selected ? 'true' : 'false');
    tab.setAttribute('tabindex', selected ? '0' : '-1');
  });

  var panel = document.querySelector('.hero-search-panel');
  if (panel) panel.setAttribute('data-hero-service', service);

  var focusInput = document.getElementById('tripFocusInput');
  if (focusInput) focusInput.value = service;

  var meta = HERO_SERVICE_META[service];
  var titleText = document.querySelector('[data-hero-service-title-text]');
  var subtitle = document.querySelector('[data-hero-service-subtitle]');
  var icon = document.querySelector('[data-hero-service-icon]');
  if (titleText) titleText.textContent = meta.title;
  if (subtitle) subtitle.textContent = meta.subtitle;
  if (icon) {
    icon.className = 'ti ' + meta.icon + ' hero-service-meta__icon';
  }

  setHeroStageArt(service);

  // Keep sidebar "سفر جدید" children in sync without a full reload
  document.querySelectorAll('.front-side-menu a[href*="tripFocus="]').forEach(function (link) {
    var href = link.getAttribute('href') || '';
    var match = href.match(/[?&]tripFocus=([^&]+)/i);
    var focus = match ? decodeURIComponent(match[1]).toLowerCase() : '';
    var item = link.closest('.menu-item');
    if (!item) return;
    item.classList.toggle('active', focus === service);
  });

  try {
    var url = new URL(window.location.href);
    url.searchParams.set('tripFocus', service);
    window.history.replaceState({}, '', url.pathname + url.search + url.hash);
  } catch (err) {
    /* ignore */
  }
}

// The card tab bar only exposes intercity/airport. If the page was opened with
// a service that has no matching tab (e.g. tripFocus=vip from the sidebar),
// default the card tabs to intercity so the tablist has a sensible, focusable
// selection. The tab stays clickable (aria-selected="false") so switching works.
function ensureCardTabSelection(root) {
  var tabs = Array.prototype.slice.call(root.querySelectorAll('[role="tab"]'));
  if (!tabs.length) return;
  var hasSelected = tabs.some(function (tab) {
    return tab.getAttribute('aria-selected') === 'true';
  });
  if (hasSelected) return;

  var fallback = root.querySelector('[data-service="intercity"]') || tabs[0];
  tabs.forEach(function (tab) {
    var isFallback = tab === fallback;
    tab.classList.toggle('active', isFallback);
    tab.setAttribute('tabindex', isFallback ? '0' : '-1');
  });
}

function initHeroServiceTabs() {
  var root = document.querySelector('[data-hero-service-tabs]');
  if (!root) return;

  ensureCardTabSelection(root);

  root.addEventListener('click', function (e) {
    var tab = e.target.closest('[role="tab"]');
    if (!tab || !root.contains(tab)) return;
    var service = tab.getAttribute('data-service');
    if (!service || tab.getAttribute('aria-selected') === 'true') return;
    setHeroService(service);
  });

  root.addEventListener('keydown', function (e) {
    var tabs = Array.prototype.slice.call(root.querySelectorAll('[role="tab"]'));
    var current = document.activeElement;
    var index = tabs.indexOf(current);
    if (index < 0) return;

    var next = index;
    if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
      // RTL: physical ArrowLeft moves toward next visually
      var dir = document.documentElement.getAttribute('dir') === 'rtl' ? -1 : 1;
      if (e.key === 'ArrowLeft') next = index - dir;
      if (e.key === 'ArrowRight') next = index + dir;
    } else if (e.key === 'Home') {
      next = 0;
    } else if (e.key === 'End') {
      next = tabs.length - 1;
    } else {
      return;
    }

    e.preventDefault();
    next = (next + tabs.length) % tabs.length;
    tabs[next].focus();
    setHeroService(tabs[next].getAttribute('data-service'));
  });
}

$(function () {
  $('#tripForm').on('submit', function (e) {
    var isValid = true;
    $(this).find('input').each(function () {
      if (this.type === 'hidden') return;
      if (($(this).val() || '').trim() === '') {
        isValid = false;
        $(this).focus();
        return false;
      }
    });
    if (!isValid) {
      e.preventDefault();
    }
  });

  ensureJalaliDatepicker();
  initHeroServiceTabs();

  $(document).on('focus click', '#starttime', function () {
    ensureJalaliDatepicker();
  });

  $(document).on('click', '.starttimeselector, .ti-calendar-stats', function () {
    ensureJalaliDatepicker();
    var dateInput = document.getElementById('starttime');
    if (dateInput) dateInput.focus();
  });
});
