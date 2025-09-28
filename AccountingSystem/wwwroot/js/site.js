// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener('DOMContentLoaded', function () {
    var toggle = document.getElementById('sidebarToggle');
    if (toggle) {
        toggle.addEventListener('click', function () {
            document.getElementById('wrapper').classList.toggle('toggled');
        });
    }

    var sidebar = document.getElementById('mainSidebar');
    if (!sidebar) {
        return;
    }

    var currentPath = window.location.pathname.toLowerCase();
    var menuLinks = sidebar.querySelectorAll('[data-menu-link]');
    var menuGroups = Array.from(sidebar.querySelectorAll('[data-menu-group]'));
    var menuLeaves = Array.from(sidebar.querySelectorAll('[data-menu-leaf]'));
    var searchInput = document.getElementById('sidebarMenuSearch');

    var setGroupOpen = function (group, isOpen, persist) {
        var toggleButton = group.querySelector('[data-menu-toggle]');
        if (!toggleButton) {
            return;
        }

        group.classList.toggle('is-open', isOpen);
        toggleButton.setAttribute('aria-expanded', isOpen ? 'true' : 'false');

        if (persist) {
            group.dataset.menuDefaultOpen = 'true';
        }
    };

    menuGroups.forEach(function (group) {
        var toggleButton = group.querySelector('[data-menu-toggle]');
        if (!toggleButton) {
            return;
        }

        toggleButton.addEventListener('click', function () {
            var isOpen = group.classList.toggle('is-open');
            toggleButton.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });
    });

    menuLinks.forEach(function (link) {
        var linkPath = link.pathname ? link.pathname.toLowerCase() : '';
        if (!linkPath) {
            return;
        }

        if (currentPath === linkPath || (linkPath !== '/' && currentPath.startsWith(linkPath + '/'))) {
            link.classList.add('active');
            var owningGroup = link.closest('[data-menu-group]');
            if (owningGroup) {
                setGroupOpen(owningGroup, true, true);
            }
            var owningLeaf = link.closest('[data-menu-leaf]');
            if (owningLeaf) {
                owningLeaf.classList.add('active');
            }
        }
    });

    var filterMenu = function (term) {
        var normalizedTerm = term.trim().toLowerCase();
        var hasTerm = normalizedTerm.length > 0;

        menuGroups.forEach(function (group) {
            var toggleButton = group.querySelector('[data-menu-toggle]');
            var submenuItems = Array.from(group.querySelectorAll('[data-menu-item]'));
            var label = toggleButton ? (toggleButton.dataset.menuLabel || toggleButton.textContent || '') : '';
            label = label.trim().toLowerCase();

            var groupMatches = hasTerm && label.indexOf(normalizedTerm) !== -1;
            var childHasMatch = false;

            submenuItems.forEach(function (item) {
                var link = item.querySelector('[data-menu-link]');
                var text = link ? link.textContent.trim().toLowerCase() : '';
                var matches = hasTerm && text.indexOf(normalizedTerm) !== -1;
                childHasMatch = childHasMatch || matches;
                item.classList.toggle('odoo-menu-item--hidden', hasTerm && !matches && !groupMatches);
            });

            var shouldShowGroup = !hasTerm || groupMatches || childHasMatch;
            group.classList.toggle('odoo-menu-item--hidden', !shouldShowGroup);

            if (!hasTerm) {
                submenuItems.forEach(function (item) {
                    item.classList.remove('odoo-menu-item--hidden');
                });

                if (group.dataset.menuDefaultOpen === 'true') {
                    setGroupOpen(group, true, true);
                } else {
                    setGroupOpen(group, false);
                }
            } else if (shouldShowGroup) {
                setGroupOpen(group, true);
            }
        });

        menuLeaves.forEach(function (leaf) {
            var link = leaf.querySelector('[data-menu-link]');
            var text = link ? link.textContent.trim().toLowerCase() : '';
            var matches = !hasTerm || text.indexOf(normalizedTerm) !== -1;
            leaf.classList.toggle('odoo-menu-item--hidden', !matches);
        });
    };

    if (searchInput) {
        searchInput.addEventListener('input', function (event) {
            filterMenu(event.target.value || '');
        });
    }
});
