// Enhanced search functionality with normalization, dynamic origin/destination lists
// Keeps existing trip card rendering logic (Trips.js) unchanged.
// Origin/Destination UX: advanced matching, Persian guesses, supported city intersection.

let directions = [];
let originKeys = []; // normalized city keys (after intersecting with supported)
let supportedKeys = new Set(); // normalized keys supported by server (DirectionsRepository)
let displayNameByKey = new Map(); // normalized -> display name (prefer Persian)
let _destinations = []; // destination display names
let trips = []; // will be reused by Trips.js
let activeOriginKey = ''; // exact typed/clicked origin currently driving destination choices

// Decode literal \uXXXX sequences into real characters (handles double-escaped payloads)
function decodeUnicodeEscapes(str) {
  if (typeof str !== 'string') return str;
  if (!/\\u[0-9a-fA-F]{4}/.test(str)) return str;
  try {
    return str.replace(/\\u([0-9a-fA-F]{4})/g, (_, g1) => String.fromCharCode(parseInt(g1, 16)));
  } catch { return str; }
}

// Latin spellings → Persian city names (prefix-matched while typing)
const LATIN_TO_CITY = {
  tehran: 'تهران', teh: 'تهران', teheran: 'تهران',
  isfahan: 'اصفهان', esfahan: 'اصفهان', esfehan: 'اصفهان',
  rasht: 'رشت',
  chalus: 'چالوس', chaloos: 'چالوس', chalous: 'چالوس',
  kermanshah: 'کرمانشاه',
  noushahr: 'نوشهر', nowshahr: 'نوشهر', noshahr: 'نوشهر',
  tabriz: 'تبریز',
  qom: 'قم', ghom: 'قم',
  hamedan: 'همدان', hamadan: 'همدان',
  sari: 'ساری',
  shiraz: 'شیراز',
  mashhad: 'مشهد', mashad: 'مشهد',
  karaj: 'کرج',
  qazvin: 'قزوین', ghazvin: 'قزوین',
  kerman: 'کرمان',
  yazd: 'یزد',
  gorgan: 'گرگان',
  zanjan: 'زنجان',
  kashan: 'کاشان',
  sanandaj: 'سنندج',
  shahrekord: 'شهرکرد', 'shahr-e-kord': 'شهرکرد',
  lahijan: 'لاهیجان',
  ramsar: 'رامسر'
};

// Windows Persian (ISIRI) layout: typing تهران with an English keyboard produces jivhk
const QWERTY_TO_FA = {
  q: 'ض', w: 'ص', e: 'ث', r: 'ق', t: 'ف', y: 'غ', u: 'ع', i: 'ه', o: 'خ', p: 'ح',
  '[': 'ج', ']': 'چ',
  a: 'ش', s: 'س', d: 'ی', f: 'ب', g: 'ل', h: 'ا', j: 'ت', k: 'ن', l: 'م',
  ';': 'ک', "'": 'گ',
  z: 'ظ', x: 'ط', c: 'ز', v: 'ر', b: 'ذ', n: 'د', m: 'پ',
  ',': 'و', '`': 'پ', '\\': 'پ'
};

