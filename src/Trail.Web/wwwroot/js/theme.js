(() => {
    'use strict';

    const storageKey = 'trail-theme';
    const lightTheme = 'trail-light';
    const darkTheme = 'trail-dark';
    const root = document.documentElement;
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)');

    const readStoredTheme = () => {
        try {
            const theme = window.localStorage.getItem(storageKey);
            return theme === lightTheme || theme === darkTheme ? theme : null;
        } catch {
            return null;
        }
    };

    const storeTheme = (theme) => {
        try {
            window.localStorage.setItem(storageKey, theme);
        } catch {
            return;
        }
    };

    const currentTheme = () => root.dataset.theme ?? (prefersDark.matches ? darkTheme : lightTheme);

    const syncToggles = () => {
        const pressed = String(currentTheme() === darkTheme);
        document.querySelectorAll('[data-theme-toggle]').forEach((toggle) => toggle.setAttribute('aria-pressed', pressed));
    };

    const storedTheme = readStoredTheme();
    if (storedTheme) {
        root.dataset.theme = storedTheme;
    }

    document.addEventListener('click', (event) => {
        if (!(event.target instanceof Element) || !event.target.closest('[data-theme-toggle]')) {
            return;
        }

        const theme = currentTheme() === darkTheme ? lightTheme : darkTheme;
        root.dataset.theme = theme;
        storeTheme(theme);
        syncToggles();
    });

    prefersDark.addEventListener('change', syncToggles);
    document.addEventListener('DOMContentLoaded', syncToggles);
})();
