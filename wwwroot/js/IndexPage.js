/**
 * Homepage / trips search: form validation + Jalali datepicker on #starttime.
 * Requires: jQuery, jdate (window.JDate), jalali-datepicker.js
 */
'use strict';

function ensureJalaliDatepicker() {
  var dateInput = document.getElementById('starttime');
  if (!dateInput || dateInput._jalaliDatepicker || !window.JalaliDatepicker) return;
  dateInput._jalaliDatepicker = new JalaliDatepicker(dateInput, { minDate: 'today' });
}

$(function () {
  $('#tripForm').on('submit', function (e) {
    var isValid = true;
    $(this).find('input').each(function () {
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

  $(document).on('focus click', '#starttime', function () {
    ensureJalaliDatepicker();
  });

  $(document).on('click', '.starttimeselector, .ti-calendar-stats', function () {
    ensureJalaliDatepicker();
    var dateInput = document.getElementById('starttime');
    if (dateInput) dateInput.focus();
  });
});
