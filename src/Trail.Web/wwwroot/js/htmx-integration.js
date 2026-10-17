(() => {
    'use strict';

    const htmxInstance = typeof htmx !== 'undefined' ? htmx : window.htmx;
    if (htmxInstance?.registerExtension) {
        htmxInstance.registerExtension('hx-csp', {
            init: (api) => {
                if (api?.initSecurity) {
                    const forbidEval = () => {
                        throw new Error('Eval is disabled under CSP.');
                    };
                    api.initSecurity(null, forbidEval, forbidEval);
                }
            },
            htmx_before_on_init: () => false
        });
    }

    document.addEventListener('htmx:config:request', (event) => {
        const token = document.querySelector('meta[name="csrf-token"]');
        const headerName = token?.dataset.headerName;
        if (headerName && token.content) {
            event.detail.ctx.request.headers[headerName] = token.content;
        }
    });

    const showUnexpectedError = () => {
        const template = document.getElementById('toast-unexpected-error');
        const container = document.getElementById('toasts');
        if (template instanceof HTMLTemplateElement && container) {
            container.append(template.content.cloneNode(true));
        }
    };

    document.addEventListener('htmx:before:swap', (event) => {
        const { ctx } = event.detail;
        if (ctx.response?.status >= 400 && !ctx.text?.trim()) {
            event.preventDefault();
            showUnexpectedError();
        }
    });
})();
