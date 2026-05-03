(function () {
  const storageKey = 'lms-theme';

  var initial = localStorage.getItem(storageKey);
  if (initial !== 'light' && initial !== 'dark') {
    initial = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  document.documentElement.setAttribute('data-theme', initial);

  function syncUi(theme) {
    document.querySelectorAll('.theme-label').forEach(function (el) {
      el.textContent = theme === 'dark' ? 'Light mode' : 'Dark mode';
    });
    document.querySelectorAll('[data-theme-toggle] i').forEach(function (icon) {
      var isDrawerOrSidebar = icon.closest('.sidebar, .nav-drawer');
      var moon = 'bi bi-moon-stars';
      var sun = 'bi bi-sun-fill';
      if (isDrawerOrSidebar) {
        icon.className = theme === 'dark' ? sun + ' me-1' : moon + ' me-1';
      } else {
        icon.className = theme === 'dark' ? sun : moon;
      }
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    syncUi(document.documentElement.getAttribute('data-theme') || 'light');
    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var next = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        localStorage.setItem(storageKey, next);
        document.documentElement.setAttribute('data-theme', next);
        syncUi(next);
      });
    });

    var mobileNav = document.getElementById('mobileNav');
    if (mobileNav && typeof bootstrap !== 'undefined') {
      mobileNav.querySelectorAll('.sidebar-link').forEach(function (link) {
        link.addEventListener('click', function () {
          var oc = bootstrap.Offcanvas.getInstance(mobileNav);
          if (oc) oc.hide();
        });
      });
    }
  });
})();