function looksLatin(str) {
  const s = str || '';
  if (!s || /[\u0600-\u06FF]/.test(s)) return false;
  return /[A-Za-z;'[\]`,\\]/.test(s);
}

function latinKeyboardToPersian(str) {
  if (!looksLatin(str)) return str;
  let out = '';
  let converted = 0;
  for (const ch of str) {
    const mapped = QWERTY_TO_FA[ch.toLowerCase()];
    if (mapped) {
      out += mapped;
      converted++;
    } else {
      out += ch;
    }
  }
  return converted ? out : str;
}

function toPersianGuess(str) {
  const trimmed = (str || '').trim();
  if (!trimmed) return str;
  const fromAlias = LATIN_TO_CITY[trimmed.toLowerCase()];
  if (fromAlias) return fromAlias;
  const fromKeys = latinKeyboardToPersian(trimmed);
  if (fromKeys !== trimmed) return fromKeys;
  return str;
}

function queryNeedles(raw) {
  const needles = [];
  const add = (value) => {
    const n = normalize(value);
    if (n && !needles.includes(n)) needles.push(n);
  };
  add(raw);
  add(toPersianGuess(raw));
  add(latinKeyboardToPersian(raw));
  const lower = (raw || '').trim().toLowerCase();
  if (lower.length >= 2 && isAscii(lower)) {
    Object.keys(LATIN_TO_CITY).forEach((alias) => {
      if (alias.startsWith(lower)) add(LATIN_TO_CITY[alias]);
    });
  }
  return needles;
}

function keysMatchingNeedles(keys, needles) {
  if (!needles.length) return [];
  return keys.filter((key) => needles.some((n) => key.includes(n)));
}

function isAscii(str) { return /^[\x00-\x7F]*$/.test(str || ''); }

// Normalize text: trim, unify Arabic/Persian chars, remove ZWNJ/diacritics, lowercase
function normalize(str) {
  try {
    return (str || '')
      .trim()
      .replace(/\(.*/, '')
      .replace(/[\u200C\u200F\u200E\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED]/g, '')
      .replace(/\u064A/g, '\u06CC')
      .replace(/\u0643/g, '\u06A9')
      .replace(/[\u0629]/g, '\u0647')
      .replace(/\s+/g, ' ')
      .toLocaleLowerCase();
  } catch { return (str || '').trim().toLocaleLowerCase(); }
}

function isMobileCityPicker() {
  return !!(document.getElementById('tripForm') && window.matchMedia('(max-width: 767.98px)').matches);
}

let desktopPickerModeActive = null;

function cityPickerDropdown(input) {
  if (!input || typeof bootstrap === 'undefined') return null;
  return bootstrap.Dropdown.getOrCreateInstance(input, {
    autoClose: 'outside',
    offset: [0, 8]
  });
}

function syncCityPickerDropdownMode() {
  const mobile = isMobileCityPicker();
  if (desktopPickerModeActive === !mobile) return;
  desktopPickerModeActive = !mobile;

  ['origin_input', 'destination_input'].forEach(function (id) {
    const el = document.getElementById(id);
    if (!el) return;
    const inst = bootstrap.Dropdown.getInstance(el);
    if (inst) inst.dispose();
    // Keep data-bs-toggle so Bootstrap clearMenus can close on outside click.
    el.setAttribute('data-bs-toggle', 'dropdown');
    el.setAttribute('data-bs-auto-close', 'outside');
    el.setAttribute('data-bs-offset', '0,8');
    if (!mobile) {
      cityPickerDropdown(el);
    }
  });
}

function isInsideOpenCityPicker(el) {
  if (!(el instanceof Element)) return false;
  if (el.closest('#origin_input, #destination_input, #origin_picker_q, #dest_picker_q')) return true;
  if (el.closest('#route-request-overlay, .route-miss-cta')) return true;
  // Only treat the currently open menu as "inside" — closed menus must not swallow dismiss clicks.
  return !!el.closest('.dropdown-menu.origin_location.show, .dropdown-menu.destination_location.show');
}

function forceHideCityPickerMenu(menu) {
  if (!menu) return;
  menu.classList.remove('show');
  menu.removeAttribute('data-bs-popper');
  menu.style.cssText = '';
  if (menu.parentElement === document.body) {
    const home = menu._cityPickerHome;
    menu.removeAttribute('data-city-picker-host');
    delete menu._cityPickerHome;
    if (home) home.appendChild(menu);
  }
}

function closeAllDesktopCityPickers() {
  closeCityPicker(document.getElementById('origin_input'));
  closeCityPicker(document.getElementById('destination_input'));
  // Belt-and-suspenders: any leftover open city menus (stale Bootstrap instance / portal).
  document.querySelectorAll('.dropdown-menu.origin_location.show, .dropdown-menu.destination_location.show')
    .forEach(forceHideCityPickerMenu);
  document.body.classList.remove('city-picker-open');
}

function openDesktopCityPicker(input) {
  if (!input || isMobileCityPicker()) return;

  const menu = input.parentElement && input.parentElement.querySelector('.dropdown-menu');
  if (menu && menu.classList.contains('show')) return;

  const otherInput = input.id === 'origin_input'
    ? document.getElementById('destination_input')
    : document.getElementById('origin_input');
  if (otherInput) closeCityPicker(otherInput);

  try {
    cityPickerDropdown(input).show();
  } catch { /* Bootstrap initializes on first interaction */ }
}

function closeCityPicker(input) {
  if (!input) return;
  try {
    const inst = typeof bootstrap !== 'undefined'
      ? (bootstrap.Dropdown.getInstance(input) || bootstrap.Dropdown.getOrCreateInstance(input, {
        autoClose: 'outside',
        offset: [0, 8]
      }))
      : null;
    if (inst) inst.hide();
  } catch { /* already closed */ }
  try {
    if (window.jQuery) window.jQuery(input).dropdown('hide');
  } catch { /* already closed */ }

  input.classList.remove('show');
  input.setAttribute('aria-expanded', 'false');

  if (input.parentElement) {
    input.parentElement
      .querySelectorAll('.dropdown-menu.origin_location, .dropdown-menu.destination_location')
      .forEach(forceHideCityPickerMenu);
  }
  const hostId = input.id || '';
  if (hostId) {
    document
      .querySelectorAll('body > .dropdown-menu[data-city-picker-host="' + hostId + '"]')
      .forEach(forceHideCityPickerMenu);
  }
}

function openCityPicker(input, force) {
  if (!input) return;
  if (isMobileCityPicker()) {
    try {
      cityPickerDropdown(input).show();
    } catch {
      try { $(input).dropdown('show'); } catch { /* Bootstrap initializes on first interaction */ }
    }
    return;
  }
  if (!force) {
    openDesktopCityPicker(input);
    return;
  }
  const otherInput = input.id === 'origin_input'
    ? document.getElementById('destination_input')
    : document.getElementById('origin_input');
  if (otherInput) closeCityPicker(otherInput);
  try {
    cityPickerDropdown(input).show();
  } catch { /* Bootstrap initializes on first interaction */ }
}

function showFocusedDesktopPicker(input) {
  openDesktopCityPicker(input);
}

function cityPickerContext(target) {
  if (!target) return null;
  const isOrigin = target.id === 'origin_input' || target.id === 'origin_picker_q';
  const isDestination = target.id === 'destination_input' || target.id === 'dest_picker_q';
  if (!isOrigin && !isDestination) return null;

  const toggle = document.getElementById(isOrigin ? 'origin_input' : 'destination_input');
  if (!toggle) return null;
  const menuClass = isOrigin ? 'origin_location' : 'destination_location';
  const menu = (toggle.parentElement && toggle.parentElement.querySelector('.' + menuClass))
    || document.querySelector('body > .' + menuClass + '[data-city-picker-host="' + toggle.id + '"]');
  return { toggle, menu };
}

function clearCityPickerKeyboardSelection(toggle) {
  if (!toggle) return;
  toggle.removeAttribute('aria-activedescendant');
  const context = cityPickerContext(toggle);
  if (!context || !context.menu) return;
  context.menu.querySelectorAll('[role="option"]').forEach(function (option) {
    option.classList.remove('active');
    option.setAttribute('aria-selected', 'false');
  });
}

function moveCityPickerSelection(target, key) {
  const context = cityPickerContext(target);
  if (!context || !context.menu) return false;
  try { openDesktopCityPicker(context.toggle); } catch { /* already open */ }

  const options = Array.from(context.menu.querySelectorAll('[role="option"]'))
    .filter(option => !option.classList.contains('disabled') && !option.hasAttribute('disabled'));
  if (!options.length) return false;

  let currentIndex = options.findIndex(option => option.classList.contains('active'));
  let nextIndex;
  if (key === 'Home') nextIndex = 0;
  else if (key === 'End') nextIndex = options.length - 1;
  else if (key === 'ArrowDown') nextIndex = currentIndex < 0 ? 0 : (currentIndex + 1) % options.length;
  else nextIndex = currentIndex < 0 ? options.length - 1 : (currentIndex - 1 + options.length) % options.length;

  options.forEach(function (option, index) {
    const selected = index === nextIndex;
    option.classList.toggle('active', selected);
    option.setAttribute('aria-selected', selected ? 'true' : 'false');
  });
  context.toggle.setAttribute('aria-activedescendant', options[nextIndex].id);
  options[nextIndex].scrollIntoView({ block: 'nearest' });
  return true;
}

function selectActiveCityPickerOption(target) {
  const context = cityPickerContext(target);
  if (!context || !context.menu) return false;
  const activeOption = context.menu.querySelector('[role="option"].active');
  if (!activeOption) return false;
  activeOption.click();
  clearCityPickerKeyboardSelection(context.toggle);
  return true;
}

function pickerChrome(title, inputId, placeholder, iconClass) {
  return `
      <div class="city-picker__head">
        <span class="city-picker__title">
          <i class="ti ${iconClass} city-picker__context-icon" aria-hidden="true"></i>
          <span>${title}</span>
        </span>
        <button type="button" class="city-picker__close" aria-label="بستن">
          <i class="ti ti-x" aria-hidden="true"></i>
        </button>
      </div>
      <div class="city-picker__search">
        <i class="ti ti-search" aria-hidden="true"></i>
        <input type="text" class="form-control city-picker__input" id="${inputId}" placeholder="${placeholder}" autocomplete="off" />
      </div>`;
}

function ensureOriginDropdown() {
  const spanElement = $('.origin_location');
  spanElement.attr({ id: 'origin_city_listbox', role: 'listbox' });
  $('#origin_input').attr('aria-controls', 'origin_city_listbox');
  if ($('#origincontainer').length === 0) {
    spanElement.html(`
      <div class="city-picker">
        ${pickerChrome('انتخاب مبدا', 'origin_picker_q', 'جستجوی شهر مبدا', 'ti-location')}
        <div class="staredlocations">
          <label class="staredlocation_title ms-2 mt-2 text-muted pb-1" id="origin_most_lable">
            <i class="ti ti-map-pin-star icon locationicon p-1 pe-0"></i>
            شهرهای پرتردد
          </label>
          <div class="px-1 terminals_container_orig" id="origincontainer"></div>
        </div>
      </div>`);
  }
}

function ensureDestinationDropdown() {
  const spanElement = $('.dropdown-menu.destination_location');
  spanElement.attr({ id: 'destination_city_listbox', role: 'listbox' });
  $('#destination_input').attr('aria-controls', 'destination_city_listbox');
  if ($('#desticontainer').length === 0) {
    spanElement.html(`
      <div class="city-picker">
        ${pickerChrome('انتخاب مقصد', 'dest_picker_q', 'جستجوی شهر مقصد', 'ti-map-pin')}
        <div class="staredlocations">
          <label class="staredlocation_title ms-2 mt-2 text-muted pb-1">
            <i class="ti ti-map-pin-star icon locationicon p-1 pe-0"></i>
            مقصد ها
          </label>
          <div class="px-1 terminals_container_desti" id="desticontainer"></div>
        </div>
      </div>`);
  }
}

let cityPickerPushed = false;

function pushCityPickerHistory() {
  if (cityPickerPushed) return;
  cityPickerPushed = true;
  try { history.pushState({ mrCityPicker: true }, ''); } catch { /* ignore */ }
}

function consumeCityPickerHistory() {
  if (!cityPickerPushed) return;
  cityPickerPushed = false;
  try {
    if (history.state && history.state.mrCityPicker) history.back();
  } catch { /* ignore */ }
}

function syncCityPickerLock() {
  const open = isMobileCityPicker() && !!document.querySelector(
    '.dropdown-menu.origin_location.show, .dropdown-menu.destination_location.show'
  );
  document.body.classList.toggle('city-picker-open', !!open);
}

const DIRECTIONS_STORAGE_KEY = 'mrshoofer_available_directions_v2';
const DIRECTIONS_CACHE_TTL_MS = 60 * 60 * 1000; // 1 hour
let directionsReady = false;
let destinationUnlocked = false;

function applyDirectionsPayload(data) {
  directions = [];
  originKeys = [];
  displayNameByKey = new Map();

  const normalizedPairs = (data || [])
    .map(item => {
      let raw1 = item.Cityone || item.cityone || item.cityOne || item.city_one || item.city_name || '';
      let raw2 = item.Citytwo || item.citytwo || item.cityTwo || item.city_two || item.destination_city_name || '';
      raw1 = decodeUnicodeEscapes(raw1);
      raw2 = decodeUnicodeEscapes(raw2);
      const disp1 = isAscii(raw1) ? toPersianGuess(raw1) : raw1;
      const disp2 = isAscii(raw2) ? toPersianGuess(raw2) : raw2;
      const key1 = normalize(disp1 || raw1);
      const key2 = normalize(disp2 || raw2);
      if (key1) displayNameByKey.set(key1, disp1 || raw1 || '');
      if (key2) displayNameByKey.set(key2, disp2 || raw2 || '');
      return key1 && key2 ? { Cityone: key1, Citytwo: key2 } : null;
    })
    .filter(Boolean);

  directions = normalizedPairs;
  // Direction pairs are ordered: Cityone is an origin and Citytwo is a destination.
  // A city that only appears as Citytwo must not be offered as an origin.
  originKeys = Array.from(new Set(directions.map(d => d.Cityone)));
  directionsReady = originKeys.length > 0;
}

function readCachedDirections() {
  try {
    const raw = sessionStorage.getItem(DIRECTIONS_STORAGE_KEY);
    if (!raw) return null;
    const cached = JSON.parse(raw);
    if (!cached || !Array.isArray(cached.data) || !cached.ts) return null;
    if (Date.now() - cached.ts > DIRECTIONS_CACHE_TTL_MS) return null;
    return cached.data;
  } catch {
    return null;
  }
}

function writeCachedDirections(data) {
  try {
    sessionStorage.setItem(DIRECTIONS_STORAGE_KEY, JSON.stringify({ ts: Date.now(), data }));
  } catch { /* quota / private mode */ }
}

function showPickerStatus(containerSelector, message) {
  const $c = $(containerSelector);
  if (!$c.length) return;
  $c.empty().append($('<a>', {
    class: 'dropdown-item text-center mt-2 text-muted',
    text: message,
    href: 'javascript:void(0)'
  }));
}

function escapeHtml(value) {
  return String(value || '').replace(/[&<>"']/g, function (ch) {
    return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[ch];
  });
}

function typedOriginCity() {
  return ($('#origin_picker_q').val() || $('#origin_input').val() || '').trim();
}

function typedDestCity() {
  return ($('#dest_picker_q').val() || $('#destination_input').val() || '').trim();
}

function routeMissIconSvg(extraClass) {
  const cls = extraClass ? ` class="${extraClass}"` : '';
  return `<svg xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 80 80" focusable="false"${cls} aria-hidden="true">
    <path d="M0 0h80v80H0z" fill="none" />
    <g fill="none">
      <path class="route-miss-cta__path" pathLength="1" stroke="#2f80ed" stroke-linecap="round" stroke-linejoin="round" stroke-width="4" d="M60 33.5H45a8.5 8.5 0 0 0-8.5 8.5v0a8.5 8.5 0 0 0 8.5 8.5h6.5A8.5 8.5 0 0 1 60 59v0a8.5 8.5 0 0 1-8.5 8.5H20" />
      <path class="route-miss-cta__pin route-miss-cta__pin--a" fill="#eb5757" fill-rule="evenodd" d="M20 45.409a8 8 0 0 1 8 8v.3a8.3 8.3 0 0 1-1.588 4.883l-6.396 8.795a.02.02 0 0 1-.032 0l-6.396-8.795A8.3 8.3 0 0 1 12 53.709v-.3a8 8 0 0 1 8-8m1.595 4.73l-.048-.023a3.62 3.62 0 0 0-3.094 0l-.047.022a3.5 3.5 0 0 0-1.917 2.377a3.53 3.53 0 0 0 .7 3.014a3.58 3.58 0 0 0 2.768 1.323h.085a3.58 3.58 0 0 0 2.768-1.323a3.53 3.53 0 0 0 .701-3.014a3.5 3.5 0 0 0-1.916-2.377" clip-rule="evenodd" />
      <path class="route-miss-cta__pin route-miss-cta__pin--a" fill="#eb5757" d="m26.412 58.592l-1.617-1.177zm-6.396 8.795l-1.618-1.176zm-.032 0l1.618-1.176zm-6.396-8.795l-1.618 1.176zm7.959-8.476l-.856 1.808zm.047.022l.856-1.808zm-3.14-.022l-.856-1.808zm-.049.022l.856 1.808zm-1.916 2.377l1.948.453zm.7 3.014l1.553-1.26zm5.621 0l-1.552-1.26zm.701-3.014l-1.948.453zm6.489.894c0-5.523-4.477-10-10-10v4a6 6 0 0 1 6 6zm0 .3v-.3h-4v.3zm-1.97 6.059A10.3 10.3 0 0 0 30 53.709h-4a6.3 6.3 0 0 1-1.205 3.706zm-6.397 8.795l6.397-8.795l-3.235-2.353l-6.397 8.796zm-3.266 0a2.02 2.02 0 0 0 3.266 0l-3.235-2.352a1.98 1.98 0 0 1 3.204 0zm-6.397-8.795l6.397 8.795l3.235-2.352l-6.397-8.796zM10 53.709a10.3 10.3 0 0 0 1.97 6.059l3.235-2.353A6.3 6.3 0 0 1 14 53.71zm0-.3v.3h4v-.3zm10-10c-5.523 0-10 4.477-10 10h4a6 6 0 0 1 6-6zm.691 8.515l.048.022l1.71-3.616l-.047-.022zm-1.382 0a1.62 1.62 0 0 1 1.382 0l1.71-3.616a5.62 5.62 0 0 0-4.803 0zm-.048.022l.048-.022l-1.71-3.616l-.049.022zm-.824 1.022c.104-.449.407-.825.824-1.022l-1.71-3.616a5.5 5.5 0 0 0-3.01 3.732zm.305 1.3a1.53 1.53 0 0 1-.305-1.3l-3.896-.906a5.53 5.53 0 0 0 1.096 4.727zm1.215.584a1.58 1.58 0 0 1-1.215-.584l-3.105 2.521a5.58 5.58 0 0 0 4.32 2.063zm.085 0h-.085v4h.085zm1.215-.584c-.3.37-.753.584-1.215.584v4a5.58 5.58 0 0 0 4.32-2.062zm.306-1.3a1.53 1.53 0 0 1-.305 1.3l3.105 2.521a5.53 5.53 0 0 0 1.096-4.727zm-.824-1.022c.416.197.72.573.824 1.022l3.896-.906a5.5 5.5 0 0 0-3.01-3.732z" />
      <path class="route-miss-cta__pin route-miss-cta__pin--b" fill="#eb5757" fill-rule="evenodd" d="M60 11.91c4.418 0 8 3.639 8 8.057a8.13 8.13 0 0 1-1.597 4.84l-6.387 8.582a.02.02 0 0 1-.032 0l-6.387-8.582A8.13 8.13 0 0 1 52 19.967c0-4.418 3.582-8.056 8-8.056m1.633 4.64l-.108-.05a3.63 3.63 0 0 0-3.05 0l-.108.05a3.388 3.388 0 0 0-1.185 5.241l.034.042a3.5 3.5 0 0 0 2.689 1.261h.19c1.04 0 2.025-.462 2.689-1.261l.034-.041a3.388 3.388 0 0 0-1.185-5.241" clip-rule="evenodd" />
      <path class="route-miss-cta__pin route-miss-cta__pin--b" fill="#eb5757" d="m66.403 24.807l-1.604-1.194zm-6.387 8.582l-1.605-1.194zm-.032 0l1.605-1.194zm-6.387-8.582l1.604-1.194zm7.928-8.306l-.84 1.815zm.108.05l.84-1.816zm-3.158-.05l.84 1.815zm-.108.05l-.84-1.816zm-1.875 2.291l1.946.463zm.69 2.95l1.538-1.279zm.034.04l-1.538 1.279zm5.568 0l1.538 1.279zm.034-.04l-1.538-1.279zm.69-2.95l-1.946.463zM70 19.967C70 14.462 65.541 9.91 60 9.91v4c3.295 0 6 2.724 6 6.056zM68.007 26A10.13 10.13 0 0 0 70 19.967h-4a6.13 6.13 0 0 1-1.201 3.646zm-6.387 8.582L68.007 26l-3.208-2.388l-6.388 8.582zm-3.24 0a2.02 2.02 0 0 0 3.24 0l-3.209-2.388a1.98 1.98 0 0 1 3.178 0zM51.992 26l6.388 8.582l3.209-2.388l-6.388-8.582zM50 19.967c0 2.162.696 4.291 1.992 6.034l3.21-2.388A6.13 6.13 0 0 1 54 19.967zM60 9.91c-5.541 0-10 4.551-10 10.056h4c0-3.332 2.705-6.056 6-6.056zm.686 8.405l.108.05l1.679-3.63l-.109-.05zm-1.372 0a1.64 1.64 0 0 1 1.372 0l1.678-3.63a5.64 5.64 0 0 0-4.728 0zm-.108.05l.108-.05l-1.678-3.63l-.109.05zm-.768.94c.098-.415.381-.761.768-.94l-1.679-3.63a5.39 5.39 0 0 0-2.98 3.644zm.282 1.207a1.39 1.39 0 0 1-.282-1.208l-3.892-.925a5.39 5.39 0 0 0 1.098 4.69zm.035.041l-.035-.04l-3.076 2.556l.034.04zm1.15.54c-.445 0-.866-.198-1.15-.54l-3.077 2.557a5.5 5.5 0 0 0 4.227 1.983zm.19 0h-.19v4h.19zm1.15-.54c-.284.342-.705.54-1.15.54v4a5.5 5.5 0 0 0 4.227-1.983zm.035-.04l-.035.04l3.077 2.557l.034-.041zm.282-1.209c.101.425-.003.872-.282 1.208l3.076 2.557a5.39 5.39 0 0 0 1.098-4.69zm-.768-.939c.387.179.67.525.768.94l3.892-.926a5.39 5.39 0 0 0-2.981-3.645z" />
    </g>
  </svg>`;
}

function bindRouteMissCta($cta, hints) {
  const originHint = (hints && hints.origin) || '';
  const destHint = (hints && hints.dest) || '';
  const focus = (hints && hints.focus) || 'origin';
  $cta.attr('data-focus', focus);
  $cta.off('mousedown.routeMiss click.routeMiss');
  $cta.on('mousedown.routeMiss', function (e) {
    e.preventDefault();
    e.stopPropagation();
  });
  $cta.on('click.routeMiss', function (e) {
    e.preventDefault();
    e.stopPropagation();
    openRouteRequestModal({ origin: originHint, dest: destHint, focus: focus });
  });
}

function appendNoRouteCta($container, hints) {
  const $existing = $container.children('.route-miss-cta');
  if ($existing.length) {
    bindRouteMissCta($existing, hints);
    return;
  }

  const $picker = $container.closest('.city-picker');
  const shouldAnimate = !$picker.attr('data-route-miss-animated');
  if (shouldAnimate) $picker.attr('data-route-miss-animated', '1');
  const enterClass = shouldAnimate ? ' route-miss-cta--enter' : '';
  const $cta = $(`
    <button type="button" class="route-miss-cta${enterClass}">
      <span class="route-miss-cta__mark" aria-hidden="true">
        ${routeMissIconSvg()}
      </span>
      <span class="route-miss-cta__copy">
        <strong>مسیر پیدا نشد؟</strong>
        <span>مبدا و مقصد را بفرستید تا با شما تماس بگیریم</span>
      </span>
      <span class="route-miss-cta__action">
        <i class="ti ti-chevron-left" aria-hidden="true"></i>
      </span>
    </button>`);
  bindRouteMissCta($cta, hints);
  $container.append($cta);
}

function clearRouteMissAnimation($container) {
  $container.closest('.city-picker').removeAttr('data-route-miss-animated');
}

function ensureRouteRequestModal() {
  const existing = document.getElementById('route-request-overlay');
  if (existing && !document.querySelector('[data-route-request-layout="v5"]')) {
    existing.remove();
  } else if (existing) {
    return;
  }

  const hourOptions = buildRouteRequestHourOptions();
  const minuteOptions = buildRouteRequestMinuteOptions();
  const html = `
    <div id="route-request-overlay" hidden>
      <div class="route-request-sheet" role="dialog" aria-modal="true" aria-labelledby="route-request-title" data-route-request-layout="v5">
        <button type="button" class="route-request-close" aria-label="بستن" data-route-request-close>
          <i class="ti ti-x" aria-hidden="true"></i>
        </button>
        <div class="route-request-form-view">
          <div class="route-request-head">
            <span class="route-request-head__icon" aria-hidden="true">
              <img class="route-request-head__img" src="/img/route-request/taxi-confirmed.png" alt="" width="48" height="38" decoding="async" />
            </span>
            <div class="route-request-head__copy">
              <p class="route-request-kicker">مسیر شما پیدا نشد؟</p>
              <h2 id="route-request-title">درخواست سفر</h2>
            </div>
          </div>
          <p class="route-request-lead">مبدا، مقصد و جزییات سفر دلخواهتان را بنویسید تا برای انجام آن با شما هماهنگ کنیم.</p>
          <form id="route-request-form" novalidate>
            <div class="route-request-row route-request-row--od">
              <div class="route-request-field">
                <label class="form-label" for="route-request-origin">
                  <span class="route-request-field__mark route-request-field__mark--origin" aria-hidden="true"></span>
                  <span class="route-request-field__text">شهر <b>مبدا</b></span>
                  <span class="route-request-req" aria-hidden="true">*</span>
                </label>
                <input id="route-request-origin" name="origin" class="form-control" type="text" autocomplete="off" required placeholder="مبدا دلخواه" />
              </div>
              <div class="route-request-field">
                <label class="form-label" for="route-request-dest">
                  <span class="route-request-field__mark route-request-field__mark--dest" aria-hidden="true"></span>
                  <span class="route-request-field__text">شهر <b>مقصد</b></span>
                  <span class="route-request-req" aria-hidden="true">*</span>
                </label>
                <input id="route-request-dest" name="destination" class="form-control" type="text" autocomplete="off" required placeholder="مقصد دلخواه" />
              </div>
            </div>
            <div class="route-request-field">
              <label class="form-label" for="route-request-date">
                <span class="route-request-field__text">تاریخ سفر</span>
                <span class="route-request-req" aria-hidden="true">*</span>
              </label>
              <input id="route-request-date" name="tripDate" class="form-control route-request-date" type="text" autocomplete="off" required placeholder="انتخاب تاریخ" readonly />
            </div>
            <div class="route-request-field route-request-field--time">
              <div class="form-label" id="route-request-time-label">
                <span class="route-request-field__text">ساعت سفر</span>
                <span class="route-request-req" aria-hidden="true">*</span>
              </div>
              <div class="route-request-time" role="group" aria-labelledby="route-request-time-label">
                <div class="route-request-time__part">
                  <label class="route-request-time__hint" for="route-request-hour">ساعت</label>
                  <select id="route-request-hour" name="tripHour" class="form-select" required aria-label="ساعت">
                    <option value="" disabled selected>—</option>
                    ${hourOptions}
                  </select>
                </div>
                <span class="route-request-time__sep" aria-hidden="true">:</span>
                <div class="route-request-time__part">
                  <label class="route-request-time__hint" for="route-request-minute">دقیقه</label>
                  <select id="route-request-minute" name="tripMinute" class="form-select" required aria-label="دقیقه">
                    <option value="" disabled selected>—</option>
                    ${minuteOptions}
                  </select>
                </div>
              </div>
            </div>
            <div class="route-request-field">
              <label class="form-label" for="route-request-car">
                <span class="route-request-field__text">خودرو درخواستی</span>
              </label>
              <input id="route-request-car" name="carModel" class="form-control" type="text" autocomplete="off" list="route-request-car-list" placeholder="مثلاً پژو پارس (اختیاری)" />
              <datalist id="route-request-car-list">
                <option value="پژو"></option>
                <option value="پژو پارس"></option>
                <option value="سمند"></option>
                <option value="دنا"></option>
                <option value="سورن"></option>
                <option value="آریو"></option>
                <option value="VIP / تشریفات"></option>
              </datalist>
            </div>
            <div class="route-request-field">
              <label class="form-label" for="route-request-phone">
                <span class="route-request-field__text">شماره تماس</span>
                <span class="route-request-req" aria-hidden="true">*</span>
              </label>
              <input id="route-request-phone" name="phone" class="form-control" type="tel" inputmode="numeric" dir="rtl" autocomplete="tel" required maxlength="13" placeholder="شماره تماس به منظور هماهنگی" />
            </div>
            <p class="route-request-error" id="route-request-error" hidden></p>
            <button type="submit" class="route-request-submit">
              <span class="route-request-submit__label">ثبت درخواست سفر</span>
              <svg class="route-request-submit__icon" xmlns="http://www.w3.org/2000/svg" width="1em" height="1em" viewBox="0 0 24 24" aria-hidden="true" focusable="false">
                <path d="M0 0h24v24H0z" fill="none" />
                <path fill="currentColor" d="M9 15.59L4.71 11.3L3.3 12.71l5 5c.2.2.45.29.71.29s.51-.1.71-.29l11-11l-1.41-1.41L9.02 15.59Z" />
              </svg>
            </button>
          </form>
        </div>
        <div class="route-request-done-view" hidden>
          <div class="route-request-done-mark" aria-hidden="true">
            <img class="route-request-head__img" src="/img/route-request/taxi-confirmed.png" alt="" width="60" height="60" decoding="async" />
          </div>
          <h2>به‌زودی تماس می‌گیریم</h2>
          <p>درخواست مسیر شما ثبت شد. کارشناس مسترشوفر در اولین فرصت با همین شماره تماس می‌گیرد.</p>
          <button type="button" class="route-request-submit" data-route-request-close>باشه</button>
        </div>
      </div>
    </div>`;
  document.body.insertAdjacentHTML('beforeend', html);

  const overlay = document.getElementById('route-request-overlay');
  overlay.addEventListener('click', function (e) {
    if (e.target === overlay) closeRouteRequestModal();
  });
  overlay.querySelectorAll('[data-route-request-close]').forEach(function (btn) {
    btn.addEventListener('click', closeRouteRequestModal);
  });
  document.getElementById('route-request-form').addEventListener('submit', submitRouteRequest);
  bindRouteRequestPhoneInput(document.getElementById('route-request-phone'));
  ensureRouteRequestDatepicker();
}

function buildRouteRequestHourOptions() {
  const parts = [];
  for (let h = 0; h < 24; h++) {
    const value = String(h).padStart(2, '0');
    parts.push(`<option value="${value}">${toPersianDigits(value)}</option>`);
  }
  return parts.join('');
}

function buildRouteRequestMinuteOptions() {
  const parts = [];
  for (let m = 0; m < 60; m += 5) {
    const value = String(m).padStart(2, '0');
    parts.push(`<option value="${value}">${toPersianDigits(value)}</option>`);
  }
  return parts.join('');
}

function ensureRouteRequestDatepicker() {
  const dateInput = document.getElementById('route-request-date');
  if (!dateInput || dateInput._jalaliDatepicker || !window.JalaliDatepicker) return;
  dateInput._jalaliDatepicker = new JalaliDatepicker(dateInput, { minDate: 'today' });
}

function toEnglishDigits(value) {
  return String(value || '')
    .replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); })
    .replace(/[٠-٩]/g, function (d) { return '٠١٢٣٤٥٦٧٨٩'.indexOf(d); });
}

