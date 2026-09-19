/**
 * JalaliDatepicker — multi-month scrollable Jalali calendar popup.
 * Requires window.JDate to be loaded (but NOT set as window.Date).
 *
 * Usage:
 *   new JalaliDatepicker(inputEl, { minDate: 'today', onSelect: fn })
 */
;(function (root) {
  'use strict';

  var MONTHS = [
    'فروردین','اردیبهشت','خرداد','تیر','مرداد','شهریور',
    'مهر','آبان','آذر','دی','بهمن','اسفند'
  ];
  var WEEKDAYS = ['ش','ی','د','س','چ','پ','ج'];
  var DAYS_IN_MONTH = [31,31,31,31,31,31,30,30,30,30,30,29];
  var MONTHS_AHEAD = 2;

  var JD = root.JDate || root.Date;

  function toJalali(d) {
    var j = new JD(d instanceof JD ? +d : d);
    return { y: j.getFullYear(), m: j.getMonth(), d: j.getDate() };
  }

  function jalaliDayOfWeek(y, m, d) {
    var j = new JD(y, m, d);
    return j.getDay(); // 0=Sun
  }

  function isLeap(y) {
    var a = [1,5,9,13,17,22,26,30];
    var rem = y % 33; if (rem < 0) rem += 33;
    return a.indexOf(rem) !== -1;
  }

  function daysInMonth(y, m) {
    if (m === 11) return isLeap(y) ? 30 : 29;
    return DAYS_IN_MONTH[m];
  }

  function sameDay(a, b) {
    return a.y === b.y && a.m === b.m && a.d === b.d;
  }

  function beforeDay(a, b) {
    if (a.y !== b.y) return a.y < b.y;
    if (a.m !== b.m) return a.m < b.m;
    return a.d < b.d;
  }


  function toFaDigits(v) {
    return String(v).replace(/\d/g, function (d) {
      return '۰۱۲۳۴۵۶۷۸۹'[Number(d)];
    });
  }

  function formatJalali(y, m, d) {
    var mm = m + 1;
    // Latin digits for form submit / PersianDate parsing; IRANSansX ss02 renders as Farsi
    return y + '/' + (mm < 10 ? '0' + mm : mm) + '/' + (d < 10 ? '0' + d : d);
  }

  function monthIndex(y, m) {
    return y * 12 + m;
  }

  function addMonths(y, m, delta) {
    var idx = monthIndex(y, m) + delta;
    var ny = Math.floor(idx / 12);
    var nm = idx % 12;
    if (nm < 0) { nm += 12; ny -= 1; }
    return { y: ny, m: nm };
  }

  function monthLabel(y, m) {
    return MONTHS[m] + ' ' + toFaDigits(y);
  }

  function el(tag, cls, txt) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (txt != null) e.textContent = txt;
    return e;
  }

  function navBtn(cls, iconCls, label) {
    var btn = el('button', 'jdp-nav-btn ' + cls);
    btn.type = 'button';
    btn.setAttribute('aria-label', label);
    var icon = el('i', 'ti ' + iconCls);
    icon.setAttribute('aria-hidden', 'true');
    btn.appendChild(icon);
    return btn;
  }

  function JalaliDatepicker(inputEl, opts) {
    opts = opts || {};
    this.input = typeof inputEl === 'string' ? document.querySelector(inputEl) : inputEl;
    this.onSelect = opts.onSelect || null;
    this.tooltipText = opts.tooltipText || '';
    this.selected = null;

    var today = toJalali(new JD());
    if (opts.minDate === 'today') {
      this.minDate = today;
    } else if (opts.minDate) {
      this.minDate = opts.minDate;
    } else {
      this.minDate = today;
    }
    this.today = today;
    this.viewStart = { y: today.y, m: today.m };
    this.monthsVisible = opts.monthsVisible || MONTHS_AHEAD;
    this.monthsStep = opts.monthsStep || this.monthsVisible;

    this._buildDOM();
    this._bindEvents();
  }

  JalaliDatepicker.prototype._buildDOM = function () {
    this.overlay = el('div', 'jdp-overlay');
    this.container = el('div', 'jdp-container');

    // top bar
    var topbar = el('div', 'jdp-topbar');
    this.closeBtn = el('button', 'jdp-close-btn', '✕');
    this.closeBtn.type = 'button';
    var title = el('span', 'jdp-topbar-title', 'انتخاب تاریخ سفر');
    topbar.appendChild(title);
    topbar.appendChild(this.closeBtn);
    this.container.appendChild(topbar);

    this.nav = el('div', 'jdp-nav');
    this.prevBtn = navBtn('jdp-nav-prev', 'ti-chevron-right', 'ماه‌های قبل');
    this.nextBtn = navBtn('jdp-nav-next', 'ti-chevron-left', 'ماه‌های بعد');
    this.navLabel = el('span', 'jdp-nav-label', '');
    this.nav.appendChild(this.prevBtn);
    this.nav.appendChild(this.navLabel);
    this.nav.appendChild(this.nextBtn);
    this.container.appendChild(this.nav);

    // body
    this.body = el('div', 'jdp-body');
    this._renderMonths();
    this.container.appendChild(this.body);

    // footer
    var footer = el('div', 'jdp-footer');
    this.confirmBtn = el('button', 'jdp-confirm-btn', 'تأیید');
    this.confirmBtn.type = 'button';
    this.confirmBtn.disabled = true;
    footer.appendChild(this.confirmBtn);
    this.container.appendChild(footer);

    document.body.appendChild(this.overlay);
    document.body.appendChild(this.container);
  };

  JalaliDatepicker.prototype._renderMonths = function () {
    this.body.innerHTML = '';
    var y = this.viewStart.y;
    var m = this.viewStart.m;

    for (var i = 0; i < this.monthsVisible; i++) {
      var cur = addMonths(y, m, i);
      this._renderMonth(cur.y, cur.m);
    }

    this._updateNav();
  };

  JalaliDatepicker.prototype._updateNav = function () {
    var first = this.viewStart;
    var last = addMonths(first.y, first.m, this.monthsVisible - 1);
    this.navLabel.textContent = monthLabel(first.y, first.m) + ' — ' + monthLabel(last.y, last.m);

    var minIdx = monthIndex(this.minDate.y, this.minDate.m);
    var startIdx = monthIndex(this.viewStart.y, this.viewStart.m);
    this.prevBtn.disabled = startIdx <= minIdx;
  };

  JalaliDatepicker.prototype._shiftMonths = function (delta) {
    var next = addMonths(this.viewStart.y, this.viewStart.m, delta);
    var minIdx = monthIndex(this.minDate.y, this.minDate.m);
    if (monthIndex(next.y, next.m) < minIdx) return;
    this.viewStart = next;
    this._renderMonths();
    this._refreshSelection();
  };

  JalaliDatepicker.prototype._renderMonth = function (y, m) {
    var monthEl = el('div', 'jdp-month');

    var header = el('div', 'jdp-month-header', MONTHS[m] + ' ' + y);
    monthEl.appendChild(header);

    var weekRow = el('div', 'jdp-weekdays');
    for (var w = 0; w < 7; w++) {
      weekRow.appendChild(el('div', 'jdp-weekday', WEEKDAYS[w]));
    }
    monthEl.appendChild(weekRow);

    var grid = el('div', 'jdp-days');
    var dim = daysInMonth(y, m);

    // first day of month — which column? Weekdays header is ش ی د س چ پ ج
    // Saturday=col0, Sunday=col1, ..., Friday=col6
    var dow = jalaliDayOfWeek(y, m, 1); // 0=Sun..6=Sat
    var col = (dow + 1) % 7; // Sat→0, Sun→1, Mon→2...Fri→6

    for (var e = 0; e < col; e++) {
      grid.appendChild(el('div', 'jdp-day-empty'));
    }

    for (var d = 1; d <= dim; d++) {
      var dayInfo = { y: y, m: m, d: d };
      var dayDow = (dow + d - 1) % 7; // native getDay for this day (approx)
      var realDow = jalaliDayOfWeek(y, m, d);
      var isFriday = realDow === 5;
      var isDisabled = beforeDay(dayInfo, this.minDate);
      var isToday = sameDay(dayInfo, this.today);
      var isSel = this.selected && sameDay(dayInfo, this.selected);

      var btn = el('button', 'jdp-day');
      btn.type = 'button';
      if (isFriday) btn.classList.add('jdp-friday');
      if (isDisabled) btn.classList.add('jdp-disabled');
      if (isToday) btn.classList.add('jdp-today');
      if (isSel) btn.classList.add('jdp-selected');
      btn.dataset.y = y;
      btn.dataset.m = m;
      btn.dataset.d = d;

      var inner = el('div', 'jdp-day-inner', toFaDigits(d));
      btn.appendChild(inner);


      grid.appendChild(btn);
    }

    monthEl.appendChild(grid);
    this.body.appendChild(monthEl);
  };

  JalaliDatepicker.prototype._bindEvents = function () {
    var self = this;

    this.input.addEventListener('click', function (e) {
      e.preventDefault();
      self.open();
    });
    this.input.addEventListener('focus', function (e) {
      e.preventDefault();
      self.input.blur();
      self.open();
    });

    this.overlay.addEventListener('click', function () { self.close(); });
    this.closeBtn.addEventListener('click', function () { self.close(); });

    this.prevBtn.addEventListener('click', function () {
      self._shiftMonths(-self.monthsStep);
    });
    this.nextBtn.addEventListener('click', function () {
      self._shiftMonths(self.monthsStep);
    });

    this.body.addEventListener('click', function (e) {
      var btn = e.target.closest('.jdp-day');
      if (!btn || btn.classList.contains('jdp-disabled')) return;
      self.selected = {
        y: +btn.dataset.y,
        m: +btn.dataset.m,
        d: +btn.dataset.d
      };
      self._refreshSelection();
      self.confirmBtn.disabled = false;
    });

    this.confirmBtn.addEventListener('click', function () {
      if (!self.selected) return;
      var formatted = formatJalali(self.selected.y, self.selected.m, self.selected.d);
      self.input.value = formatted;
      if (self.onSelect) self.onSelect(self.selected, formatted);
      self.close();
    });
  };

  JalaliDatepicker.prototype._refreshSelection = function () {
    var all = this.body.querySelectorAll('.jdp-day');
    for (var i = 0; i < all.length; i++) {
      var b = all[i];
      var day = { y: +b.dataset.y, m: +b.dataset.m, d: +b.dataset.d };
      if (this.selected && sameDay(day, this.selected)) {
        b.classList.add('jdp-selected');
      } else {
        b.classList.remove('jdp-selected');
      }
    }
  };

  JalaliDatepicker.prototype.open = function () {
    this._syncViewFromInput();
    this._renderMonths();
    this._refreshSelection();

    this.overlay.classList.add('jdp-open');
    this.container.classList.add('jdp-open');
    document.body.style.overflow = 'hidden';

    var scrollTarget = this.body.querySelector('.jdp-selected, .jdp-today');
    if (scrollTarget) {
      var month = scrollTarget.closest('.jdp-month');
      if (month) month.scrollIntoView({ block: 'start' });
    }
  };

  JalaliDatepicker.prototype._syncViewFromInput = function () {
    var raw = (this.input.value || '').trim();
    var match = /^(\d{4})[\/\-](\d{1,2})[\/\-](\d{1,2})$/.exec(raw);
    if (match) {
      var y = +match[1];
      var m = +match[2] - 1;
      var d = +match[3];
      if (m >= 0 && m < 12 && d >= 1 && d <= daysInMonth(y, m)) {
        this.selected = { y: y, m: m, d: d };
        this.viewStart = { y: y, m: m };
        this.confirmBtn.disabled = false;
        return;
      }
    }

    this.viewStart = { y: this.today.y, m: this.today.m };
    if (!this.selected) this.confirmBtn.disabled = true;
  };

  JalaliDatepicker.prototype.close = function () {
    this.overlay.classList.remove('jdp-open');
    this.container.classList.remove('jdp-open');
    document.body.style.overflow = '';
  };

  root.JalaliDatepicker = JalaliDatepicker;
})(window);
