(() => {
    'use strict';

    const invalidSelector = '[aria-invalid="true"]';
    const unprocessableStatus = 422;

    const contextOfTask = new WeakMap();
    const settledByContext = new WeakMap();

    const invalidFieldsIn = (nodes) => nodes
        .filter((node) => node instanceof Element && node.isConnected)
        .flatMap((node) => (node.matches(invalidSelector) ? [node] : []).concat(Array.from(node.querySelectorAll(invalidSelector))));

    const blockedByModal = (field) => {
        const modal = document.querySelector('dialog:modal');
        return modal !== null && !modal.contains(field);
    };

    const focusFirst = (fields) => {
        for (const field of fields) {
            if (blockedByModal(field)) {
                return;
            }

            field.focus();
            if (document.activeElement === field) {
                return;
            }
        }
    };

    const loadedWithUnprocessableStatus = () => {
        const navigation = performance.getEntriesByType('navigation')[0];
        const status = navigation?.responseStatus;
        return !status || status === unprocessableStatus;
    };

    document.addEventListener('htmx:before:swap', (event) => {
        const { ctx, tasks } = event.detail ?? {};
        if (!ctx) {
            return;
        }

        settledByContext.set(ctx, []);
        (tasks ?? []).forEach((task) => contextOfTask.set(task, ctx));
    });

    document.addEventListener('htmx:after:settle', (event) => {
        const ctx = contextOfTask.get(event.detail?.task);
        settledByContext.get(ctx)?.push(...(event.detail.newContent ?? []));
    });

    document.addEventListener('htmx:after:swap', (event) => {
        const ctx = event.detail?.ctx;
        const nodes = settledByContext.get(ctx) ?? [];
        settledByContext.delete(ctx);

        if (ctx?.response?.status !== unprocessableStatus) {
            return;
        }

        focusFirst(invalidFieldsIn(nodes));
    });

    if (loadedWithUnprocessableStatus()) {
        focusFirst(invalidFieldsIn([document.querySelector('main')]));
    }
})();