function toPersianDigits(value) {
  return String(value || '').replace(/\d/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
}

function bindRouteRequestPhoneInput(input) {
  if (!input || input.dataset.phoneBound === '1') return;
  input.dataset.phoneBound = '1';

  const formatPhoneDisplay = function () {
    const english = toEnglishDigits(input.value).replace(/[^\d+]/g, '');
    let digits = english.replace(/\D/g, '');
    if (digits.startsWith('0098')) digits = '0' + digits.slice(4);
    else if (digits.startsWith('98') && digits.length >= 12) digits = '0' + digits.slice(2);
    if (digits.length > 11) digits = digits.slice(0, 11);
    input.value = toPersianDigits(digits);
  };

  input.addEventListener('input', formatPhoneDisplay);
  input.addEventListener('blur', formatPhoneDisplay);
  input.addEventListener('paste', function () {
    window.setTimeout(formatPhoneDisplay, 0);
  });
}

function openRouteRequestModal(hints) {
  ensureRouteRequestModal();
  ensureRouteRequestDatepicker();
  closeAllDesktopCityPickers();
  try { $('#origin_input, #destination_input').dropdown('hide'); } catch { /* ignore */ }

  const overlay = document.getElementById('route-request-overlay');
  const formView = overlay.querySelector('.route-request-form-view');
  const doneView = overlay.querySelector('.route-request-done-view');
  const errorEl = document.getElementById('route-request-error');
  const form = document.getElementById('route-request-form');
  form.reset();
  const hourSelect = document.getElementById('route-request-hour');
  const minuteSelect = document.getElementById('route-request-minute');
  if (hourSelect) hourSelect.selectedIndex = 0;
  if (minuteSelect) minuteSelect.selectedIndex = 0;
  errorEl.hidden = true;
  errorEl.textContent = '';
  formView.hidden = false;
  doneView.hidden = true;

  document.getElementById('route-request-origin').value = (hints && hints.origin) || typedOriginCity();
  document.getElementById('route-request-dest').value = (hints && hints.dest) || typedDestCity();
  document.getElementById('route-request-origin').placeholder = 'مبدا دلخواه';
  document.getElementById('route-request-dest').placeholder = 'مقصد دلخواه';
  document.getElementById('route-request-phone').placeholder = 'شماره تماس به منظور هماهنگی';
  const kicker = overlay.querySelector('.route-request-kicker');
  const title = document.getElementById('route-request-title');
  const lead = overlay.querySelector('.route-request-lead');
  const submitBtn = document.querySelector('#route-request-form .route-request-submit');
  const submitLabel = submitBtn && submitBtn.querySelector('.route-request-submit__label');
  if (kicker) kicker.textContent = 'مسیر شما پیدا نشد؟';
  if (title) title.textContent = 'درخواست سفر';
  if (lead) lead.textContent = 'مبدا، مقصد و جزییات سفر دلخواهتان را بنویسید تا برای انجام آن با شما هماهنگ کنیم.';
  if (submitLabel) submitLabel.textContent = 'ثبت درخواست سفر';
  else if (submitBtn) submitBtn.textContent = 'ثبت درخواست سفر';
  overlay.hidden = false;
  document.body.classList.add('route-request-open');

  const focusId = hints && hints.focus === 'dest' ? 'route-request-dest' : 'route-request-origin';
  window.setTimeout(function () {
    const originVal = document.getElementById('route-request-origin').value.trim();
    const destVal = document.getElementById('route-request-dest').value.trim();
    const target = !originVal
      ? document.getElementById('route-request-origin')
      : (!destVal ? document.getElementById('route-request-dest') : document.getElementById(focusId));
    target.focus();
  }, 40);
}

function closeRouteRequestModal() {
  const overlay = document.getElementById('route-request-overlay');
  if (!overlay) return;
  overlay.hidden = true;
  document.body.classList.remove('route-request-open');
  const active = document.activeElement;
  if (active && overlay.contains(active)) active.blur();
}

function isValidIranMobile(raw) {
  let digits = toEnglishDigits(raw).replace(/[^\d+]/g, '');
  digits = digits.replace(/\D/g, '');
  if (digits.startsWith('0098')) digits = '0' + digits.slice(4);
  else if (digits.startsWith('98') && digits.length >= 12) digits = '0' + digits.slice(2);
  else if (digits.length === 10 && digits.startsWith('9')) digits = '0' + digits;

  // Iranian mobile: 09 + valid operator prefix + 7 digits
  if (/^09(0[0-5]|1[0-9]|2[0-3]|3[0-9]|9[0-9])\d{7}$/.test(digits)) return digits;
  return '';
}

async function submitRouteRequest(event) {
  event.preventDefault();
  const origin = document.getElementById('route-request-origin').value.trim();
  const dest = document.getElementById('route-request-dest').value.trim();
  const tripDate = document.getElementById('route-request-date').value.trim();
  const tripHour = document.getElementById('route-request-hour').value.trim();
  const tripMinute = document.getElementById('route-request-minute').value.trim();
  const carModel = document.getElementById('route-request-car').value.trim();
  const phone = isValidIranMobile(document.getElementById('route-request-phone').value);
  const errorEl = document.getElementById('route-request-error');
  const submitBtn = event.currentTarget.querySelector('.route-request-submit');
  const tripTime = tripHour && tripMinute ? `${tripHour}:${tripMinute}` : '';

  if (!origin || !dest) {
    errorEl.hidden = false;
    errorEl.textContent = 'مبدا و مقصد را وارد کنید.';
    return;
  }
  if (!tripDate || !tripHour || !tripMinute) {
    errorEl.hidden = false;
    errorEl.textContent = 'تاریخ و ساعت سفر را انتخاب کنید.';
    return;
  }
  if (!phone) {
    errorEl.hidden = false;
    errorEl.textContent = 'شماره موبایل معتبر ایرانی وارد کنید (مثلاً ۰۹۱۲۱۲۳۴۵۶۷).';
    return;
  }

  errorEl.hidden = true;
  submitBtn.disabled = true;
  const submitLabel = submitBtn.querySelector('.route-request-submit__label');
  const originalLabel = submitLabel ? submitLabel.textContent : submitBtn.textContent;
  if (submitLabel) submitLabel.textContent = 'در حال ثبت…';
  else submitBtn.textContent = 'در حال ثبت…';

  const carPart = carModel ? ` | خودرو درخواستی: ${carModel}` : '';
  try {
    const res = await fetch('/Message', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        name: 'درخواست مسیر',
        number: phone,
        message: `درخواست مسیر جدید — مبدا: ${origin} | مقصد: ${dest} | تاریخ: ${tripDate} | ساعت: ${tripTime}${carPart}`
      })
    });
    if (!res.ok) throw new Error('request failed');
    document.querySelector('.route-request-form-view').hidden = true;
    document.querySelector('.route-request-done-view').hidden = false;
  } catch {
    errorEl.hidden = false;
    errorEl.textContent = 'ثبت نشد. دوباره تلاش کنید یا با ۲۸۴۲۲۲۴۳-۰۲۱ تماس بگیرید.';
  } finally {
    submitBtn.disabled = false;
    if (submitLabel) submitLabel.textContent = originalLabel;
    else submitBtn.textContent = originalLabel;
  }
}

