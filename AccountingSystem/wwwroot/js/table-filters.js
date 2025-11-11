(function () {
    "use strict";

    const tables = document.querySelectorAll('[data-table-filter]');
    if (!tables.length) {
        return;
    }

    const getNormalizedText = (value) => (value ?? "").trim().toLowerCase();

    tables.forEach((table) => {
        const tbody = table.tBodies.length ? table.tBodies[0] : null;
        if (!tbody) {
            return;
        }

        const dataRows = Array.from(tbody.querySelectorAll('tr')).filter((row) => !row.classList.contains('no-results'));
        const noResultsRow = tbody.querySelector('tr.no-results');
        const filterInputs = table.querySelectorAll('[data-filter-column]');

        const applyFilters = () => {
            const filters = {};

            filterInputs.forEach((input) => {
                const columnIndex = Number.parseInt(input.dataset.filterColumn ?? "", 10);
                if (Number.isNaN(columnIndex)) {
                    return;
                }

                const type = input.dataset.filterType || (input.tagName === 'SELECT' ? 'select' : 'text');
                const rawValue = input.value;

                switch (type) {
                    case 'text': {
                        const normalized = getNormalizedText(rawValue);
                        if (!normalized) {
                            return;
                        }
                        (filters[columnIndex] ??= {}).text = normalized;
                        break;
                    }
                    case 'select': {
                        const normalized = getNormalizedText(rawValue);
                        if (!normalized) {
                            return;
                        }
                        (filters[columnIndex] ??= {}).select = normalized;
                        break;
                    }
                    case 'number-min': {
                        if (rawValue === '') {
                            return;
                        }
                        const number = Number.parseFloat(rawValue);
                        if (Number.isNaN(number)) {
                            return;
                        }
                        (filters[columnIndex] ??= {}).numberMin = number;
                        break;
                    }
                    case 'number-max': {
                        if (rawValue === '') {
                            return;
                        }
                        const number = Number.parseFloat(rawValue);
                        if (Number.isNaN(number)) {
                            return;
                        }
                        (filters[columnIndex] ??= {}).numberMax = number;
                        break;
                    }
                    case 'date-min': {
                        if (!rawValue) {
                            return;
                        }
                        (filters[columnIndex] ??= {}).dateMin = rawValue;
                        break;
                    }
                    case 'date-max': {
                        if (!rawValue) {
                            return;
                        }
                        (filters[columnIndex] ??= {}).dateMax = rawValue;
                        break;
                    }
                    default:
                        break;
                }
            });

            const activeColumns = Object.entries(filters);
            let visibleCount = 0;

            dataRows.forEach((row) => {
                let isVisible = true;

                for (const [columnKey, config] of activeColumns) {
                    const columnIndex = Number.parseInt(columnKey, 10);
                    const cell = row.children[columnIndex];
                    if (!cell) {
                        continue;
                    }

                    const rawValue = (cell.dataset.filterValue ?? cell.textContent ?? '').trim();
                    const normalized = rawValue.toLowerCase();

                    if (config.text && !normalized.includes(config.text)) {
                        isVisible = false;
                        break;
                    }

                    if (config.select && normalized !== config.select) {
                        isVisible = false;
                        break;
                    }

                    if (config.numberMin !== undefined || config.numberMax !== undefined) {
                        const numericRaw = cell.dataset.filterNumeric ?? rawValue.replace(/[^0-9+\-.,]/g, '').replace(',', '.');
                        const numericValue = Number.parseFloat(numericRaw);
                        if (Number.isNaN(numericValue)) {
                            isVisible = false;
                            break;
                        }

                        if (config.numberMin !== undefined && numericValue < config.numberMin) {
                            isVisible = false;
                            break;
                        }

                        if (config.numberMax !== undefined && numericValue > config.numberMax) {
                            isVisible = false;
                            break;
                        }
                    }

                    if (config.dateMin || config.dateMax) {
                        const dateValue = cell.dataset.filterDate ?? '';
                        if (!dateValue) {
                            isVisible = false;
                            break;
                        }

                        if (config.dateMin && dateValue < config.dateMin) {
                            isVisible = false;
                            break;
                        }

                        if (config.dateMax && dateValue > config.dateMax) {
                            isVisible = false;
                            break;
                        }
                    }
                }

                row.classList.toggle('d-none', !isVisible);
                if (isVisible) {
                    visibleCount += 1;
                }
            });

            if (noResultsRow) {
                noResultsRow.classList.toggle('d-none', visibleCount !== 0);
            }
        };

        filterInputs.forEach((input) => {
            const eventName = input.tagName === 'SELECT' ? 'change' : 'input';
            input.addEventListener(eventName, applyFilters);
        });

        const resetButtonSelector = table.dataset.tableFilterReset;
        if (resetButtonSelector) {
            const resetButton = document.querySelector(resetButtonSelector);
            if (resetButton) {
                resetButton.addEventListener('click', (event) => {
                    event.preventDefault();
                    filterInputs.forEach((input) => {
                        if (input.tagName === 'SELECT') {
                            input.selectedIndex = 0;
                        } else {
                            input.value = '';
                        }
                    });
                    applyFilters();
                });
            }
        }

        applyFilters();
    });
})();
