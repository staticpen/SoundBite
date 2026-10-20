// Behaviour for the top navbar. Uses delegated document listeners so it works on
// static SSR pages, enhanced navigation and interactive pages alike, and is safe
// to load more than once.
(function () {
    if (window.__soundBiteNavInit) {
        return;
    }
    window.__soundBiteNavInit = true;

    // Checks whether an element is currently visible on the page.
    function isVisible(element) {
        return element.offsetParent !== null;
    }

    // Marks the navigation menu as collapsed.
    function collapseMenu(toggle) {
        toggle.setAttribute('aria-expanded', 'false');
    }

    // Closes every open account menu except the one being interacted with.
    function closeAccountMenus(except) {
        // Keeps the account menu that contains the current click open.
        document.querySelectorAll('[data-account-menu][open]').forEach(function (menu) {
            if (menu !== except) {
                menu.removeAttribute('open');
            }
        });
    }

    // Handles navigation toggles, menu links, and outside clicks.
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

    // Closes open menus and restores focus when Escape is pressed.
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
