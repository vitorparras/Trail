(() => {
    'use strict';

    const ChartLibrary = window.Chart;
    if (!ChartLibrary) {
        return;
    }

    const locale = document.documentElement.lang || 'pt-BR';
    const canvasSelector = 'canvas[data-chart]';
    const darkQuery = window.matchMedia('(prefers-color-scheme: dark)');
    const reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
    const seriesTokens = {
        primary: '--color-primary',
        secondary: '--color-secondary',
        accent: '--color-accent',
        info: '--color-info',
        success: '--color-success',
        error: '--color-error'
    };
    const fallbackColor = [90, 90, 90];
    const minHoverContrast = 1.5;
    const maxDecimals = 10;
    const dashPattern = [6, 4];
    const rowHeight = 12;
    const barHeight = 20;
    const frameBaseHeight = 48;
    const frameMinHeight = 192;

    const live = new Map();
    const probe = document.createElement('canvas');
    probe.width = 1;
    probe.height = 1;
    const probeContext = probe.getContext('2d', { willReadFrequently: true });

    const toRgb = (value, fallback) => {
        if (!probeContext || !value) {
            return fallback;
        }

        probeContext.clearRect(0, 0, 1, 1);
        probeContext.fillStyle = 'rgba(0, 0, 0, 0)';
        probeContext.fillStyle = value;
        probeContext.fillRect(0, 0, 1, 1);
        const [red, green, blue, alpha] = probeContext.getImageData(0, 0, 1, 1).data;

        return alpha === 0 ? fallback : [red, green, blue];
    };

    const css = ([red, green, blue], alpha = 1) =>
        alpha === 1 ? `rgb(${red}, ${green}, ${blue})` : `rgba(${red}, ${green}, ${blue}, ${alpha})`;

    const mix = (from, to, amount) => from.map((channel, index) => Math.round(channel + (to[index] - channel) * amount));

    const luminance = ([red, green, blue]) => {
        const linear = (channel) => {
            const value = channel / 255;
            return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
        };

        return 0.2126 * linear(red) + 0.7152 * linear(green) + 0.0722 * linear(blue);
    };

    const contrast = (first, second) => {
        const [lighter, darker] = [luminance(first), luminance(second)].sort((left, right) => right - left);

        return (lighter + 0.05) / (darker + 0.05);
    };

    const hoverColor = (base, palette) => {
        const target = palette.lightSurface ? [0, 0, 0] : [255, 255, 255];
        let amount = 0.2;
        let hover = mix(base, target, amount);
        while (contrast(base, hover) < minHoverContrast && amount < 0.9) {
            amount += 0.1;
            hover = mix(base, target, amount);
        }

        return hover;
    };

    const readPalette = () => {
        const styles = getComputedStyle(document.documentElement);
        const token = (name, fallback) => toRgb(styles.getPropertyValue(name).trim(), fallback);
        const content = token('--color-base-content', fallbackColor);
        const series = {};
        for (const [name, property] of Object.entries(seriesTokens)) {
            series[name] = token(property, fallbackColor);
        }

        return {
            content,
            grid: mix(token('--color-base-100', [255, 255, 255]), content, 0.18),
            surface: token('--color-base-100', [255, 255, 255]),
            lightSurface: luminance(token('--color-base-100', [255, 255, 255])) > 0.5,
            series
        };
    };

    const makeFormatter = (format, forTicks) => {
        const decimals = format?.decimals ?? 0;
        const suffix = format?.suffix ?? '';
        const numberFormat = new Intl.NumberFormat(locale, {
            minimumFractionDigits: forTicks ? 0 : decimals,
            maximumFractionDigits: decimals
        });

        return (value) => `${numberFormat.format(value)}${suffix}`;
    };

    const truncate = (text, limit) => (text.length > limit ? `${text.slice(0, limit - 1)}…` : text);

    const seriesColors = (series, palette) => {
        const base = palette.series[String(series.color).toLowerCase()] ?? palette.series.primary;
        const hover = hoverColor(base, palette);
        const isLine = String(series.kind).toLowerCase() === 'line';

        return isLine
            ? {
                borderColor: css(base),
                backgroundColor: css(base),
                pointBackgroundColor: css(base),
                pointBorderColor: css(palette.surface),
                hoverBorderColor: css(hover),
                hoverBackgroundColor: css(hover),
                pointHoverBackgroundColor: css(hover),
                pointHoverBorderColor: css(palette.surface)
            }
            : {
                borderColor: css(base),
                backgroundColor: css(base),
                hoverBorderColor: css(hover),
                hoverBackgroundColor: css(hover)
            };
    };

    const buildDatasets = (model, palette) =>
        model.series.map((series) => {
            const isLine = String(series.kind).toLowerCase() === 'line';

            return {
                type: isLine ? 'line' : 'bar',
                label: series.label,
                data: series.values,
                order: isLine ? 0 : 1,
                spanGaps: model.spanGaps === true,
                borderWidth: isLine ? 2 : 1,
                borderDash: isLine && series.dashed ? dashPattern : [],
                pointStyle: isLine ? 'circle' : 'rect',
                pointRadius: isLine ? 3 : 0,
                pointHoverRadius: isLine ? 5 : 0,
                tension: 0,
                maxBarThickness: 32,
                ...seriesColors(series, palette)
            };
        });

    const buildOptions = (model, palette, canvas) => {
        const horizontal = model.horizontal === true;
        const content = css(palette.content);
        const grid = css(palette.grid);
        const tickFormatter = makeFormatter(model.format, true);
        const tooltipFormatter = makeFormatter(model.format, false);
        const font = { family: getComputedStyle(canvas).fontFamily, size: 12 };

        const categoryScale = {
            type: 'category',
            grid: { display: false },
            border: { color: grid },
            ticks: {
                color: content,
                font,
                autoSkip: !horizontal,
                maxRotation: horizontal ? 0 : 45,
                callback(value) {
                    const label = this.getLabelForValue(value);
                    const limit = this.chart.width < 480 ? 24 : 40;

                    return truncate(String(label), limit);
                }
            }
        };

        const valueScale = {
            type: 'linear',
            beginAtZero: true,
            border: { color: grid },
            grid: {
                color: (context) => (context.tick?.value === 0 ? content : grid),
                lineWidth: (context) => (context.tick?.value === 0 ? 1.5 : 1)
            },
            ticks: {
                color: content,
                font,
                precision: model.format?.decimals ?? 0,
                callback: (value) => tickFormatter(value)
            }
        };

        if (model.valueAxis) {
            valueScale.min = model.valueAxis.min;
            valueScale.max = model.valueAxis.max;
        }

        const valueKey = horizontal ? 'x' : 'y';

        return {
            responsive: true,
            maintainAspectRatio: false,
            indexAxis: horizontal ? 'y' : 'x',
            ...(reducedMotionQuery.matches ? { animation: false } : {}),
            color: content,
            scales: horizontal ? { y: categoryScale, x: valueScale } : { x: categoryScale, y: valueScale },
            plugins: {
                legend: {
                    display: model.series.length > 1,
                    labels: { color: content, font, usePointStyle: true, boxWidth: 12, boxHeight: 12 }
                },
                tooltip: {
                    callbacks: {
                        label: (item) => {
                            const value = item.parsed[valueKey];

                            return value === null || value === undefined || Number.isNaN(value)
                                ? item.dataset.label
                                : `${item.dataset.label}: ${tooltipFormatter(value)}`;
                        }
                    }
                }
            }
        };
    };

    const isValid = (model) => {
        const decimals = model?.format?.decimals ?? 0;
        const axis = model?.valueAxis;

        return Array.isArray(model?.labels)
            && Array.isArray(model?.series)
            && model.series.length > 0
            && model.series.every((series) => Array.isArray(series.values) && series.values.length === model.labels.length)
            && Number.isInteger(decimals)
            && decimals >= 0
            && decimals <= maxDecimals
            && (!axis || (Number.isFinite(axis.min) && Number.isFinite(axis.max) && axis.min < axis.max));
    };

    const frameBases = new WeakMap();

    const sizeFrame = (canvas, model) => {
        const frame = canvas.closest('.chart-frame');
        if (!frame) {
            return;
        }

        let base;
        if (model.horizontal === true) {
            const perLabel = model.series.length * barHeight + rowHeight;
            base = Math.max(frameMinHeight, frameBaseHeight + model.labels.length * perLabel);
        } else {
            frame.style.height = '';
            base = frame.offsetHeight;
        }

        frameBases.set(frame, base);
        frame.style.height = `${base}px`;
    };

    const legendFit = {
        id: 'legendFit',
        afterUpdate(chart) {
            const frame = chart.canvas?.closest('.chart-frame');
            const base = frame ? frameBases.get(frame) : undefined;
            if (!base) {
                return;
            }

            const extra = chart.legend?.options.display ? Math.ceil(chart.legend.height) : 0;
            const target = base + extra;
            if (Math.abs(frame.offsetHeight - target) > 1) {
                frame.style.height = `${target}px`;
            }
        }
    };

    const destroyChart = (chart) => {
        live.delete(chart);
        chart.destroy();
    };

    const paint = (chart, palette) => {
        const state = live.get(chart);
        if (!state || !chart.canvas) {
            return;
        }

        chart.data.datasets.forEach((dataset, index) => {
            Object.assign(dataset, seriesColors(state.model.series[index], palette));
        });
        chart.options = buildOptions(state.model, palette, chart.canvas);
        chart.update();
    };

    const isInClosedDetails = (element) => element.closest('details:not([open])') !== null;

    const mount = (canvas) => {
        const raw = canvas.dataset.chart;
        const existing = ChartLibrary.getChart(canvas);
        if (existing) {
            const state = live.get(existing);
            if (state?.raw === raw) {
                sizeFrame(canvas, state.model);
                existing.resize();
                return;
            }

            destroyChart(existing);
        }

        let model;
        try {
            model = JSON.parse(raw);
        } catch (error) {
            console.error('Invalid chart model.', error);
            return;
        }

        if (!isValid(model)) {
            console.error('Invalid chart model.', model);
            return;
        }

        const palette = readPalette();
        sizeFrame(canvas, model);
        const hasBars = model.series.some((series) => String(series.kind).toLowerCase() !== 'line');
        const chart = new ChartLibrary(canvas, {
            type: hasBars ? 'bar' : 'line',
            plugins: [legendFit],
            data: { labels: model.labels, datasets: buildDatasets(model, palette) },
            options: buildOptions(model, palette, canvas)
        });
        live.set(chart, { model, raw });
    };

    const initWithin = (root) => {
        if (!(root instanceof Element)) {
            return;
        }

        const canvases = [...root.querySelectorAll(canvasSelector)];
        if (root.matches(canvasSelector)) {
            canvases.unshift(root);
        }

        for (const canvas of canvases) {
            if (canvas.isConnected && !isInClosedDetails(canvas)) {
                mount(canvas);
            }
        }
    };

    const destroyWithin = (root) => {
        if (!(root instanceof Element)) {
            return;
        }

        for (const chart of [...live.keys()]) {
            if (chart.canvas && root.contains(chart.canvas)) {
                destroyChart(chart);
            }
        }
    };

    const sweep = () => {
        for (const chart of [...live.keys()]) {
            if (!chart.canvas?.isConnected) {
                destroyChart(chart);
            }
        }
    };

    const repaintAll = () => {
        if (live.size === 0) {
            return;
        }

        const palette = readPalette();
        for (const chart of live.keys()) {
            paint(chart, palette);
        }
    };

    const onSwapped = (event) => {
        sweep();
        initWithin(event.target);
    };

    document.addEventListener('htmx:after:process', onSwapped);
    document.addEventListener('htmx:after:settle', onSwapped);
    document.addEventListener('htmx:before:cleanup', (event) => destroyWithin(event.target));

    document.addEventListener('toggle', (event) => {
        if (event.target instanceof HTMLDetailsElement && event.target.open) {
            initWithin(event.target);
        }
    }, true);

    new MutationObserver(repaintAll).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['data-theme']
    });
    darkQuery.addEventListener('change', repaintAll);

    const start = () => initWithin(document.body);
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
