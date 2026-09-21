/**
 * Legacy Agency Index charge-modal helper — no auto-open.
 */
(function () {
  function hideChargeModal() {
    var el = document.getElementById('referAndEarn');
    if (!el || typeof bootstrap === 'undefined' || !bootstrap.Modal) return;
    var instance = bootstrap.Modal.getInstance(el);
    if (instance) instance.hide();
  }

  document.addEventListener('DOMContentLoaded', function () {
    var urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get('openChargeModal') || window.location.hash === '#charge') {
      if (window.history.replaceState) {
        var cleanUrl = window.location.protocol + '//' + window.location.host + window.location.pathname;
        window.history.replaceState({ path: cleanUrl }, '', cleanUrl);
      }
    }
  });

  window.agencyHideChargeModal = hideChargeModal;
})();