const DIRECTIONS_STATIC_URL = '/json/Directions/Directions.json';

function refreshOriginDestinationUiAfterCatalog() {
  const typedOrigin = ($('#origin_input').val() || '').trim();
  const typedOriginKey = normalize(typedOrigin);
  if (typedOriginKey && isPickerOriginKey(typedOriginKey)) {
    activeOriginKey = typedOriginKey;
    SetDestinations(typedOriginKey);
    EnableDestination();
  } else if (!typedOrigin) {
    LoadMostUsedOrigins();
  }
}

function refreshDirectionsFromApi() {
  return $.getJSON('/TaxiTrips/AvailableDirections')
    .done(function (data) {
      if (!data || !data.length) return;
      applyDirectionsPayload(data);
      writeCachedDirections(data);
      refreshOriginDestinationUiAfterCatalog();
    });
}

function FetchDirections() {
  return new Promise((resolve, reject) => {
    const cached = readCachedDirections();
    if (cached && cached.length) {
      applyDirectionsPayload(cached);
      resolve();
      refreshDirectionsFromApi().fail(function () { /* keep cache */ });
      return;
    }

    let settled = false;
    const done = function () {
      if (settled) return;
      settled = true;
      resolve();
    };

    // Instant bootstrap from bundled JSON so the picker is not blocked ~30s on ORS.
    $.getJSON(DIRECTIONS_STATIC_URL)
      .done(function (data) {
        if (!data || !data.length || directionsReady) return;
        applyDirectionsPayload(data);
        writeCachedDirections(data);
        done();
      });

    $.getJSON('/TaxiTrips/AvailableDirections')
      .done(function (data) {
        applyDirectionsPayload(data);
        writeCachedDirections(data);
        done();
      })
      .fail(function (xhr) {
        console.error('Failed to fetch available directions.', xhr?.status, xhr?.responseText);
        if (directionsReady) {
          done();
          return;
        }
        if (!settled) {
          settled = true;
          reject('Error fetching available directions');
        }
      });
  });
}

