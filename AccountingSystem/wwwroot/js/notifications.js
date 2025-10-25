(function () {
    function ready(callback) {
        if (document.readyState !== 'loading') {
            callback();
        } else {
            document.addEventListener('DOMContentLoaded', callback);
        }
    }

    ready(function () {
        const hubUrl = '/hubs/notifications';
        const wrapper = document.getElementById('notificationsDropdownWrapper');
        const list = document.getElementById('notificationsList');
        const badge = document.getElementById('notificationsBadge');

        if (!wrapper || !list || typeof signalR === 'undefined') {
            return;
        }

        const emptyText = list.dataset.emptyText || '';
        const maxItems = parseInt(list.dataset.maxItems || '5', 10);

        const ensureEmptyMessage = () => {
            if (list.querySelector('[data-notification-id]')) {
                const existing = document.getElementById('notificationsEmptyMessage');
                if (existing) {
                    existing.remove();
                }
                return;
            }

            if (!document.getElementById('notificationsEmptyMessage')) {
                const emptyElement = document.createElement('div');
                emptyElement.id = 'notificationsEmptyMessage';
                emptyElement.className = 'text-center text-muted py-3';
                emptyElement.textContent = emptyText || 'لا توجد إشعارات';
                list.appendChild(emptyElement);
            }
        };

        ensureEmptyMessage();

        const updateBadge = (count) => {
            if (!badge) {
                return;
            }

            if (count > 0) {
                badge.textContent = count;
                badge.classList.remove('d-none');
            } else {
                badge.classList.add('d-none');
            }
        };

        const removeNotification = (id) => {
            const item = list.querySelector(`[data-notification-id="${id}"]`);
            if (item) {
                item.remove();
            }
            ensureEmptyMessage();
        };

        const clearNotifications = () => {
            list.querySelectorAll('[data-notification-id]').forEach((item) => item.remove());
            ensureEmptyMessage();
        };

        const renderNotification = (notification) => {
            if (!notification) {
                return;
            }

            const item = document.createElement('a');
            item.className = 'list-group-item list-group-item-action fw-bold';
            item.dataset.notificationId = notification.id;
            item.href = notification.link || '#';

            const container = document.createElement('div');
            container.className = 'd-flex align-items-start';

            if (notification.icon) {
                const icon = document.createElement('i');
                icon.className = `fa ${notification.icon} me-2 text-primary`;
                container.appendChild(icon);
            }

            const content = document.createElement('div');
            const title = document.createElement('div');
            title.textContent = notification.title;
            content.appendChild(title);

            if (notification.message) {
                const message = document.createElement('small');
                message.className = 'text-muted';
                message.textContent = notification.message;
                content.appendChild(message);
            }

            container.appendChild(content);
            item.appendChild(container);

            list.insertBefore(item, list.firstChild);

            const items = list.querySelectorAll('[data-notification-id]');
            if (items.length > maxItems) {
                for (let i = maxItems; i < items.length; i += 1) {
                    items[i].remove();
                }
            }

            const existingEmpty = document.getElementById('notificationsEmptyMessage');
            if (existingEmpty) {
                existingEmpty.remove();
            }
        };

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl)
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveNotification', (notification) => {
            renderNotification(notification);
        });

        connection.on('UnreadCountUpdated', (count) => {
            updateBadge(parseInt(count, 10) || 0);
        });

        connection.on('NotificationMarkedAsRead', (id) => {
            removeNotification(id);
        });

        connection.on('NotificationsCleared', () => {
            clearNotifications();
        });

        connection.start().catch((error) => {
            console.error('Failed to connect to notification hub:', error);
        });
    });
})();
