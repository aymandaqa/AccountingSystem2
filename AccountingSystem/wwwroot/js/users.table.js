(function () {
    document.addEventListener('DOMContentLoaded', function () {
        var usersTable = document.getElementById('usersTable');
        if (!usersTable) {
            return;
        }

        var antiForgeryInput = document.querySelector('#usersAntiForgery input[name="__RequestVerificationToken"]');
        var antiForgeryToken = antiForgeryInput ? antiForgeryInput.value : '';
        if (!antiForgeryToken) {
            return;
        }

        usersTable.addEventListener('click', function (event) {
            var button = event.target.closest('.js-toggle-user');
            if (!button) {
                return;
            }

            var userId = button.getAttribute('data-id');
            if (!userId) {
                return;
            }

            button.disabled = true;

            fetch('/Users/ToggleActive/' + encodeURIComponent(userId), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: '__RequestVerificationToken=' + encodeURIComponent(antiForgeryToken)
            })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('Request failed');
                    }
                    return response.json();
                })
                .then(function (data) {
                    if (!data || !data.success) {
                        throw new Error('Invalid response');
                    }

                    var isActive = !!data.isActive;
                    updateStatus(button, isActive);
                })
                .catch(function () {
                    alert('حدث خطأ أثناء تحديث حالة المستخدم.');
                })
                .finally(function () {
                    button.disabled = false;
                });
        });

        function updateStatus(button, isActive) {
            updateStatusBadge(button, isActive);
            updateToggleButton(button, isActive);
        }

        function updateStatusBadge(button, isActive) {
            var row = button.closest('tr');
            if (!row) {
                return;
            }

            var badge = row.querySelector('.js-user-status');
            if (!badge) {
                return;
            }

            badge.textContent = isActive ? 'نعم' : 'لا';
            badge.classList.remove('bg-success', 'bg-secondary');
            badge.classList.add(isActive ? 'bg-success' : 'bg-secondary');
        }

        function updateToggleButton(button, isActive) {
            button.classList.remove('btn-danger', 'btn-success');
            button.classList.add(isActive ? 'btn-danger' : 'btn-success');
            button.innerHTML = '<i class="fas ' + (isActive ? 'fa-user-slash' : 'fa-user') + '"></i> ' + (isActive ? 'إيقاف' : 'تفعيل');
        }
    });
})();
