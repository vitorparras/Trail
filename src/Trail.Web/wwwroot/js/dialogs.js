(() => {
    'use strict';

    const watchedQueries = new Set();

    const closeOpenDialogs = (query) => {
        const dialogs = Array.from(document.querySelectorAll('dialog[data-close-when][open]'))
            .filter((dialog) => dialog.dataset.closeWhen === query);
        dialogs.forEach((dialog) => dialog.close());
        if (dialogs.length > 0) {
            document.querySelector('main')?.focus();
        }
    };

    const watch = (query) => {
        if (watchedQueries.has(query)) {
            return;
        }

        watchedQueries.add(query);
        window.matchMedia(query).addEventListener('change', (event) => {
            if (event.matches) {
                closeOpenDialogs(query);
            }
        });
    };

    document.addEventListener('click', (event) => {
        const closer = event.target instanceof Element ? event.target.closest('[data-dialog-close]') : null;
        if (closer) {
            const dialog = closer.closest('dialog');
            if (dialog instanceof HTMLDialogElement && dialog.open) {
                if (closer instanceof HTMLAnchorElement) {
                    event.preventDefault();
                }
                dialog.close();
            }
            return;
        }

        const opener = event.target instanceof Element ? event.target.closest('[data-dialog-open]') : null;
        if (!opener) {
            return;
        }

        const dialog = document.getElementById(opener.dataset.dialogOpen);
        if (!(dialog instanceof HTMLDialogElement) || dialog.open) {
            return;
        }

        if (dialog.dataset.closeWhen) {
            watch(dialog.dataset.closeWhen);
        }

        dialog.showModal();
    });

    document.addEventListener('htmx:after:swap', (event) => {
        const source = event.target instanceof Element ? event.target.closest('[data-dialog-open-after-swap]') : null;
        const status = event.detail?.ctx?.response?.status;
        if (!source || typeof status !== 'number' || status < 200 || status >= 300) {
            return;
        }

        const dialog = document.getElementById(source.dataset.dialogOpenAfterSwap);
        if (dialog instanceof HTMLDialogElement && !dialog.open) {
            dialog.showModal();
        }
    });
})();
