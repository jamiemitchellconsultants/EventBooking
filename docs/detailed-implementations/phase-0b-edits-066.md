# 00b — Vocabulary edits 66 (Task 2)

[← Overview](README.md) · [Ontology](../ontology.md)

Cross-layer exact before and after files for Task 2. The predecessor vocabulary appears only in the before side so a small executor can match the edit without guessing. After files contain the full replacement; part numbers continue long files without omitted code.

## before — src/EventBooking.Web/wwwroot/css/app.css — 2/2

<!-- vocabulary-file: {"id":218,"oldPath":"src/EventBooking.Web/wwwroot/css/app.css","newPath":"src/EventBooking.Web/wwwroot/css/app.css","beforeSha":"4f6b728919404d0294a19cea3227b24930789630d249ccda16617f25a26bfad6","afterSha":"0c04e877642811b234a57684ad0a7f3967fe67136da1008a08345cb9d976f957","side":"before","part":2,"parts":2} -->

`````text
    padding: 56px 28px 60px;
    text-align: center;
}

.landing-intro {
    align-items: center;
    display: flex;
    flex-direction: column;
    gap: 10px;
}

.landing h1 {
    font-size: 1.75rem;
    line-height: 1.2;
    margin: 0;
}

.landing p {
    color: var(--sub);
    font-size: 0.9375rem;
    line-height: 1.55;
    margin: 0;
    max-width: 52ch;
}

.landing-links {
    display: grid;
    gap: 12px;
    grid-template-columns: repeat(auto-fit, minmax(258px, 1fr));
    margin: 32px auto 0;
    text-align: left;
}

.link-card {
    align-items: center;
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    box-shadow: var(--shadow-sm);
    color: var(--ink-strong);
    display: flex;
    font-size: 0.9375rem;
    font-weight: 600;
    gap: 12px;
    min-height: 72px;
    padding: 16px 18px;
    text-decoration: none;
    transition: border-color 120ms ease, box-shadow 120ms ease, transform 120ms ease;
}

.link-card::before {
    align-self: stretch;
    background: linear-gradient(180deg, var(--ba-speedmarque-red), var(--ba-chatham-blue));
    border-radius: 999px;
    content: "";
    flex: 0 0 3px;
}

.link-card:hover {
    border-color: var(--accent);
    box-shadow: var(--shadow);
    color: var(--ink-strong);
    transform: translateY(-2px);
}

.link-card > span:not(.arrow) {
    flex: 1 1 auto;
}

.arrow {
    color: var(--accent);
    font-size: 1.125rem;
    font-weight: 700;
    margin-left: auto;
}

.landing-sub {
    color: var(--sub);
    display: block;
    font-size: 0.75rem;
    font-weight: 400;
    margin-top: 3px;
}

.landing-hint {
    background: var(--surface);
    border: 1px solid var(--line);
    border-radius: var(--radius);
    color: var(--sub);
    font-size: 0.8125rem !important;
    margin: 28px auto 0 !important;
    max-width: 520px !important;
    padding: 18px 20px;
}

.landing-signed-out {
    min-height: calc(100vh - 220px);
    padding-top: 76px;
}

.access-summary {
    color: var(--sub);
    font-size: 0.8125rem;
    margin-top: 14px;
}

.access-summary p {
    margin: 0 auto;
}

.sign-in-button {
    background: var(--accent);
    border: 1.5px solid var(--accent);
    border-radius: var(--radius-sm);
    box-shadow: var(--shadow-sm);
    color: #fff;
    display: inline-block;
    font-size: 0.875rem;
    font-weight: 600;
    margin-top: 16px;
    padding: 11px 22px;
    text-decoration: none;
}

.sign-in-button:hover {
    background: var(--accent-hover);
    border-color: var(--accent-hover);
    color: #fff;
}

.topbar .sign-in-button {
    background: #fff;
    border-color: #fff;
    box-shadow: none;
    color: var(--accent);
    font-size: 0.8125rem;
    margin-top: 0;
    padding: 8px 16px;
}

.topbar .sign-in-button:hover {
    background: var(--accent-soft);
    border-color: var(--accent-soft);
    color: var(--accent-hover);
}

/* ---------- Utilities ---------- */

.muted {
    color: var(--sub);
}

.error {
    color: var(--error-ink);
    font-weight: 600;
}

.warning {
    color: var(--warning-ink);
    font-weight: 600;
}

.saved {
    color: var(--success-ink);
    font-weight: 600;
}

.visually-hidden {
    border: 0;
    clip: rect(0 0 0 0);
    clip-path: inset(50%);
    height: 1px;
    margin: -1px;
    overflow: hidden;
    padding: 0;
    position: absolute;
    white-space: nowrap;
    width: 1px;
}

/* ---------- Startup and error chrome ---------- */

#app {
    align-items: center;
    display: flex;
    justify-content: center;
    min-height: 100vh;
}

.loading-splash {
    align-items: center;
    display: flex;
    flex-direction: column;
    gap: 16px;
    text-align: center;
}

.loading-splash .brand-mark {
    height: 40px;
    width: auto;
}

.loading-splash-title {
    color: var(--ink-strong);
    font-size: 1.0625rem;
    font-weight: 700;
}

.loading-splash-title span {
    color: var(--sub);
    display: block;
    font-size: 0.6875rem;
    font-weight: 600;
    letter-spacing: 0.14em;
    text-transform: uppercase;
}

.loading-progress {
    display: block;
    height: 5.5rem;
    margin: 0 auto;
    position: relative;
    width: 5.5rem;
}

.loading-progress circle {
    fill: none;
    stroke: var(--line);
    stroke-width: 0.45rem;
    transform: rotate(-90deg);
    transform-origin: 50% 50%;
}

.loading-progress circle:last-child {
    stroke: var(--accent);
    stroke-dasharray: calc(3.141592653589793 * var(--blazor-load-percentage, 0%) * 0.8) 500%;
    transition: stroke-dasharray 80ms linear;
}

.loading-progress-text {
    color: var(--sub);
    font-size: 0.8125rem;
    font-weight: 600;
    letter-spacing: 0.04em;
}

.loading-progress-text::after {
    content: var(--blazor-load-percentage-text, "Loading");
}

#blazor-error-ui {
    background: var(--warning-bg);
    border-top: 3px solid var(--warning-line);
    bottom: 0;
    box-shadow: 0 -2px 10px rgb(0 27 68 / 18%);
    color: var(--warning-ink);
    display: none;
    font-size: 0.8125rem;
    left: 0;
    padding: 0.7rem 1.25rem 0.8rem;
    position: fixed;
    width: 100%;
    z-index: 1000;
}

#blazor-error-ui .reload {
    color: var(--warning-ink);
    font-weight: 600;
}

#blazor-error-ui .dismiss {
    cursor: pointer;
    position: absolute;
    right: 0.75rem;
    top: 0.5rem;
}

/* ---------- Responsive ---------- */

@media (max-width: 760px) {
    .topbar {
        gap: 12px;
        padding: 10px 18px;
    }

    .staff-nav {
        flex-wrap: nowrap;
        overflow-x: auto;
        padding: 0 18px;
    }

    .brand-name {
        font-size: 0.625rem;
        letter-spacing: 0.1em;
    }

    .page {
        gap: 16px;
        padding: 20px 16px 44px;
    }

    .page-header {
        align-items: stretch;
        flex-direction: column;
    }

    .app-footer {
        padding: 16px 18px 22px;
    }

    .landing {
        padding: 44px 18px 42px;
    }

    .landing-signed-out {
        padding-top: 56px;
    }

    .landing-links {
        grid-template-columns: 1fr;
    }

    /* Tables become stacked cards; each cell names its column through data-label. */
    .table-wrap {
        padding: 6px 16px;
    }

    table {
        min-width: 0;
    }

    thead {
        clip: rect(0 0 0 0);
        clip-path: inset(50%);
        height: 1px;
        overflow: hidden;
        position: absolute;
        white-space: nowrap;
        width: 1px;
    }

    tbody tr {
        border-bottom: 1px solid var(--line);
        display: block;
        padding: 8px 0;
    }

    tbody tr:last-child {
        border-bottom: 0;
    }

    tbody tr:hover td {
        background: none;
    }

    td {
        border: 0;
        display: flex;
        gap: 14px;
        justify-content: space-between;
        padding: 6px 4px;
        text-align: right;
    }

    td::before {
        color: var(--sub);
        content: attr(data-label);
        flex: 0 0 auto;
        font-size: 0.6875rem;
        font-weight: 700;
        letter-spacing: 0.06em;
        text-align: left;
        text-transform: uppercase;
    }

    td:not([data-label])::before {
        content: none;
    }

    .chip-row,
    .row-actions {
        justify-content: flex-end;
    }

    .actions-column {
        min-width: 0;
    }
}

@media (prefers-reduced-motion: reduce) {
    *,
    *::before,
    *::after {
        animation-duration: 0.001ms !important;
        animation-iteration-count: 1 !important;
        transition-duration: 0.001ms !important;
    }
}
`````
