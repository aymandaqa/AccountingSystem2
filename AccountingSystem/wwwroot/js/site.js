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

    // Highlight active sidebar link based on current path
    var currentPath = window.location.pathname.toLowerCase();
    document.querySelectorAll('#sidebar .nav-link').forEach(function (link) {
        if (link.hasAttribute('data-bs-toggle')) return; // Skip collapse toggles
        var linkPath = link.pathname.toLowerCase();
        if (currentPath === linkPath || currentPath.startsWith(linkPath + '/')) {
            link.classList.add('active');
            var parent = link.closest('.collapse');
            if (parent) parent.classList.add('show');
        }
    });
});