var most_used_origins = [];
const SEARCH_HINTS_STORAGE_KEY = 'mrshoofer_search_hints_v1';

function applySearchHints(data) {
  if (Array.isArray(data.supportedCities)) {
    supportedKeys = new Set(data.supportedCities.map(c => normalize(c)));
    data.supportedCities.forEach(function (city) {
      const key = normalize(city);
      if (key) displayNameByKey.set(key, city);
    });
  }
  if (Array.isArray(data.popularOrigins) && data.popularOrigins.length) {
    most_used_origins = data.popularOrigins.slice();
  }
}

function readCachedSearchHints(version) {
  try {
    const raw = sessionStorage.getItem(SEARCH_HINTS_STORAGE_KEY);
    if (!raw) return null;
    const cached = JSON.parse(raw);
    if (!cached || cached.version !== version) return null;
    return cached;
  } catch {
    return null;
  }
}

function FetchSearchHints() {
  return new Promise((resolve) => {
    $.getJSON('/TaxiTrips/SearchHints', function (data) {
      const version = data?.version || '';
      const cached = readCachedSearchHints(version);
      if (cached) {
        applySearchHints(cached);
        resolve();
        return;
      }
      try {
        sessionStorage.setItem(SEARCH_HINTS_STORAGE_KEY, JSON.stringify(data));
      } catch { /* quota / private mode */ }
      applySearchHints(data);
      resolve();
    }).fail(function (xhr) {
      console.error('Failed to fetch search hints.', xhr?.status, xhr?.responseText);
      most_used_origins = ["تهران", "اصفهان", "شیراز", "رشت", "چالوس", "کرمانشاه", "نوشهر"];
      supportedKeys = new Set();
      resolve();
    });
  });
}

function FetchSupportedCities() {
  return FetchSearchHints();
}

function intersectDirectionsWithSupported() {
  // Skip filtering if supportedKeys is empty or smaller than API directions
  // The API (AvailableDirections) is the source of truth for available cities
  // We only use supportedKeys for validation fallback, not to restrict the city list
  if (!supportedKeys || supportedKeys.size === 0) return;
  
  // Don't filter - API directions should be the primary source
  // The supportedKeys from DirectionsRepository is just a static fallback list
  // Filtering would remove valid cities that the API supports but aren't in the hardcoded list
}

/** Page was server-rendered for this OD; AJAX only updates trip cards, not SEO. */
function pageInitialOd() {
  const form = document.getElementById('tripForm');
  return {
    origin: normalize(toPersianGuess((form?.dataset.initialOrigin || '').trim())),
    dest: normalize(toPersianGuess((form?.dataset.initialDest || '').trim()))
  };
}

function searchOdChangedFromPage() {
  if (!$('.trips-container').length) return false;
  const initial = pageInitialOd();
  if (!initial.origin || !initial.dest) return false;
  const origin = normalize(toPersianGuess(($('#origin_input').val() || '').trim()));
  const dest = normalize(toPersianGuess(($('#destination_input').val() || '').trim()));
  return origin !== initial.origin || dest !== initial.dest;
}

