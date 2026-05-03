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

    document.querySelectorAll('[data-nav-search]').forEach(function (input) {
      input.addEventListener('input', function () {
        var term = (input.value || '').trim().toLowerCase();
        var nav = input.closest('.sidebar, .offcanvas-body')?.querySelector('.sidebar-nav');
        if (!nav) return;

        nav.querySelectorAll('[data-nav-group]').forEach(function (group) {
          var visibleLinks = 0;
          group.querySelectorAll('.sidebar-link').forEach(function (link) {
            var label = (link.getAttribute('data-nav-label') || link.textContent || '').toLowerCase();
            var show = !term || label.indexOf(term) !== -1;
            link.style.display = show ? '' : 'none';
            if (show) visibleLinks += 1;
          });
          group.style.display = visibleLinks > 0 ? '' : 'none';
        });
      });
    });

    document.querySelectorAll('[data-snackbar]').forEach(function (snackbar) {
      function dismiss() {
        snackbar.style.opacity = '0';
        snackbar.style.transform = 'translateY(-6px)';
        window.setTimeout(function () {
          snackbar.remove();
        }, 180);
      }

      var close = snackbar.querySelector('[data-snackbar-close]');
      if (close) {
        close.addEventListener('click', dismiss);
      }

      window.setTimeout(dismiss, 6000);
    });
  });
})();
