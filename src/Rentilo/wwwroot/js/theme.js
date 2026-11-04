// Runs in <head> before first paint so the page never flashes the wrong theme.
(function () {
  var stored = null;
  try { stored = localStorage.getItem('rentilo-theme'); } catch (e) { /* storage blocked */ }
  var theme = stored || (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  document.documentElement.setAttribute('data-bs-theme', theme);
})();
