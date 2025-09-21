(function () {
    function loadLocalization() {
        if (typeof ej !== 'undefined' && ej.base && ej.base.L10n) {
            ej.base.L10n.load({
                ar: {
                    grid: {
                        EmptyRecord: 'لا توجد بيانات لعرضها.',
                        EmptyDataSourceError: 'لا توجد بيانات متاحة.',
                        Search: 'بحث'
                    },
                    pager: {
                        currentPageInfo: 'صفحة {0} من {1}',
                        totalItemsInfo: '({0} عناصر)',
                        firstPageTooltip: 'الصفحة الأولى',
                        lastPageTooltip: 'الصفحة الأخيرة',
                        nextPageTooltip: 'الصفحة التالية',
                        previousPageTooltip: 'الصفحة السابقة',
                        nextPagerTooltip: 'الصفحات التالية',
                        previousPagerTooltip: 'الصفحات السابقة'
                    }
                }
            });
        }

        if (typeof ej !== 'undefined' && ej.base && typeof ej.base.setCulture === 'function') {
            ej.base.setCulture('ar');
        }
    }

    function formatDate(value) {
        if (!value) {
            return '-';
        }

        var date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '-';
        }

        return date.toLocaleString('ar-EG', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (typeof ej === 'undefined' || !ej.grids || !ej.data) {
            return;
        }

        var gridElement = document.getElementById('usersGrid');
        if (!gridElement) {
            return;
        }

        loadLocalization();

        var antiForgeryTokenInput = document.querySelector('#usersAntiForgery input[name="__RequestVerificationToken"]');
        var antiForgeryToken = antiForgeryTokenInput ? antiForgeryTokenInput.value : '';

        var dataManager = new ej.data.DataManager({
            url: '/Users/GridData',
            adaptor: new ej.data.UrlAdaptor(),
            crossDomain: false
        });

        function statusTemplate(data) {
            return data.IsActive
                ? '<span class="badge bg-success">نعم</span>'
                : '<span class="badge bg-secondary">لا</span>';
        }

        function lastLoginTemplate(data) {
            return '<span>' + formatDate(data.LastLoginAt) + '</span>';
        }

        function actionTemplate(data) {
            var toggleClass = data.IsActive ? 'btn-danger' : 'btn-success';
            var toggleText = data.IsActive ? 'إيقاف' : 'تفعيل';
            var toggleIcon = data.IsActive ? 'fa-user-slash' : 'fa-user';

            var actions = [
                '<div class="btn-group btn-group-sm user-actions" role="group">',
                '<a class="btn btn-warning" href="/Users/Edit/' + data.Id + '" title="تعديل"><i class="fas fa-edit"></i></a>',
                '<a class="btn btn-primary" href="/Users/ManagePermissions/' + data.Id + '" title="الصلاحيات"><i class="fas fa-key"></i></a>',
                '<a class="btn btn-secondary" href="/Users/ResetPassword/' + data.Id + '" title="إعادة ضبط كلمة المرور"><i class="fas fa-lock"></i></a>',
                '<button type="button" class="btn ' + toggleClass + ' js-toggle-user" data-id="' + data.Id + '">',
                '<i class="fas ' + toggleIcon + '"></i> ' + toggleText,
                '</button>',
                '</div>'
            ];

            return actions.join('');
        }

        var searchInput = document.getElementById('usersSearch');
        var searchButton = document.getElementById('usersSearchBtn');
        var resetButton = document.getElementById('usersResetBtn');
        var pageInfo = document.getElementById('usersPageInfo');
        var prevButton = document.getElementById('usersPrevPage');
        var nextButton = document.getElementById('usersNextPage');

        var grid = new ej.grids.Grid({
            dataSource: dataManager,
            enableRtl: true,
            locale: 'ar',
            allowPaging: true,
            pageSettings: {
                pageSize: 20,
                pageSizes: [10, 20, 50, 100],
                pageCount: 4
            },
            allowSorting: true,
            allowTextWrap: true,
            textWrapSettings: { wrapMode: 'Content' },
            rowHeight: 38,
            height: 480,
            searchSettings: { fields: ['Email', 'FullName'] },
            loadingIndicator: { indicatorType: 'Spinner' },
            gridLines: 'Both',
            columns: [
                { field: 'Email', headerText: 'البريد الإلكتروني', width: 220, textAlign: 'Right', clipMode: 'EllipsisWithTooltip' },
                { field: 'FullName', headerText: 'الاسم', width: 180, textAlign: 'Right', clipMode: 'EllipsisWithTooltip' },
                { field: 'IsActive', headerText: 'نشط', width: 100, textAlign: 'Center', template: statusTemplate },
                { field: 'LastLoginAt', headerText: 'آخر تسجيل دخول', width: 200, textAlign: 'Right', template: lastLoginTemplate },
                { field: 'Id', headerText: 'الإجراءات', width: 280, textAlign: 'Center', template: actionTemplate, allowSorting: false }
            ],
            dataBound: function () {
                updatePageInfo();
            },
            actionComplete: function (args) {
                var types = ['paging', 'sorting', 'refresh', 'searching'];
                if (types.indexOf(args.requestType) !== -1) {
                    updatePageInfo();
                }
            }
        });

        grid.appendTo(gridElement);

        function updatePageInfo() {
            if (!pageInfo) {
                return;
            }

            var pager = grid.pagerModule && grid.pagerModule.pagerObj;
            var currentPage = pager && pager.currentPage ? pager.currentPage : grid.pageSettings.currentPage || 1;
            var pageSize = pager && pager.pageSize ? pager.pageSize : grid.pageSettings.pageSize || 1;
            var totalRecords = pager && typeof pager.totalRecordsCount === 'number'
                ? pager.totalRecordsCount
                : grid.pageSettings.totalRecordsCount || 0;

            var totalPages = totalRecords > 0 ? Math.ceil(totalRecords / pageSize) : 1;
            pageInfo.textContent = 'صفحة ' + currentPage + ' من ' + totalPages;

            if (prevButton) {
                prevButton.disabled = currentPage <= 1;
            }
            if (nextButton) {
                nextButton.disabled = currentPage >= totalPages;
            }
        }

        if (searchButton && searchInput) {
            searchButton.addEventListener('click', function (event) {
                event.preventDefault();
                grid.search(searchInput.value);
            });
        }

        if (searchInput) {
            searchInput.addEventListener('keyup', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    grid.search(searchInput.value);
                }
            });
        }

        if (resetButton && searchInput) {
            resetButton.addEventListener('click', function (event) {
                event.preventDefault();
                if (searchInput.value) {
                    searchInput.value = '';
                }
                grid.search('');
            });
        }

        if (prevButton) {
            prevButton.addEventListener('click', function () {
                if (grid.pageSettings.currentPage > 1) {
                    grid.goToPage(grid.pageSettings.currentPage - 1);
                }
            });
        }

        if (nextButton) {
            nextButton.addEventListener('click', function () {
                var pager = grid.pagerModule && grid.pagerModule.pagerObj;
                var totalRecords = pager && typeof pager.totalRecordsCount === 'number'
                    ? pager.totalRecordsCount
                    : grid.pageSettings.totalRecordsCount || 0;
                var pageSize = pager && pager.pageSize ? pager.pageSize : grid.pageSettings.pageSize || 1;
                var totalPages = totalRecords > 0 ? Math.ceil(totalRecords / pageSize) : 1;
                if (grid.pageSettings.currentPage < totalPages) {
                    grid.goToPage(grid.pageSettings.currentPage + 1);
                }
            });
        }

        gridElement.addEventListener('click', function (event) {
            var button = event.target.closest('.js-toggle-user');
            if (!button || !antiForgeryToken) {
                return;
            }

            var userId = button.getAttribute('data-id');
            if (!userId) {
                return;
            }

            button.disabled = true;

            fetch('/Users/ToggleActive/' + userId, {
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
                    if (data && data.success) {
                        grid.refresh();
                    } else {
                        alert('تعذر تحديث حالة المستخدم.');
                    }
                })
                .catch(function () {
                    alert('حدث خطأ أثناء تحديث حالة المستخدم.');
                })
                .finally(function () {
                    button.disabled = false;
                });
        });
    });
})();