function readSearchCities() {
  // Prefer live .val(); fall back to attribute (DestSelected used to set value="0")
  const read = (sel) => {
    const $el = $(sel);
    let v = ($el.val() || '').toString().trim();
    if (!v || v === '0') {
      const attr = ($el.attr('value') || '').toString().trim();
      if (attr && attr !== '0') v = attr;
    }
    return v;
  };
  return {
    origin: read('#origin_input'),
    destination: read('#destination_input'),
    searchdate: ($('#starttime').val() || '').trim()
  };
}

/** Keep sticky bridge + sidebar labels in sync with the search inputs. */
function updateRouteChromeLabels(origin, destination) {
  const o = (origin || '').trim();
  const d = (destination || '').trim();
  if (!o || !d) return;
  $('.trips-sticky-bridge-route').text(o + ' ← ' + d);
  const $strongs = $('.direction-text strong');
  if ($strongs.length >= 2) {
    $strongs.eq(0).text(o);
    $strongs.eq(1).text(d);
  }
}

/**
 * Swap #route-seo (+ bridge) for the current OD after an AJAX search.
 */
async function refreshRouteSeoUi(origin, destination) {
  const o = (origin || '').trim();
  const d = (destination || '').trim();
  if (!o || !d || o === '0' || d === '0') return;

  updateRouteChromeLabels(o, d);

  const form = document.getElementById('tripForm');
  if (form) {
    form.dataset.initialOrigin = o;
    form.dataset.initialDest = d;
  }

  try {
    const html = await $.ajax({
      url: '/TaxiTrips/RouteSeoPartial',
      method: 'GET',
      dataType: 'html',
      data: { originstring: o, destinationstring: d }
    });

    let $bridge = $('.trips-sticky-bridge');
    if (!$bridge.length && $('.trips-results .page-safezone').length) {
      $('.trips-results .page-safezone').prepend(
        `<div class="trips-sticky-bridge">
          <a href="#route-seo" class="trips-sticky-bridge-inner">
            <span class="trips-sticky-bridge-text">
              <span class="trips-sticky-bridge-label">راهنمای مسیر</span>
              <span class="trips-sticky-bridge-cta">
                نکته‌ها و سوالات
                <i class="ti ti-chevrons-down" aria-hidden="true"></i>
              </span>
              <span class="trips-sticky-bridge-route"></span>
            </span>
          </a>
        </div>`
      );
      $bridge = $('.trips-sticky-bridge');
    }
    updateRouteChromeLabels(o, d);
    $bridge.show();

    const $existing = $('#route-seo');
    if ($existing.length) $existing.replaceWith(html);
    else $('.trips-results .page-safezone').append(html);

    if (!document.querySelector('link[href*="RoutePages.css"]')) {
      const link = document.createElement('link');
      link.rel = 'stylesheet';
      link.href = '/css/RoutePages.css?v=12';
      document.head.appendChild(link);
    }
  } catch (err) {
    $('#route-seo').remove();
    $('.trips-sticky-bridge').remove();
  }
}

async function FetchTrips() {
  const { origin, destination, searchdate } = readSearchCities();

  if (!origin || !destination || !searchdate) {
    trips = [];
    if (typeof renderTrips === 'function') renderTrips(trips);
    return;
  }

  // Update labels immediately so the bridge never lags behind the trip cards
  updateRouteChromeLabels(origin, destination);

  const oKey = normalize(toPersianGuess(origin));
  const dKey = normalize(toPersianGuess(destination));
  const isOriginValid = isPickerOriginKey(oKey);
  const isDirectionValid = isDirectionPairValid(oKey, dKey);
  if (!isOriginValid || !isDirectionValid) {
    const $container = $('.trips-container');
    if ($container.length) {
      $container.empty().append(`
        <div class="route-miss-page">
          <div class="route-miss-page__icon" aria-hidden="true"><i class="ti ti-route-off"></i></div>
          <p class="route-miss-empty">${!isOriginValid
            ? `شهر مبدا «<b>${escapeHtml(origin)}</b>» در مسیرهای فعال نیست.`
            : `مسیر «<b>${escapeHtml(origin)}</b> به <b>${escapeHtml(destination)}</b>» هنوز فعال نیست.`}</p>
        </div>`);
      appendNoRouteCta($container.find('.route-miss-page'), {
        origin: origin,
        dest: destination,
        focus: isOriginValid ? 'dest' : 'origin'
      });
    } else {
      openRouteRequestModal({ origin: origin, dest: destination, focus: isOriginValid ? 'dest' : 'origin' });
    }
    trips = [];
    return;
  }

  try {
    const url = `/TaxiTrips/SearchJson?originstring=${encodeURIComponent(origin)}&destinationstring=${encodeURIComponent(destination)}&searchdate=${encodeURIComponent(searchdate)}`;
    const data = await $.getJSON(url);
    trips = data || [];
    if (typeof renderTrips === 'function') {
      renderTrips(trips);
      if (typeof GetCarModels === 'function' && typeof GenerateCarModelsFilter === 'function') {
        $('#carmodelsfilter').find('.form-check').not(':first').remove();
        const carModels = GetCarModels(trips);
        GenerateCarModelsFilter(carModels);
      }
    }
    await refreshRouteSeoUi(origin, destination);
  } catch (e) {
    console.error('Failed to fetch trips', e);
    let msg = 'خطا در جستجوی سفر';
    if (e && e.responseJSON && e.responseJSON.error) {
      const sug = Array.isArray(e.responseJSON.suggestions) && e.responseJSON.suggestions.length
        ? `\nپیشنهاد: ${e.responseJSON.suggestions.join('، ')}`
        : '';
      msg = `${e.responseJSON.error}${sug}`;
    }
    const $container = $('.trips-container');
    if ($container.length) {
      $container.empty().append(`<div class="d-flex col-12 mt-3" style="flex-direction: column; align-items: center; justify-content: start;">
        <label class="fs-5 fw-bold mt-4 pt-3 text-danger">${msg}</label>
      </div>`);
    } else { alert(msg); }
    trips = [];
  }
}

function keyToDisplay(key) { return displayNameByKey.get(key) || toPersianGuess(key) || key; }

function isPickerOriginKey(key) {
  if (!key) return false;
  if (supportedKeys && supportedKeys.size > 0) return supportedKeys.has(key);
  if (originKeys.includes(key)) return true;
  return directions.some(function (d) { return d.Cityone === key || d.Citytwo === key; });
}

function isDirectionPairValid(oKey, dKey) {
  if (!directions.length) return true;
  return directions.some(function (d) {
    return (d.Cityone === oKey && d.Citytwo === dKey) ||
      (d.Cityone === dKey && d.Citytwo === oKey);
  });
}

function pickerOriginKeys() {
  if (supportedKeys && supportedKeys.size > 0) return Array.from(supportedKeys);
  const keys = new Set(originKeys);
  directions.forEach(function (d) {
    keys.add(d.Cityone);
    keys.add(d.Citytwo);
  });
  return Array.from(keys);
}

function popularOriginPickerKeys() {
  const popular = most_used_origins.map(function (c) { return normalize(c); }).filter(Boolean);
  if (popular.length) {
    const supportedPopular = popular.filter(function (k) { return isPickerOriginKey(k); });
    if (supportedPopular.length) return supportedPopular;
    return popular;
  }
  return pickerOriginKeys().slice(0, 10);
}

function SetDestinations(originDisplay) {
  ensureDestinationDropdown();
  const selectedKey = normalize(originDisplay);
  const destinationsKeys = [];
  directions.forEach(function (item) {
    if (item.Cityone === selectedKey) destinationsKeys.push(item.Citytwo);
    else if (item.Citytwo === selectedKey) destinationsKeys.push(item.Cityone);
  });
  const uniqueKeys = Array.from(new Set(destinationsKeys));
  _destinations = uniqueKeys.map(keyToDisplay);
  AddResultLocations_destination(_destinations);
}

function LoadMostUsedOrigins() {
  ensureOriginDropdown();
  $('#origin_most_lable').css('visibility', 'visible');
  AddResultLocations_origin(popularOriginPickerKeys());
}

function AddResultLocations_origin(keys, query) {
  ensureOriginDropdown();
  var terminals_container = $('#origincontainer');
  clearCityPickerKeyboardSelection(document.getElementById('origin_input'));
  const q = (query || '').trim();
  if (!keys || keys.length === 0) {
    $('#origin_most_lable').hide();
    if (q.length >= 2) {
      terminals_container.children().not('.route-miss-cta').remove();
      if (!terminals_container.children('.route-miss-empty').length) {
        terminals_container.prepend($('<div>', {
          class: 'route-miss-empty',
          role: 'status'
        }));
      }
      terminals_container.children('.route-miss-empty').html(
        `شهری با نام «<b>${escapeHtml(q)}</b>» در مسیرهای فعال پیدا نشد.`
      );
      appendNoRouteCta(terminals_container, {
        origin: q,
        dest: typedDestCity(),
        focus: 'origin'
      });
    } else {
      terminals_container.empty();
      clearRouteMissAnimation(terminals_container);
      terminals_container.append($('<div>', { class: 'dropdown-item text-center mt-2 text-muted', role: 'status', text: "نتیجه‌ای پیدا نشد" }));
    }
  } else {
    terminals_container.empty();
    clearRouteMissAnimation(terminals_container);
    $('#origin_most_lable').show();
    keys.forEach((key, index) => {
      const display = keyToDisplay(key);
      var $aTag = $('<button>', {
        id: 'origin_city_option_' + index,
        type: 'button',
        class: 'dropdown-item',
        role: 'option',
        tabindex: '-1',
        'aria-selected': 'false',
        text: display
      });
      $aTag.on('mousedown', function (e) {
        e.preventDefault();
      });
      $aTag.on('click', function (e) {
        e.preventDefault();
        e.stopPropagation();
        OriginSelected(0, display);
      });
      terminals_container.append($aTag);
    });
  }
}

