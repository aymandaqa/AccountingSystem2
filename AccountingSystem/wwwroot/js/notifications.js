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
        const toastContainer = document.getElementById('notificationToastContainer');

        const audioContextType = window.AudioContext || window.webkitAudioContext;
        let audioContext;

        const ensureAudioContext = () => {
            if (!audioContextType) {
                return null;
            }

            if (!audioContext) {
                audioContext = new audioContextType();
            }

            return audioContext;
        };

        const playNotificationSound = () => {
            const context = ensureAudioContext();
            if (!context) {
                return;
            }

            if (context.state === 'suspended') {
                context.resume().catch(() => { /* ignore resume errors */ });
            }

            const oscillator = context.createOscillator();
            const gainNode = context.createGain();

            const startTime = context.currentTime;

            oscillator.type = 'triangle';
            oscillator.frequency.setValueAtTime(660, startTime);
            oscillator.frequency.exponentialRampToValueAtTime(440, startTime + 0.6);

            gainNode.gain.setValueAtTime(0.0001, startTime);
            gainNode.gain.exponentialRampToValueAtTime(0.25, startTime + 0.02);
            gainNode.gain.exponentialRampToValueAtTime(0.0001, startTime + 1);

            oscillator.connect(gainNode);
            gainNode.connect(context.destination);

            oscillator.start(startTime);
            oscillator.stop(startTime + 1);

            oscillator.onended = () => {
                oscillator.disconnect();
                gainNode.disconnect();
            };
        };

        const showToastNotification = (notification) => {
            if (!notification || !notification.workflowActionId) {
                return;
            }

            playNotificationSound();

            if (!toastContainer || typeof bootstrap === 'undefined') {
                return;
            }

            const toastElement = document.createElement('div');
            toastElement.className = 'toast notification-toast shadow border-0 text-bg-primary';
            toastElement.setAttribute('role', 'alert');
            toastElement.setAttribute('aria-live', 'assertive');
            toastElement.setAttribute('aria-atomic', 'true');
            toastElement.dataset.bsAutohide = 'true';
            toastElement.dataset.bsDelay = '8000';

            const toastContent = document.createElement('div');
            toastContent.className = 'toast-body d-flex align-items-start gap-3';

            const iconClass = notification.icon || 'fa-diagram-project';
            if (iconClass) {
                const iconElement = document.createElement('i');
                iconElement.className = `fa ${iconClass} fs-4 text-warning flex-shrink-0`;
                toastContent.appendChild(iconElement);
            }

            const textWrapper = document.createElement('div');

            const titleElement = document.createElement('strong');
            titleElement.className = 'd-block mb-1';
            titleElement.textContent = notification.title || 'إشعار جديد';
            textWrapper.appendChild(titleElement);

            if (notification.message) {
                const messageElement = document.createElement('div');
                messageElement.className = 'small text-muted';
                messageElement.textContent = notification.message;
                textWrapper.appendChild(messageElement);
            }

            toastContent.appendChild(textWrapper);

            toastElement.appendChild(toastContent);

            const closeButton = document.createElement('button');
            closeButton.type = 'button';
            closeButton.className = 'btn-close me-2 m-auto';
            closeButton.setAttribute('data-bs-dismiss', 'toast');
            closeButton.setAttribute('aria-label', 'إغلاق');

            const footer = document.createElement('div');
            footer.className = 'd-flex align-items-center justify-content-end gap-2 px-3 pb-3';
            footer.appendChild(closeButton);

            if (notification.link) {
                const openLink = document.createElement('a');
                openLink.className = 'btn btn-sm btn-outline-light';
                openLink.href = notification.link;
                openLink.textContent = 'عرض التفاصيل';
                footer.appendChild(openLink);
            }

            toastElement.appendChild(footer);

            toastContainer.appendChild(toastElement);

            toastElement.addEventListener('hidden.bs.toast', () => {
                toastElement.remove();
            });

            if (notification.link) {
                toastElement.addEventListener('click', (event) => {
                    const isButton = event.target.closest('button, a');
                    if (!isButton) {
                        window.location.href = notification.link;
                    }
                });
            }

            const toastInstance = new bootstrap.Toast(toastElement, {
                autohide: true,
                delay: 8000
            });
            toastInstance.show();
        };

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
            if (notification.workflowActionId) {
                item.dataset.workflowActionId = notification.workflowActionId;
            }
            item.href = notification.link || '#';

            const container = document.createElement('div');
            container.className = 'd-flex align-items-start';

            const iconClass = notification.icon || (notification.workflowActionId ? 'fa-diagram-project' : '');
            if (iconClass) {
                const icon = document.createElement('i');
                icon.className = `fa ${iconClass} me-2 text-primary`;
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

            showToastNotification(notification);
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
