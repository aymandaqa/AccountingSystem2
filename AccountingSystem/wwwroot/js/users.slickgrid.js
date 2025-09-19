(function ($) {
    function initializeUsersGrid() {
        const $gridContainer = $("#usersGrid");
        if ($gridContainer.length === 0 || typeof Slick === "undefined") {
            return;
        }

        const state = {
            page: 1,
            pageSize: 20,
            sortField: "Email",
            sortOrder: "asc",
            search: ""
        };

        function actionFormatter(row, cell, value, columnDef, dataContext) {
            const toggleClass = dataContext.isActive ? "btn-danger" : "btn-success";
            const toggleText = dataContext.isActive ? "إيقاف" : "تفعيل";
            const toggleIcon = dataContext.isActive ? "fa-user-slash" : "fa-user";

            return [
                '<div class="btn-group btn-group-sm" role="group">',
                `<a class="btn btn-warning" href="/Users/Edit/${dataContext.id}"><i class="fas fa-edit"></i></a>`,
                `<a class="btn btn-primary" href="/Users/ManagePermissions/${dataContext.id}"><i class="fas fa-key"></i></a>`,
                `<a class="btn btn-secondary" href="/Users/ResetPassword/${dataContext.id}"><i class="fas fa-lock"></i></a>`,
                `<button type="button" class="btn ${toggleClass} js-toggle-user" data-id="${dataContext.id}">`,
                `<i class="fas ${toggleIcon}"></i> ${toggleText}`,
                "</button>",
                "</div>"
            ].join("");
        }

        const columns = [
            {
                id: "email",
                name: "البريد الإلكتروني",
                field: "email",
                sortField: "Email",
                sortable: true,
                minWidth: 180,
                formatter: function (row, cell, value) {
                    return value || "";
                }
            },
            {
                id: "fullName",
                name: "الاسم",
                field: "fullName",
                sortField: "FullName",
                sortable: true,
                minWidth: 160,
                formatter: function (row, cell, value) {
                    return value || "";
                }
            },
            {
                id: "isActive",
                name: "نشط",
                field: "isActive",
                sortField: "IsActive",
                sortable: true,
                width: 100,
                formatter: function (row, cell, value) {
                    return value
                        ? '<span class="badge bg-success">نعم</span>'
                        : '<span class="badge bg-secondary">لا</span>';
                }
            },
            {
                id: "lastLoginAt",
                name: "آخر تسجيل دخول",
                field: "lastLoginFormatted",
                sortField: "LastLoginAt",
                sortable: true,
                minWidth: 180,
                formatter: function (row, cell, value, columnDef, dataContext) {
                    return dataContext.lastLoginFormatted || "-";
                }
            },
            {
                id: "actions",
                name: "الإجراءات",
                field: "id",
                sortable: false,
                width: 260,
                formatter: actionFormatter
            }
        ];

        const options = {
            enableColumnReorder: false,
            enableCellNavigation: true,
            explicitInitialization: true,
            forceFitColumns: true,
            rowHeight: 38,
            headerRowHeight: 38,
            enableTextSelectionOnCells: true
        };

        const dataView = new Slick.Data.DataView({ inlineFilters: false });
        const grid = new Slick.Grid("#usersGrid", dataView, columns, options);
        grid.setSelectionModel(new Slick.RowSelectionModel({ selectActiveRow: false }));

        dataView.onRowCountChanged.subscribe(function () {
            grid.updateRowCount();
            grid.render();
        });

        dataView.onRowsChanged.subscribe(function (e, args) {
            grid.invalidateRows(args.rows);
            grid.render();
        });

        grid.onSort.subscribe(function (e, args) {
            state.sortField = args.sortCol.sortField || args.sortCol.field || "Email";
            state.sortOrder = args.sortAsc ? "asc" : "desc";
            state.page = 1;
            loadData();
        });

        const $pageInfo = $("#usersPageInfo");
        const $prevPage = $("#usersPrevPage");
        const $nextPage = $("#usersNextPage");
        const $searchInput = $("#usersSearch");
        const $searchBtn = $("#usersSearchBtn");
        const $resetBtn = $("#usersResetBtn");
        const $emptyPlaceholder = $("#usersGridEmpty");
        const antiForgeryToken = $("#usersAntiForgery input[name='__RequestVerificationToken']").val() || "";

        function setLoading(isLoading) {
            $gridContainer.toggleClass("loading", isLoading);
        }

        function updatePager(totalCount) {
            const totalPages = Math.max(Math.ceil(totalCount / state.pageSize), 1);
            state.page = Math.min(state.page, totalPages);

            $pageInfo.text(`صفحة ${state.page} من ${totalPages}`);
            $prevPage.prop("disabled", state.page <= 1);
            $nextPage.prop("disabled", state.page >= totalPages);
        }

        function formatDate(value) {
            if (!value) {
                return "-";
            }
            const date = new Date(value);
            if (Number.isNaN(date.getTime())) {
                return value;
            }
            return date.toLocaleString("ar-EG", {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit"
            });
        }

        function loadData() {
            setLoading(true);
            $.ajax({
                url: "/Users/GridData",
                method: "GET",
                data: {
                    page: state.page,
                    pageSize: state.pageSize,
                    sortField: state.sortField,
                    sortOrder: state.sortOrder,
                    search: state.search
                }
            })
                .done(function (response) {
                    const items = (response.items || []).map(function (item) {
                        return {
                            id: item.id,
                            email: item.email,
                            fullName: item.fullName,
                            isActive: item.isActive,
                            lastLoginAt: item.lastLoginAt,
                            lastLoginFormatted: formatDate(item.lastLoginAt)
                        };
                    });

                    dataView.beginUpdate();
                    dataView.setItems(items, "id");
                    dataView.endUpdate();
                    grid.invalidate();
                    grid.render();

                    $emptyPlaceholder.toggleClass("d-none", items.length > 0);
                    updatePager(response.totalCount || 0);
                })
                .fail(function () {
                    alert("حدث خطأ أثناء تحميل بيانات المستخدمين.");
                })
                .always(function () {
                    setLoading(false);
                });
        }

        $prevPage.on("click", function () {
            if (state.page > 1) {
                state.page -= 1;
                loadData();
            }
        });

        $nextPage.on("click", function () {
            state.page += 1;
            loadData();
        });

        $searchBtn.on("click", function (event) {
            event.preventDefault();
            state.search = $searchInput.val();
            state.page = 1;
            loadData();
        });

        $searchInput.on("keyup", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                state.search = $searchInput.val();
                state.page = 1;
                loadData();
            }
        });

        $resetBtn.on("click", function (event) {
            event.preventDefault();
            if ($searchInput.val()) {
                $searchInput.val("");
            }
            state.search = "";
            state.page = 1;
            loadData();
        });

        $(document).on("click", ".js-toggle-user", function () {
            const $button = $(this);
            const userId = $button.data("id");
            if (!userId || !antiForgeryToken) {
                return;
            }

            $button.prop("disabled", true);

            fetch(`/Users/ToggleActive/${userId}`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: `__RequestVerificationToken=${encodeURIComponent(antiForgeryToken)}`
            })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error("Request failed");
                    }
                    return response.json();
                })
                .then(function (data) {
                    if (data && data.success) {
                        loadData();
                    } else {
                        alert("تعذر تحديث حالة المستخدم.");
                    }
                })
                .catch(function () {
                    alert("حدث خطأ أثناء تحديث حالة المستخدم.");
                })
                .finally(function () {
                    $button.prop("disabled", false);
                });
        });

        grid.init();
        grid.setSortColumn("email", true);
        loadData();

        $(window).on("resize.usersGrid", function () {
            grid.resizeCanvas();
        });
    }

    $(function () {
        initializeUsersGrid();
    });
})(jQuery);