function AddResultLocations_destination(result_locations, query) {
  ensureDestinationDropdown();
  var terminals_container = $('#desticontainer');
  clearCityPickerKeyboardSelection(document.getElementById('destination_input'));
  const q = (query || '').trim();
  if (!result_locations || result_locations.length === 0) {
    $('.destination_location .staredlocation_title').hide();
    if (!destinationUnlocked) {
      terminals_container.empty();
      clearRouteMissAnimation(terminals_container);
      terminals_container.append($('<div>', { class: 'dropdown-item text-center mt-2 text-muted', role: 'status', text: "ابتدا شهر مبدا را انتخاب کنید" }));
      return;
    }
    terminals_container.children().not('.route-miss-cta').remove();
    if (!terminals_container.children('.route-miss-empty').length) {
      terminals_container.prepend($('<div>', {
        class: 'route-miss-empty',
        role: 'status'
      }));
    }
    const $empty = terminals_container.children('.route-miss-empty');
    if (q) {
      $empty.html(`مقصد «<b>${escapeHtml(q)}</b>» برای این مبدا در فهرست نیست.`);
    } else {
      $empty.text('برای این مبدا مقصد فعالی در فهرست نیست.');
    }
    appendNoRouteCta(terminals_container, {
      origin: typedOriginCity(),
      dest: q,
      focus: 'dest'
    });
    return;
  }
  terminals_container.empty();
  clearRouteMissAnimation(terminals_container);
  $('.destination_location .staredlocation_title').show();
  result_locations.forEach((location, index) => {
    var $aTag = $('<button>', {
      id: 'destination_city_option_' + index,
      type: 'button',
      class: 'dropdown-item',
      role: 'option',
      tabindex: '-1',
      'aria-selected': 'false',
      text: location
    });
    $aTag.on('mousedown', function (e) {
      e.preventDefault();
    });
    $aTag.on('click', function (e) {
      e.preventDefault();
      e.stopPropagation();
      DestSelected(0, location);
    });
    terminals_container.append($aTag);
  });
}

function focusDestinationPicker() {
  const destInput = document.getElementById('destination_input');
  if (!destInput || !destinationUnlocked) return;
  destInput.focus();
  openCityPicker(destInput, true);
}

function OriginSelected(id, name) {
  const city = (name || '').trim();
  // Always store the city name in both property and attribute (never the numeric id)
  $('#origin_input').val(city).attr('value', city);
  $('#origin_picker_q').val(city);
  $('#destination_input').val('').attr('value', '');
  $('#dest_picker_q').val('');
  activeOriginKey = normalize(city);
  EnableDestination();
  SetDestinations(city);

  const originInput = document.getElementById('origin_input');
  const originMenu = originInput && originInput.parentElement && originInput.parentElement.querySelector('.dropdown-menu');
  const originWasOpen = !!(originMenu && originMenu.classList.contains('show'));

  if (originWasOpen && originInput) {
    const onOriginHidden = function () {
      originInput.removeEventListener('hidden.bs.dropdown', onOriginHidden);
      focusDestinationPicker();
    };
    originInput.addEventListener('hidden.bs.dropdown', onOriginHidden);
    closeCityPicker(originInput);
    window.setTimeout(function () {
      originInput.removeEventListener('hidden.bs.dropdown', onOriginHidden);
      if (!originMenu.classList.contains('show')) focusDestinationPicker();
    }, isMobileCityPicker() ? 120 : 60);
    return;
  }

  window.setTimeout(focusDestinationPicker, isMobileCityPicker() ? 80 : 0);
}

function focusNextSearchField() {
  if (isMobileCityPicker()) return;
  const dateInput = document.getElementById('starttime');
  if (!dateInput || dateInput.disabled) return;
  window.setTimeout(function () {
    dateInput.focus();
  }, 0);
}

function DestSelected(id, name) {
  const city = (name || '').trim();
  $('#destination_input').val(city).attr('value', city);
  $('#dest_picker_q').val(city);

  const destInput = document.getElementById('destination_input');
  closeCityPicker(destInput);
  focusNextSearchField();
}

/**
 * Swap origin ↔ destination without wiping dest via OriginSelected.
 * Rebuilds destination list for the new origin; keeps dest if still valid.
 */
async function SwapOriginDestination() {
  const $o = $('#origin_input');
  const $d = $('#destination_input');
  if (!$o.length || !$d.length) return;

  const prevOrigin = ($o.val() || '').trim();
  const prevDest = ($d.val() || '').trim();
  if (!prevOrigin && !prevDest) return;

  const newOriginRaw = prevDest;
  const newDestRaw = prevOrigin;

  $o.val(newOriginRaw).attr('value', newOriginRaw);
  $d.val(newDestRaw).attr('value', newDestRaw);

  const originKey = normalize(toPersianGuess(newOriginRaw));

  if (!originKey) {
    activeOriginKey = '';
    LoadMostUsedOrigins();
    _destinations = [];
    AddResultLocations_destination([]);
    DisableDestination();
    return;
  }

  if (!isPickerOriginKey(originKey)) {
    activeOriginKey = '';
    _destinations = [];
    AddResultLocations_destination([]);
    DisableDestination();
    return;
  }

  const originDisplay = keyToDisplay(originKey);
  activeOriginKey = originKey;
  $o.val(originDisplay).attr('value', originDisplay);
  SetDestinations(originDisplay);
  EnableDestination();

  const destKey = normalize(toPersianGuess(newDestRaw));
  const destMatch = _destinations.find(city => normalize(city) === destKey);
  if (destMatch) {
    $d.val(destMatch).attr('value', destMatch);
  } else if (newDestRaw) {
    // Keep typed value visible but destinations list already refreshed
    $d.val(newDestRaw).attr('value', newDestRaw);
  } else {
    $d.val('').attr('value', '');
  }

  updateRouteChromeLabels(($o.val() || '').trim(), ($d.val() || '').trim());

  const inTaxiTripsPage = $('.trips-container').length > 0;
  if (inTaxiTripsPage && ($o.val() || '').trim() && ($d.val() || '').trim() && ($('#starttime').val() || '').trim()) {
    await FetchTrips();
  }
}

function DisableDestination() {
  destinationUnlocked = false;
  // Keep dropdown openable so users never hit a "dead" field — list explains next step.
  $("#destination_input").prop("disabled", false);
  syncCityPickerDropdownMode();
  AddResultLocations_destination([]);
}
function EnableDestination() {
  destinationUnlocked = true;
  $("#destination_input").prop("disabled", false);
  syncCityPickerDropdownMode();
}

function applyOdUiAfterLoad() {
  var origin_value_raw = ($('#origin_input').val() || '').trim();
  var origin_value = normalize(origin_value_raw);

  if (origin_value && isPickerOriginKey(origin_value)) {
    activeOriginKey = origin_value;
    LoadMostUsedOrigins();
    SetDestinations(origin_value_raw);
    EnableDestination();
  } else if (originKeys.length) {
    activeOriginKey = '';
    LoadMostUsedOrigins();
    AddResultLocations_destination([]);
    DisableDestination();
  } else {
    showPickerStatus('#origincontainer', 'شهرها در دسترس نیستند — لطفا دوباره تلاش کنید');
    showPickerStatus('#desticontainer', 'ابتدا شهر مبدا را انتخاب کنید');
    DisableDestination();
  }
}

