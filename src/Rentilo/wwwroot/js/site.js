// Rentilo client-side enhancements. Everything here is progressive: pages work without JavaScript.
(function () {
  'use strict';

  // ---- Theme toggle -----------------------------------------------------------------------
  function applyThemeIcon() {
    var dark = document.documentElement.getAttribute('data-bs-theme') === 'dark';
    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
      btn.textContent = dark ? '☀️' : '🌙';
      btn.setAttribute('aria-label', dark ? 'Switch to light theme' : 'Switch to dark theme');
    });
  }
  document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-theme-toggle]');
    if (!btn) return;
    var next = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-bs-theme', next);
    try { localStorage.setItem('rentilo-theme', next); } catch (err) { /* ignore */ }
    applyThemeIcon();
  });
  applyThemeIcon();

  // ---- Confirm before destructive actions (forms with data-confirm) ------------------------
  document.addEventListener('submit', function (e) {
    var form = e.target;
    var submitter = e.submitter;
    var message = (submitter && submitter.getAttribute('data-confirm')) || form.getAttribute('data-confirm');
    if (message && !window.confirm(message)) e.preventDefault();
  });

  // ---- Auto-submit filters (selects with data-autosubmit) ----------------------------------
  document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
    el.addEventListener('change', function () { el.form && el.form.submit(); });
  });

  // ---- Live price preview on the booking form ------------------------------------------------
  var bookingForm = document.querySelector('[data-booking-form]');
  if (bookingForm) {
    var pricePerDay = parseFloat(bookingForm.getAttribute('data-price-per-day'));
    var start = bookingForm.querySelector('[data-start]');
    var end = bookingForm.querySelector('[data-end]');
    var summary = bookingForm.querySelector('[data-summary]');
    var daysEl = bookingForm.querySelector('[data-days]');
    var totalEl = bookingForm.querySelector('[data-total]');
    var fmt = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2 });

    var update = function () {
      if (start.value) end.min = start.value;
      if (start.value && end.value && end.value < start.value) end.value = start.value;
      if (!start.value || !end.value) { summary.hidden = true; return; }
      var days = Math.round((Date.parse(end.value) - Date.parse(start.value)) / 86400000) + 1;
      if (!(days > 0)) { summary.hidden = true; return; }
      daysEl.textContent = days === 1 ? '1 day' : days + ' days';
      totalEl.textContent = fmt.format(days * pricePerDay) + ' kr';
      summary.hidden = false;
    };
    start.addEventListener('change', update);
    end.addEventListener('change', update);
    update();
  }

  // ---- Image preview on the item form -------------------------------------------------------
  document.querySelectorAll('[data-image-input]').forEach(function (input) {
    var preview = document.querySelector(input.getAttribute('data-image-input'));
    if (!preview) return;
    input.addEventListener('change', function () {
      var file = input.files && input.files[0];
      if (!file || !file.type.startsWith('image/')) return;
      var img = preview.querySelector('img') || document.createElement('img');
      img.alt = 'Selected image preview';
      img.src = URL.createObjectURL(file);
      preview.replaceChildren(img);
      preview.hidden = false;
    });
  });

  // ---- Star rating input (radio group) highlight ------------------------------------------------
  document.querySelectorAll('[data-star-input]').forEach(function (group) {
    var labels = group.querySelectorAll('label');
    var paint = function (value) {
      labels.forEach(function (l) { l.classList.toggle('on', parseInt(l.getAttribute('data-value'), 10) <= value); });
    };
    group.addEventListener('change', function (e) { paint(parseInt(e.target.value, 10)); });
    var checked = group.querySelector('input:checked');
    paint(checked ? parseInt(checked.value, 10) : 0);
  });
})();
