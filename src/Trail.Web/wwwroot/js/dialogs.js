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
})();