$(document).ready(async function () {
  // Build picker chrome immediately so Bootstrap can open menus before the API returns.
  ensureOriginDropdown();
  ensureDestinationDropdown();
  ensureRouteRequestModal();
  showPickerStatus('#origincontainer', 'در حال بارگذاری شهرها…');
  showPickerStatus('#desticontainer', 'ابتدا شهر مبدا را انتخاب کنید');
  DisableDestination();

  const loadCatalog = (async function () {
    try {
      await Promise.all([FetchDirections(), FetchSupportedCities()]);
      intersectDirectionsWithSupported();
      applyOdUiAfterLoad();
      if (destinationUnlocked && (($('#destination_input').val() || '').trim())) {
        await FetchTrips();
      }
    } catch (error) {
      console.error('An error occurred:', error);
      most_used_origins = most_used_origins.length
        ? most_used_origins
        : ["تهران", "اصفهان", "شیراز", "رشت", "چالوس", "کرمانشاه", "نوشهر"];
      if (!originKeys.length && most_used_origins.length) {
        most_used_origins.forEach((c) => {
          const key = normalize(c);
          displayNameByKey.set(key, c);
          originKeys.push(key);
        });
      }
      applyOdUiAfterLoad();
      if (!directionsReady) {
        showPickerStatus('#origincontainer', 'خطا در بارگذاری — دوباره لمس کنید');
      }
    }
  })();

  syncCityPickerDropdownMode();
  window.addEventListener('resize', function () {
    window.clearTimeout(syncCityPickerDropdownMode._t);
    syncCityPickerDropdownMode._t = window.setTimeout(syncCityPickerDropdownMode, 150);
  });

  document.addEventListener('focus', function (event) {
    if (event.target.matches('#origin_input, #destination_input')) {
      openDesktopCityPicker(event.target);
    }
  }, true);

  // Desktop: open on field press; force-close when pressing anywhere else.
  // Bootstrap autoClose alone is unreliable here (input toggles + custom absolute menus).
  function dismissCityPickerIfOutside(event) {
    if (isMobileCityPicker()) return;
    const el = event.target instanceof Element
      ? event.target
      : (event.target && event.target.parentElement);
    if (!(el instanceof Element)) return;

    if (el.matches('#origin_input, #destination_input')) {
      event.stopImmediatePropagation();
      openDesktopCityPicker(el);
      return;
    }

    if (isInsideOpenCityPicker(el)) return;
    closeAllDesktopCityPickers();
    const active = document.activeElement;
    if (active && (active.id === 'origin_input' || active.id === 'destination_input')) {
      active.blur();
    }
  }
  document.addEventListener('pointerdown', dismissCityPickerIfOutside, true);
  document.addEventListener('click', dismissCityPickerIfOutside, true);
  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape' || !document.body.classList.contains('route-request-open')) return;
    event.preventDefault();
    event.stopPropagation();
    closeRouteRequestModal();
  }, true);

  document.addEventListener('keydown', function (event) {
    if (!event.target.matches('#origin_input, #destination_input, #origin_picker_q, #dest_picker_q')) return;

    if (event.key === 'Tab') {
      const nextToggle = !event.shiftKey && event.target.id === 'origin_input'
        ? document.getElementById('destination_input')
        : event.shiftKey && event.target.id === 'destination_input'
          ? document.getElementById('origin_input')
          : null;
      if (nextToggle) {
        event.preventDefault();
        event.stopPropagation();
        clearCityPickerKeyboardSelection(event.target);
        nextToggle.focus();
        showFocusedDesktopPicker(nextToggle);
      }
      return;
    }

    if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
      if (moveCityPickerSelection(event.target, event.key)) {
        event.preventDefault();
        event.stopPropagation();
      }
      return;
    }

    if (event.key === 'Enter' && selectActiveCityPickerOption(event.target)) {
      event.preventDefault();
      event.stopPropagation();
      return;
    }

    if (event.key === 'Escape') {
      const context = cityPickerContext(event.target);
      if (context && context.menu && context.menu.classList.contains('show')) {
        event.preventDefault();
        event.stopPropagation();
        clearCityPickerKeyboardSelection(context.toggle);
        try { $(context.toggle).dropdown('hide'); } catch { /* already closed */ }
      }
    }
  }, true);

  $('#origin_input').on('input', function () {
    ensureOriginDropdown();
    showFocusedDesktopPicker(this);
    if (!directionsReady && !originKeys.length) {
      showPickerStatus('#origincontainer', 'در حال بارگذاری شهرها…');
      return;
    }
    const raw = (($(this).val() || ''));
    const needles = queryNeedles(raw);
    if (!normalize(raw)) {
      activeOriginKey = '';
      $('#destination_input').val('');
      _destinations = [];
      AddResultLocations_destination([]);
      LoadMostUsedOrigins();
      DisableDestination();
      return;
    }
    $('#origin_most_lable').css('display', 'none');
    const searchableKeys = pickerOriginKeys();
    const matches = keysMatchingNeedles(searchableKeys, needles);
    const listToShow = raw.length < 2 ? searchableKeys : matches;
    AddResultLocations_origin(listToShow, raw);
    const exactKey = searchableKeys.find(function (key) { return key === normalize(raw); });
    if (exactKey && isPickerOriginKey(exactKey)) {
      // Exact text unlocks its destinations, but never rewrites the user's input.
      // Only an explicit dropdown click commits/canonicalizes the displayed city.
      if (activeOriginKey !== exactKey) {
        activeOriginKey = exactKey;
        $('#destination_input').val('').attr('value', '');
        $('#dest_picker_q').val('');
      }
      SetDestinations(exactKey);
      EnableDestination();
    } else {
      activeOriginKey = '';
      _destinations = [];
      AddResultLocations_destination([]);
      DisableDestination();
    }
  });

  $('#destination_input').on('input', function () {
    ensureDestinationDropdown();
    showFocusedDesktopPicker(this);
    if (!destinationUnlocked) {
      AddResultLocations_destination([]);
      return;
    }
    const raw = $(this).val() || '';
    const needles = queryNeedles(raw);
    if (!normalize(raw)) {
      AddResultLocations_destination(_destinations, '');
    } else {
      const destKeys = _destinations.map((city) => normalize(city));
      const matchedKeys = new Set(keysMatchingNeedles(destKeys, needles));
      const filteredCities = _destinations.filter((city) => matchedKeys.has(normalize(city)));
      AddResultLocations_destination(filteredCities, raw);
    }
  });

  $(document).on('input', '#origin_picker_q', function () {
    $('#origin_input').val($(this).val()).trigger('input');
  });
  $(document).on('input', '#dest_picker_q', function () {
    $('#destination_input').val($(this).val()).trigger('input');
  });
  $(document).on('click', '.city-picker__close', function (e) {
    e.preventDefault();
    e.stopPropagation();
    const $menu = $(this).closest('.dropdown-menu');
    const hostId = $menu.attr('data-city-picker-host');
    const $toggle = hostId
      ? $('#' + hostId)
      : $menu.closest('.input-container').find('input.dropdown-toggle');
    if ($toggle.length) $toggle.dropdown('hide');
  });

  function portalCityPickerMenu(toggleEl) {
    if (!isMobileCityPicker() || !toggleEl) return;
    const menu = toggleEl.parentElement && toggleEl.parentElement.querySelector('.dropdown-menu');
    if (!menu || menu.parentElement === document.body) return;
    menu.setAttribute('data-city-picker-host', toggleEl.id || '');
    menu._cityPickerHome = toggleEl.parentElement;
    document.body.appendChild(menu);
    menu.classList.add('show');
    menu.style.position = 'fixed';
    menu.style.inset = '0';
    menu.style.top = '0';
    menu.style.right = '0';
    menu.style.bottom = '0';
    menu.style.left = '0';
    menu.style.transform = 'none';
    menu.style.width = '100vw';
    menu.style.height = '100dvh';
    menu.style.maxHeight = '100dvh';
    menu.style.zIndex = '1080';
    menu.style.margin = '0';
  }

  function unportalCityPickerMenu(toggleEl) {
    const hostId = toggleEl && toggleEl.id;
    let menu = hostId
      ? document.querySelector('body > .dropdown-menu[data-city-picker-host="' + hostId + '"]')
      : null;
    if (!menu && toggleEl && toggleEl.parentElement) {
      menu = toggleEl.parentElement.querySelector('.dropdown-menu');
    }
    if (!menu) return;
    const home = menu._cityPickerHome;
    menu.style.cssText = '';
    menu.removeAttribute('data-city-picker-host');
    delete menu._cityPickerHome;
    if (home && menu.parentElement === document.body) {
      home.appendChild(menu);
    }
  }

  $('#origin_input, #destination_input').on('show.bs.dropdown', function (e) {
    if (this.id === 'destination_input' && !destinationUnlocked) {
      ensureDestinationDropdown();
      AddResultLocations_destination([]);
    }
    if (this.id === 'origin_input' && !directionsReady && !originKeys.length) {
      ensureOriginDropdown();
      showPickerStatus('#origincontainer', 'در حال بارگذاری شهرها…');
      loadCatalog.catch(function () { /* handled above */ });
    }
    if (!isMobileCityPicker()) return;
    this.setAttribute('data-bs-display', 'static');
  });
  $('#origin_input').on('shown.bs.dropdown', function () {
    if (!isMobileCityPicker()) return;
    portalCityPickerMenu(this);
    const $q = $('#origin_picker_q');
    $q.val($(this).val() || '');
    window.setTimeout(() => $q.trigger('focus'), 50);
    pushCityPickerHistory();
    syncCityPickerLock();
  });
  $('#destination_input').on('shown.bs.dropdown', function () {
    if (!isMobileCityPicker()) return;
    portalCityPickerMenu(this);
    const $q = $('#dest_picker_q');
    $q.val($(this).val() || '');
    window.setTimeout(() => $q.trigger('focus'), 50);
    pushCityPickerHistory();
    syncCityPickerLock();
  });
  $('#origin_input, #destination_input').on('hidden.bs.dropdown', function () {
    clearCityPickerKeyboardSelection(this);
    unportalCityPickerMenu(this);
    syncCityPickerLock();
    consumeCityPickerHistory();
  });
  window.addEventListener('popstate', function () {
    if (!document.body.classList.contains('city-picker-open')) return;
    cityPickerPushed = false;
    $('#origin_input, #destination_input').dropdown('hide');
  });

  $(document).on('click', '#od-swap-btn', function (e) {
    e.preventDefault();
    e.stopPropagation();
    const $btn = $(this);
    $btn.addClass('is-swapping');
    window.setTimeout(() => $btn.removeClass('is-swapping'), 380);
    SwapOriginDestination();
  });

  // Submit: AJAX trips + refresh SEO block for the selected OD
  $('#tripForm').on('submit', async function (e) {
    const inTaxiTripsPage = $('.trips-container').length > 0;
    if (!inTaxiTripsPage) return true;
    e.preventDefault();

    const $c = $('.trips-container');
    if ($c.length) {
      $c.empty().append(`<div class="d-flex justify-content-center align-items-center mt-5 pt-3">
        <div class="sk-chase sk-primary">
          <div class="sk-chase-dot"></div><div class="sk-chase-dot"></div><div class="sk-chase-dot"></div><div class="sk-chase-dot"></div><div class="sk-chase-dot"></div><div class="sk-chase-dot"></div>
        </div>
        <label class="fw-bold fs-5 ms-3">در حال بارگزاری سفر ها</label>
      </div>`);
    }
    await loadCatalog;
    await FetchTrips();
    return false;
  });
});
