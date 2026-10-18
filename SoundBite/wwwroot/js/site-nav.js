// Behaviour for the top navbar. Uses delegated document listeners so it works on
// static SSR pages, enhanced navigation and interactive pages alike, and is safe
// to load more than once.
(function () {
    if (window.__soundBiteNavInit) {
        return;
    }
    window.__soundBiteNavInit = true;

    function isVisible(element) {
        return element.offsetParent !== null;
    }

    function collapseMenu(toggle) {
        toggle.setAttribute('aria-expanded', 'false');
    }

    function closeAccountMenus(except) {
        document.querySelectorAll('[data-account-menu][open]').forEach(function (menu) {
            if (menu !== except) {
                menu.removeAttribute('open');
            }
        });
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        if (!(target instanceof Element)) {
            return;
        }

        var toggle = target.closest('[data-site-nav-toggle]');
        if (toggle) {
            var expanded = toggle.getAttribute('aria-expanded') === 'true';
            toggle.setAttribute('aria-expanded', expanded ? 'false' : 'true');
            return;
        }

        if (target.closest('#site-nav-menu a[href]')) {
            var menuToggle = document.querySelector('[data-site-nav-toggle]');
            if (menuToggle) {
                collapseMenu(menuToggle);
            }
            closeAccountMenus(null);
            return;
        }

        closeAccountMenus(target.closest('[data-account-menu]'));
    });

    document.addEventListener('keydown', function (event) {
        if (event.key !== 'Escape') {
            return;
        }

        var openMenu = document.querySelector('[data-account-menu][open]');
        if (openMenu) {
            openMenu.removeAttribute('open');
            var summary = openMenu.querySelector('summary');
            if (summary && summary.isConnected && isVisible(summary)) {
                summary.focus();
            }
            return;
        }

        var toggle = document.querySelector('[data-site-nav-toggle]');
        if (toggle && toggle.getAttribute('aria-expanded') === 'true') {
            collapseMenu(toggle);
            if (toggle.isConnected && isVisible(toggle)) {
                toggle.focus();
            }
        }
    });
})();
