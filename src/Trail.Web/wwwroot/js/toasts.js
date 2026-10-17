(() => {
    'use strict';

    const defaultDuration = 5000;
    const toastSelector = '#toasts > [data-toast]';
    const scheduled = new WeakSet();

    const dismissLater = (toast) => {
        if (scheduled.has(toast) || toast.dataset.toastPersistent === 'true') {
            return;
        }

        scheduled.add(toast);
        const holds = new Set();
        let remaining = Number(toast.parentElement?.dataset.toastDuration) || defaultDuration;
        let startedAt = 0;
        let timer = 0;

        const resume = () => {
            if (holds.size > 0 || timer !== 0) {
                return;
            }

            startedAt = Date.now();
            timer = window.setTimeout(() => toast.remove(), remaining);
        };

        const hold = (reason) => {
            holds.add(reason);
            if (timer !== 0) {
                window.clearTimeout(timer);
                timer = 0;
                remaining -= Date.now() - startedAt;
            }
        };

        const release = (reason) => {
            holds.delete(reason);
            resume();
        };

        toast.addEventListener('mouseenter', () => hold('pointer'));
        toast.addEventListener('mouseleave', () => release('pointer'));
        toast.addEventListener('focusin', () => hold('focus'));
        toast.addEventListener('focusout', () => release('focus'));
        resume();
    };

    const scan = (node) => {
        if (!(node instanceof Element)) {
            return;
        }

        if (node.matches(toastSelector)) {
            dismissLater(node);
        }

        node.querySelectorAll(toastSelector).forEach(dismissLater);
    };

    document.addEventListener('click', (event) => {
        const button = event.target instanceof Element ? event.target.closest('[data-toast-dismiss]') : null;
        const toast = button?.closest('[data-toast]');
        if (!toast) {
            return;
        }

        const hadFocus = toast.contains(document.activeElement);
        toast.remove();
        if (hadFocus) {
            document.getElementById('main-content')?.focus();
        }
    });

    new MutationObserver((mutations) => {
        mutations.forEach((mutation) => mutation.addedNodes.forEach(scan));
    }).observe(document.body, { childList: true, subtree: true });

    scan(document.body);
})();
