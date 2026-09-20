# 00a — Port source 38 (Task 1)

[← Overview](README.md) · [Ontology](../ontology.md)

Infrastructure, domain, application, API and web baseline source, continued in numbered order. These are complete file contents, not an instruction to retrieve the predecessor. Task 1 temporarily retains predecessor names with the user's approval; Task 2 removes them. Binary browser assets are losslessly base64-encoded.

## src/EventBooking.Web/wwwroot/css/app.css — 2/2

<!-- port-file: {"path":"src/EventBooking.Web/wwwroot/css/app.css","encoding":"utf8","sha256":"4f6b728919404d0294a19cea3227b24930789630d249ccda16617f25a26bfad6","parts":2,"part":2} -->

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

## src/EventBooking.Web/wwwroot/css/fonts.css — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/wwwroot/css/fonts.css","encoding":"utf8","sha256":"3ad788ab7a86fe4890508cec8214b8339cd7a1c13b92e79d3e02370bc5dfd411","parts":1,"part":1} -->

`````text
/* Self-hosted from BAgel's own font source (britishairways.design), matching its exact
   weight registrations — e.g. the "bold" cut of each family is registered at 600, not 700. */

@font-face {
    font-family: "Mylius Modern";
    src: url("../fonts/mylius-Modern-extlig.woff2") format("woff2");
    font-weight: 200;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: "Mylius Modern";
    src: url("../fonts/mylius-Modern-lt.woff2") format("woff2");
    font-weight: 300;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: "Mylius Modern";
    src: url("../fonts/mylius-Modern-reg.woff2") format("woff2");
    font-weight: 400;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: "Mylius Modern";
    src: url("../fonts/mylius-Modern-bd.woff2") format("woff2");
    font-weight: 600;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: "Open Sans";
    src: url("../fonts/open-sans/open-sans-v15-latin-300.woff2") format("woff2");
    font-weight: 300;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: "Open Sans";
    src: url("../fonts/open-sans/open-sans-v15-latin-regular.woff2") format("woff2");
    font-weight: 400;
    font-style: normal;
    font-display: swap;
}

@font-face {
    font-family: "Open Sans";
    src: url("../fonts/open-sans/open-sans-v15-latin-700.woff2") format("woff2");
    font-weight: 600;
    font-style: normal;
    font-display: swap;
}
`````

## src/EventBooking.Web/wwwroot/favicon.png — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/wwwroot/favicon.png","encoding":"base64","sha256":"e265ac0f2dda1e5dfa65b1adf330722bb3ef7789115283604d8cd19f098f1f08","parts":1,"part":1} -->

`````text
iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAYAAABzenr0AAAEQ0lEQVR4XsVWTWxMURQ+XgaqagiFRkwjQcRCoxIrdKkJDFY0
RRdUbEotREOMaFiAsWtZgEAkpEGiK6YsqZBIRRFGiqCio+0U8Jzv5n7JzctkzDTwJSd59+fdc853fu4d4vu+/E+E5B9hYGCg
VAcnVXpU4gUFBQnMe39JWVQlprLFmY6rLFJZrnKTawjBn1a+XgcnnKkalTEqRzBo2vNeNu0uxmdKpdT7kxSrtFD53UQ/l2BQ
DINLxz7K7Wu98qh9AMOwSjSUR/wgFZIZZZZaSff+lNOHP0j3628yr6JQLO2S7Pwql49/FKDtWp/MKi+QbAZAKWjbYj2ISA64
pd7By+4332XlhrECUHlj7WshwAANz2gAEsTSFqZXyc4v5qB+/SZcBTg03cc1kYVLiwRoPZ+CUe6aMdAiEgp6bbN1HS29fv6T
tLf1Sz6YNXeE+bfRshEEnRpZ5LEKhAYkEDMsIltdxSNHeRKZMcz1ml4NCvvOTjHnhRzlcSrfq/F6+eSrAOWLCqVy9WgmDcFY
Gnof3fss+YLOeFY5srvOVQ6Ptx6YKPUHJ8r4kqFy5tAHJBLFjIGdzSXYh/2Db8W2RWq8U1DOg2GlUdR6ISVBwGvMgyE0Fuxv
qOrKOUcs2jzrfeS9JgvrtLp+HJTDU6MkG5AnYK14csj8lwPccN73bJ3Lrau9AozXgxavDjO2nDM0n70zjSLHb5TK1OkmjmAN
+/Gf2fsbsEEBCY/dja2zUpUjF1C/VL4fGTtzuFFCuaue71LaqRBMgUX8/xv6mYApvRFb8HeEXpCeDqeprNo4Vvr1u2FNF+d4
kCxcUmSoZ623qxM4PBvAkpt3HpuCWx6ocQJKEJ4cah5dEg5k8570g944DTDU/W2gTGtjEzjEg+QFDSBtQmOKS0ICMDfQ14N1
Tk/RIwj8RzaDqNVSRbgUD1R5jPOeayG7Gw/nHV6oa6hz1DxoxGHIDYRqrZZetRWEC70kk3JSz6ojJJ1OJ1T8xs3P/RWzH/oN
1U99jA9vT2JspC7a6SeffcK8ke63fWa9an6H33rxLef8M/FX2E9x1yE9KmW4e1wZopO4eo/gLm/WCwjY2TRZwzJcdlR1McNZ
kmCKFZMV6BGbNOYsOZS7Un9fCCcELcx2KOC7Dag/OAkHufd4VuU0EpTvPzeFMQd6qDwIz2bjKRX0dCpie0WzwesGB2dVukAd
QLc8emWqzNNcwR2CZmUxxrw1guCr2C7CkDBDwcSs3maSixWCt17GauAbAUmIhsT/LFag60kQrAJd7LEtOQVloBBA84Exdcte
Go+Sj78I4VZJk92jtyH2oGKgnLGvofJMCL6IorZFhuFtc+wdL6Sckq5yTdj1OqkSZexzMYBGlFkj5pB2UNrR/lmbzA8h2IRK
NcvLKwqZcMQelbhlVvIxIPgyhkQkNyRhOIRtFhisAQSfa5QgoAgUJ4JU52XA/8QvRgye1rf4g4QAAAAASUVORK5CYII=
`````

## src/EventBooking.Web/wwwroot/fonts/mylius-Modern-bd.woff2 — 1/1

<!-- port-file: {"path":"src/EventBooking.Web/wwwroot/fonts/mylius-Modern-bd.woff2","encoding":"base64","sha256":"eb89e6fe848b107c0553a4862e178a41467674d2299772b5020b19f7d32e7fbe","parts":1,"part":1} -->

`````text
d09GMgABAAAAAINUABIAAAABNPgAAILrAAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAP0ZGVE0cGh4bgYNmHFAGVgCIDgiDXAmS
UBEQCoOHAILSTAuHYgABNgIkA49ABCAFhj8HmHMMhF5bpRVxB3G3HYcuAd0GEZCY+XRR2oLp5oIHusMbXEhV3qIRMTgPju9Q
T3z2////n78shmx3T3X/AIxtbjMtVTOSIbkshzicnMXZS66tolZ0BYlan4KLUAIaOWN2t1zJi+xVqUHCaaNq7icchrspmZIp
4Wy4YymUc27292VqJ7r+RDaG+YthVcgSZIkLBY2E290oD6tsvZ+51gxxtQNhDKfKmYbZj2laePoKfM3G+fmL4JyP69mI9Ww9
GyruaTKG7Up8QtYLwhAeF8Fppz84nN0TXub6efXG0dhZ3J2WRnJTxpXELrOlNZqx/CAoaC5vCvyR4JDn456h2tGgxvp/TNmY
cDM7xp4RZlBxCSLHsAIzzU65JO4Jbyp9q8ikEDnJK00c4aiB/qBti54wW6e0l6jQ5Elu/6frA7Yo9dtxea/3+lpm1PSbucqU
rY9oNF6fYk9eBsYugxLRxooTLzzR2v/nOVXVPfP5vr9ADHo5iAqFzEbIbIRhFxW1edPDg93+mR1dXO7ccdY5zlqHwzkcR2cd
GWfGmeOMMSo7IxkrO3brq6QiCRXaKmO2jGv/9L85yy8nA+wkwxqQRpMteBHcNqQseCE079mL8tT7AgViLV5OPX8XPXsq8xkA
bzboJmBgYVm8QULOZc7/1rra1l857Wu9liy1sLulFoEhzsRJ7FhDHoqX8bReADjtOvqUT1lSZvIH6HBeoPPA6Xv5OnNectUe
LvO/a1mgOFRSZVVWLOsqXJJfoEOS++L8MSvkZHCBMjlQnP2wAFTFhXPyIvQVlEggxtvz/N/6mka49RYcd+6e17ppoGUjJaz8
LZUXbCCmO4546tRjS1W327B8a68m6amt9NfxeH6uNUAMaBA5WNvwpMNG+ZX/zWX1v77ElgV2uVyQSjWhB+iWfvcF4mRSvdQ8
UfcsodKLfDjP3k8ZnBfapMP09yx3Y3L4rlezydDnB5wM4f+/pdu/syl1XhLmUWrRVzgOQjiEK1WCkAgXy5b/+XRw8Wdne+DX
wwMvwoXnkEdGp6Wnxuu0oPHKtPHgxuu0pHR6l0YHyyENcmoHtMPvz+h2JV4JDO1CG+gvDuCbWQABk3MWxNI1QttHB6wAKGsG
xmEA+L1LbdJQnUnb8viVNvb/14R0iCgG2AQZgnvhArKYru5b5sCw/T+dVb+qpCpRqUAqWbLslrt7uKeBFqi9xxlG5GN7rCMK
l/z+uwmPyjqijKONl7MDTLIOwnuLwpQ13wx5qasGfHLkfVo6jqlCVSLg/5ZW/v8r5K7qqpbUknoUZmZDlO6Y80d+Zs4w5llA
LiSU2T2jFWBjQ6NMU0S3gFygRqspLrXFOooD5W8HAEPbcjEsqSB2itqBv3sIYWYcDEowgf1v/9ssk4qi7rrrIkwgwETql5+A
gYXD+L9u/ZcEmSG5F4ZH0e42/Z6tzALhwXvOKN+EzG2++bZiRS1aqzKw7kH7//eq5vY9AiyQXChpktBKo2bClCqlrUEqhall
39r6v/ve++X9D+Djf5BGEWXwU5JF0FJY1AjqePABygeA5BOS1pxjabqmtEaAlg3S9hkRVCPtPqU12WlTqpQypbTFerbZZbPK
Ztn6YpXlJstttlnsckI8/d7epNMrPQX4awHRZTbTACZ/J0jfXL0v6ejWbMhP45Tu5imjcR6lDJ2KL4Cms3ZmW0Y2QwugOTU8
j6dp3k3VrLZ0CGvSCU/A327FAmNDHf7XNKX77+2frE5pitOaUEoD3JPhhkHwtNq9s8p3aQ2nMteOUhoOC4AmNBCFADpVP/O6
HzlqHWuYRFuLSuFt9qR9mrJLYXMIIohgjBnM8I8152Ofb6TrSd8rzVmMY4wwRgxCOL7Sl/uM7fr4zfv7eM5ZJ0lGRkbLmtUE
kiDrYIt2LCjotfa6Yb/1XmQRddIC6vJ7rXuXTLU4xsRZ7mUNbxzYQmi99FrQLsPp+fxiWvFBDS8JbGcBpvjmlzzkxfI//y39
qOfrw9MXqtpm4Z212L6lbL/BBoqNLHQjuBFvOCkXWzkA9AOJUaFA5pEovxCg8aRXTDgw6JP/a9n1N0lDHopSAGIMc9iD1Aw0
/E9Xt+gw+H5BcgjXSInyqImGaIvuwlR7E20NbWRCTpQ5fEPORr6umU11W3iH3tiWtOYO10NEIoPZHXZeD7hR96Lmgu3wavg2
PBIOondH5Y4G/ad4MTEW2RO1MgdxpMZEtyRbQ7W/Ob5cdEPOCew94EVK6GEe+ut3djBnTfGLyi8Btym5qqTYjZLP7jKWy+27
ML6XFFXqkdJwYMZyFbnr1S36tYP7G8XaUH5xhm2bKvheXA27zG0WxGC5QQ/7jh5qOMG7XGFEyR+8doojAY0dlBaVLZTrlB/F
UVFToRWmysN3ROV5LakKUfV/paleVxNXs1Ir7xmmtqeOVaeq09PLUA9X793Uufc30sBoyGhEmhoT7zAeeKA23hYfpU6v1Bj+
O/7/VxJSSmoiNZU0aVpKWgZaCd+T1WqNaFsQ5AhZhKYmSZjURO4m4ZN30OHWcdDl08X0Sd0DPbVOekMnaSpz68XpZej1EYWI
NGIM8SzxocneP4P4nMjSxyas79BFafpx3/RXLWaSeK6fUzbH6WM1af1I3CR9g7ifBoOaCobPfAUlByiwdH6HF5eeUQm1lVta
yUvLIJZNJive++8TKc0DXcZJbpKL8OoACd3so5+W7dEZj168je++gVcRAPfgjYDYyyUV3T+vdKWKqHFSr2ugNKImnbpCJpSg
onWmA2t+mG3DNZNx8hIu3Xr06jNQBsGQYSOHR53M6xbQoi1ZLhs6JRU1DchW2XbYsWvv8D6jM7NyNsUejrjg6eeRt/iKn7tS
/kHBhBAGiwQp0RzieHG6lUhZ7QooHZVohuX43eD069Z/jAwXlni1ozCcUP6VCv1nlUyVp6pRjRXLr7iILwhESERMQmrQkGEj
GX1S9pGOnnFh3kowowhRMVzCdM4N9+qgDV4IiIWPBIRExCSkkX3IdetJL/oMRA8jc7Ha2PldEQJDDhUEaHBc2Wu5nKq0qkr1
OlxjUmvG5wRFSERM0qXeMHoves7I3GMYABVCs+VgGs1rbXp+krWFJcmxELtwwT7dZ2EGuAErw3kLqMMbARGp1KOG9xo5ctpr
0ls8EP48AhMSEZOQFplOrluPXn0GTJg0ZdqMWXPL82iBW7Rkuaz0UmvWywZSUlHTgJa3PLYt7Ni1d+WDu3pvMeiML6aYwxIr
3YbscMRlnnKe8/IVv+5Kv95LhShhBItAFtGtUtSwfMlu/P17f1lm5X5UyVWBGl+4llGdXLaV+DGrhHNuDjPaLao2kMALAXGZ
hOpVDb3RhpxQ+SYToKOYCCGEjBgoE2jSlGkzZs0tz6sWbBYtWS4bKiUVNQ1oectL22DHbttLE9+eGJAJlm4lbNPYwxGXeMp5
G2/zFYWQkYT7wBGBLKKvlZBZFctf+OZ/ltQFWuEREP+X5YoKm0pZlYdqXCX3LEcdUmyOy8ieRF669ejVZ6AM2gwZNvLNUVfJ
3Ef07aZxTUc+MFZryo5Z95FsHS00JWxYrbXgyqgibBXDXRxc/ffw8LoixPzfYm2Y+StnNs8HuAkVWQgxlrLPVs5VaqpQtVUN
qHVIXeqTBk3AAaiwkWVZlhf5/P4QQgghaksE4pxzzvnxyMYZpamoaUDZ6veTfxkuMpe5zAUIgOMgxhJKuAJHBCK6iL0aCCqE
ZzctRAKiWtt6ALl56uYhTtkUaIEHAbEGMBWGu2W+Pzk/N52lq/5CCSpaY5bIrBKLxWKxWCyccy6BpmmP29z2KyLWUpmJBR62
uKtpmmVe/LNKUBOaXB6WU0sO+xzb6b7jV0tXXZ9ZhWZYjr8RJHvTg3yBiLWEyIW2UQwH+abj4OhxySEcERHxEzRKSWifSRAR
ERspaU1JqfHr/7l6I8AI4xEsk2t5HScBEzwIiMskRb1Rw3KjN3S6pyvkPRShoi3TGSbBCls44T5rPOqSJEmSFun8YMYYY2xU
mVBMmjJtxqy55XmjBW9YtGS5bBgpqahpQMtbHtomduzam0h6u9BLDGFkYmZZtjI2IztHXOIp573By1f8zJXyj3uCCa2E74FL
BCKawz7HcrqIvxoaLpLULF8q20aXRM/GhU+ZMW8HSGKrqE1ukle/H+cc1jfL/I/XvdBamTW91XOd93R5Cm8wRmjNJ3N2dnZ2
dn4WV4ENpijKWH/squehEUrpoGw4D+ev5qwoZ0sPeO4H8g8JcpvhMTbSY/4Xr2sg58LzEdwlrcXy+5he3SXf7Wf6StLLeTZt
vxXgAcxnF6vI1eTtcB0EeCMgvlfmd7XcThXl0KpV6OS6CPkMJahoofNpDI8xGRZU8wRChBBCiBEAAKC2LKWUUkoVq9XaV6tC
iqIoisIYYwyQUEIJBQUURbFarTC6BIQfyj9cMKEz4VngiEBEc9jn2E5/EvO7uM4F50MI7tTjnCJZzfIl6mtt+pnUswcL/2ym
aAvDgBTeCIjfLOPSg/g5VznnRxo3UEUu3Xr06jNwB7TPVVVVOeecc8E5H/y4QjnlnKuqSinllNPq7HycyUXuySOevebjDL35
rcXPP/VhgGuYLVhJ7qPk1TLOADTBIyD2Mpva/jqJa84NGbViPmk9daZLIRNKUNHeoxsxXJbWb3QWNRKDwWAwGBhMJpPJZDJJ
kiRJ0iKdH+Lk5DSfrJKNLkpTUdOAlrfcsi3ZsWuv7Bvput7AQIxhYmbpVhPbs9jhiMs85bwTL1/xm1x5L8D9IIkkaTGtCiwS
pERHSdLIxTGbuDE4H0Jwpzx3kVLN8olnr8haGq0u/RPpN4Do2vVukhdohjcC4rpRvU3azWw2m83mUWXBYdGS5YkgzSPs5byD
l6/4za4IgSG/nBZDYHkpURArQc2R9/OtAa9gf7eLmCYYg6/W4LvNjvcX2GF0qXCjk5WLZ+ISrp5zl4hcgYw6SUNd17/yHhv7
UnAFL5+wiOgHKbE0JsXtZl/Q5b8qKanasjKU3dkAKyxV1Fc9FlUITRtBRn3tmTpBCCGEEI9qU0DTNE3T5sYwj8ksscHBxbPH
/Qkjsoge+B902XqIUuVyS9cCD6dO+v1ZfjXyAZp2tn2SEMkhZz65atIEzUwTt0zalGkzZs1N4mW/EW/XHh5DqBscTGYZeVgV
LVcbr8qVKlZOpAqOZvkPltqVJAS/on6Tzw+/fml/774A1mINi9lH9gzctva72/Hr+tem98wWf3vGv/N4LPL9d8mjs8ckgPYh
crtWs/kwCp+rgFkJcUP3jq6GFX2a3Q720WVCM2nKtBmz5jJ/y8Isi7JkuWzYKamoaUCTc5qm2e12OwDY7ffvg+NIEI9m2IlQ
AF5+Kms63YHnOyAQ8NRTTx/3qQJ4AKRRJ//N5r6no/lamN2OINrjjhBYpdFJJZuXbDsM1HHVraO2z6im9LWISQmdXkfprTQp
kV8eWsQy/NUuxb3lwoVLUhcuXPwuJLHePcA2OF+9bnmboEwx1ISlXyztbFKT6sCXXAoAAAAAoko84mtL4FgUwzAMw5RtIidv
x1hCY7rZ7Z1ek2iFpxPbI9pga5Zqld2FRpO11WEDAACANdKA0xWbHrKRj9p8qt2Jk3Voc38k1XA0aW6dSTVNM6Zqh8fnbSlV
KZhMpodP2oCh1r5ZtIBpNF+5zF0tcQhhFL+pBkh1ppEuolL2WJm3IC6qrjqY6XRnwPS2njBuw+NqJ3EvT5HEtod953RhWsmZ
TDxbd8Bc42iWU72MiYf/WdYrfyV/audUgkzk5L0dAkJRjXmqIbY4vPidUJQkSZVMJQesF0y59wo1pzZd0mv7nzXLvboowK+Q
Uehi8YXTo0IAipFUSaO06Bb9jXIN3x+YV+ejDv+ofq3+CH9GGEVedQds+5Y890PGC+wM4Yziflg1YIooNNGJ2cc2Jy7xKv7t
1XD//WouTrKSGkt5u9ZqRz2gV/30L/izvr8sOmRr13LytQ8cabtRjJoLTJALYWjRFZPN4fLi93PojVqUc95YKfmTsKQ8eyVr
dm06Xet5PfHSdMdc45by2+awQy2USOXY5NMAX8C/bKMf4wP3KvvxtVLnbeQUhVOrUUOhbirdUFVP11V9s2Km65qNKRd8AIAA
QLgUAwYOeDoM+us31vQdWcMXP5BXlRLjoKfGeZA7mpp+zjagb4DG7odDtvYQBuc1P1ddnZdD1VBDYCAwZGywS+2svUeKvl5Q
0LpaM4Zfjs28vJHTJr6aD9Ivuq7rI61qth7FJiFB1g2/wc6Zvcmt4XXKqzXqLmhzhzLR+WbnkvlmrKGuD1M+9bGH1/098OX0
cGtyRC07TZ9Xm73vbYUldW1nqHlaJ9QTxAeHetQGDN7RGfdrPaYfB/KZjEmC7NlNub3fM3vW2xzeZFDPsLpGNh5Vt+A4Oy42
UTNbo30qJrA5D/N2HlMOusiAvPBcPlyQaRamiIeIROEAWEiX7Wg0XWNd8ZhmU1iOVu0++e+yNL7D0k0hIlyA/fW61EDEw9Bz
oyeUXxmMQqOPKLysgVYH03qneQ5PMrot9DYBg4LWgthJaxWKe3QpBg6GdIIJf9rrXznLmxfvr5I3XSFosmsaYjPyfhIoUNB7
DxMRJbYSr5KQhzNCT3OZSpbkKvlZClJM6W42FVSXF6g60chy0lJpUx0gXenpZ3AzzGhWIHkrM2XnvruMvDbayM7eoX5EtX2l
BwGDGko/7W90YugOBCcIMERJoJSauuZiP3E8YfiBcuqgp2rkldLgQqcI5kt6e1BOsemkFgWOtCQ5xNEKiil1yirfrHJqbESr
t006CbCevmHGN9PMZNN2LmZzoH51o8ZuZIPAQs8b/vQbnBovXfiQC8t9JtGV2Jm4dxNDks4js5ImMskiJ6+gmNI9s0lFqa7J
BR3J8pmWe1vREZAe+gaGfWQ0ntnKLLs7Jmtqg132y2Fx+n63ylQXkEqz01V7kz+veyBVR0eGaP1SWVfGyHjlWBlWdu95qVoj
XrPDx1wgbMLPZxqnfUXDmvhNSHUcIrmgTqCrBFYgB2aD6pm5Tyb1akobJ2w65aoYjY0drUyxJY49zlu8PRiywdTpjRueVcJJ
fdWP2V++/RFge33za4LFe2QyHoDfMKMCZxXuvx2B/CZR15OmK0nfRpYv1Rp80wzfhg76oZoDrqLx4Fp0T2/A/sa83zxmtNL5
/M4kvU94eZIJoUgiMq4LKCGDEKIQojCm0Fl0DOjpTykmRCEKJYQw8p70msXc5IhHs+iqxuRioxxx9rxgyA3MHPFrYbd7i1wV
KFDQNpFKdNiTNqVSnVZXx6VdxjazvtdTV+g28dmEzLFsTbJzn3f1J9o7eKqapFdfud13FrjzNtpmN9Y0CCHEWPYdXDvS5Rnz
KZYQQoix3Hrx5t3H4uvyxwRN456irEe8yTmtp7OezHoqc6V6Eus5XDy/Kn2UGOh6dRKo9Oeo9y75Xu29R1i8vwW4XfDKrYIn
RG2qWkmqM1/05Vp9V79fvUm8j43+e7vUFer8q7PXmb543S+aNtK0TXu1qPbbTVbn0AEAABrWWKdN3l/4VoveOOI+3lYQaNtm
8ujA/XuhtsNgRXVVw32hV6NbB+za7YN3QV/aO849fBMQEhGTkNIzLsxf8M87R29q/z5hy4PTbbuEFNGBjBFGmPmxbdqd3IpL
CCGjbVBSUdOAbNXFp9tth3un7jXbdyIIIWQsvvIL+s0f/rpyM3XCSkSiezdQ97g5BC6U5cvJ/F+Z73oKDd+f3H3+ueDWClzx
8Jfq4S77T1R7NI7uiVsGNoT6P6ZllhAQUGKAAqwhACfYEUG4we6ggYADUYAHHI0qMDQgIIAuBghBwQxxqFgiCQ0aOGyxRwZH
nFDABQ+UyaIIPOWUY8gFajCillpMaeQKZgxzHUduchM37jCLO094SgiLvCGMVdaJZZNNEthmm0R22SWJz3wmmX2PwZvCRFKh
kMLUilOGOuVVoEFlNWhSU03a1ZZAh3oS6ZIkhR7NNGdYS+25pqN0xnXWlwn9DWfeKON4aYLpLJtlFhvmmcemBRazZZnlfPGC
VexZYyOHttjCH9tt53877eOvgw7JSZ7xqtwk8ro85IJj8pIXnPAEecX7wpx0ypPO+ES4iy4qDBuAjeBKaD/gwSPtUanXuTCc
MaXleDLG4AK02oZXxg9m3ySw+EeCc7w3aax+3SQQ+RzxVdfQbAAGzLfs3BMLhbFZOXgZ4CewQG71PZWNEZwr3eG/LzjZvgfw
MkZwgZNJ8xjJKKKEUiqqLkEDqVrrpp8hRptmeQjHVEzHcubl2TyfzTlX0OShci/PKttXEIPGiGOwGBmMGkYP01shJbGSMlh2
bMduw27HCmLRWHGsIpaKZWD9cI/+j/b6n/j3D9iXJqFkXHGxKqiitrqStdJWD4MNN8HyEIrJmI6XmZMFeT6Ls7XYksPJanlU
6b5wjBBGFIPJsi1dvFYvXOuL/J8Kx9/HxLhtxxbIhjVLFs2bMWXCiCF9RFDLjYv9YrreqEG9OpcErQeuB6z7r2vjdxjGEAbR
h8uoh9KHB99D2+j6OuALeALugDNgC5gDW2vDmr/mTJiTH9lm5xNsYBXv7P1tZGuhWxsXJtoeg/kDkXrAPHPIKqjXWGc8wUOf
1EIqbSzbcT0/CKM4SbO8KKu6qaZi/WBmP83Luu3Hed3P+6lVOV4Qpf/8K6oGIMKE6oZp2Y7r+UEYxUma5eyOj4MQwkRS+Vyh
VCxX67VGs93qgN6gPxyP5rPFEnk+JQpf/AhygQSaiSTE18QR7BYF1LpOtNvuEEC8b33vO93cIJQUKXRyk0x8CHdNlJsuE0aq
i3iTRS7VfOeHD512znlnfMwNd7krDOBLn/vCz37xGYnO+sQlcjhPNkWcpYRSyiimkioAX1BHPZdYEa0kTMUUV4IksWIU6TCO
ARoLqb17taJwCGOP5XZw6/rXYbl1fKITmMe4ftCY7j3skBjpo88PVEFTS4LHUM5OqZfGuRMVQtLKKh3iTgFtdxODTA0BfpLp
v+WAPTZ5wyy7LPNkj3M6FIFfsrpP7fvENjGCQFMVWeC5v9+3tqa6ao122qokok2oqigrKSrIy8nK4KSlsJI/0UT+sWbyDzeT
mzj/KcMmtNmAY9GVHZwqKhwUJWEyjAd/xxDSlIsU27TOW0NwMmKXhZBxcLTihpDb7mG3NRp4EBNZ809+c+AByiQms81li31+
gBfXXC7MzyhK89JNXLHJ+T5M2Ubbe0xAilQZNZgVeIkEFir4sO2O4i9pFVrNI7W2D87dkiqXhPduBSB+YVjNQaLNME4VSwij
IQzWsRnDif6uaRUbb1tIWZMKeqPAOsTRPzGDOHYAIc0faC7qc+YnZdAR8kDaOFUMlDuN3wdRznTd/SU3tWmmEpd28A3DDQf6
i6J4sn/jgW4I+wdIDLRH4qC4PiSDl5NT0hiUO9so9PpBC6usQm57O5g7pcZeUeKca792Z1H8wr4Gd+SzJ8Ml+DrsLmLPPFkb
cz7CKRbef0FnZMKysgYDEfhHXUz4pyVDSIzJVgNnnMrhLVEo0ataDCXTHXjOZHKnaQYyTYE0g8fdICdonVQhYqJak/BwV3Qf
4y9QrMMAiSkLU0yZoTu/pgktJ9vsNqr1Y2qfU9VFCL5XvSo5uWY1i9ooZEuVprjsFPwRWuczGon23sAzO+VO+QxfXO7EXad/
OvYbuS7qvx8ar8A3lIrbMR6ojrxa55Em+x6mXz+pJz4NjjUpOxClLVBJQwxh6eF2h13ZEvJt5IF2MU4gsmzQjePS6se1lvJF
/aDgMXnJYYbqUBymWFsEjWwRc42DDQ33q4zW5/yS98igAPQkkRKeRsz66Wiyj6m1gmlhyPQOlslfCasv2CFOzZUlariJhAMu
+2WS/uId1VfCZPI6rtf9YiEArqsLpxoBwdYuohrJG6JcJ3GUlAtE7+J3+5VFWWndRVlZm41+FN9/slOZQv9qcFBfLxUr9BoJ
mRfiXfazpz00VYvsXLOaxdrgcs3BTPWIb9aVWcsvKc53heDbKU956D2qwHgRuxX2FLE7qsvPbRrGosspK5ctTVH8O6tsH6ls
g5YSJlwZr3SX5iPUXTRoITETnu5o160jiRwsUOUZVQBwkDooPr2nohwseya+eMyWiJWhT0FuneWhoSZ1v100tlR3v0IbVYLb
22PSqcezO5YH6l6k2Y51wMvagwb4gu1G8f+2232NWleCGuelBk2M9JYwmY0LiwrcMZ9vW7oI9oHAYqEqui9Q22NmCiFNcu1z
URoh1byy3iTVZjqMmog24jhIhbWO5XksMbH9RAmcHia1slEWnXjpvxdByyxyHTZFaVDSeCotlto7/+BKJW1ly8UoQvnA2efh
cFUsdeLnHngbrUcwRCURqieHSdbydB8yVeLRWkBfRaP4c4IyT3V3AA/QkJZKUDt7nF1A0Y3vRy+ZrZ2qG/lS7nq2IKqC0tFw
HUTzSC6XxF49FJTbgQraDWJPhY07LlIB0zPnGMOytcoc4t2ZPbzH1cZ7oQ2VR/weeudniMkJCqEgj1fDHHm8bmexYgKwx7/i
PmUQo6gKXrgYlgKIeXI1BvTgpoatAIc4Nb2XNEpXOM5zvx6enA+01/PAdTawrIM0sAEvex0Ms3s5PUb0C4ZPSgBIMIMAm4D/
/pEAcA8A+QHga8CunyBw8MMAzBOADgFu+t13UoD96zcvaCCB+fECRyh72QVHIIucg+yv76s4+DNuUh+7pJhirlEbuHICrrWa
oMr/9yf8VuwfsjVvR0H23mbkaBvpeuN+WJAPx2M9r66zzGI+XeKuF0+4+y6aEEWYuAgqz9ymORYeS47qCaXIPYVngUPJozyk
NJR2lpnKRbGBF+xBBrVxQH4ulRfi5sFTJ+SKEXSPKsJBn40WSk/WtXLmM9M9Dpbm3clDD5R+eYm/qwFQ5BJvRhhvw6TWRoAg
dX3IaV2zrsxrJgkFdOc/LRX3zZ1xEJnCIMN7n/zBRi10M5gCJyuxtphkfTUOBIO4GsiWc9SbZVCS9oznE3F5e8kNeKskuZxM
RECODpKYbjB1NmWIKHLSq6pz4t7TYymczeg/zxL3B4lHl9/ykp4NLUrZ4rgTzF7gAAhQufFC+PuJ5fb2ThVBcphByA4SRZBM
JO5FdVRdryefuk+8fRioCtr0w8LbkTaswtPdfmDxwZcYmF2azg8R0W0YVTmxZ4e7+zFx9o3VXZrANar+ZkfOHMuV6OEI38os
frPNPvoesHyslnAmzaF5GkUeyQDteomUjAB9jfgcx1kRnNYPvuuQyC5FwmobubTXNYOSHnKJKJcFR2BLkwCtUnZ9XN0SS5Wr
ZiZznJRT1DmOoNOVZq2gWcM7UfzkR8QlnMTPvJKGIztNp5tEvaR25DW5oSm6j9QAbYKikLVTqs5lX0etK7Ua9bgPCzWfzU7h
WYUJqXTK5D3eg7r7Qe2nSduYaEKr70S/6azPqKUZ7aSzZFRTvxPTDHNDIKQDHB0lcLoht0+HPJqaD3JF6bqhZFNmbuLWXhcg
5cD1kiMOtDNVpEf8j0iOb8TK3g0TjyuyQrQqM4uv/AKbxJljYINq00otBTghJ+WDU9FPW6OJDVhD63CJdUhDOWMHg6fa6rmm
gnIC6daEJn4XWKrOUG3M7ieWVOdvI7NQIqIWK25VaeWAiwwgSiJBoGOBjHdFw5j6Wh4c7/qG3KYlJ3Z0gli0Ix5Tww8f4pZ6
t4g2dds0yz6KgEkwTluepX4N8j8s+8OvX0AOjAx4ba0KbnCjRXAj45ZDNoUarI3l9egbLFExXRHmQGiLnH4iqMKy6WaN4bh7
O2m4WEd567ltSfF1pylg9DfQbJP33XTWkRyI9IjiNBSR6DQ2XdgUECPyyjM1GOBIjgQecgC6kYc4lNg/p3TWRxFejxcvdPw0
nuA5GVqCQbP0gdR0S0VyOSQpLHseVLDUpfA44WPmaFTIOgibBmwKFBtY1mPC4SogQ4K/w5iA64LNSn/ED7EpxVIJR/ci2lCR
WaENoZ6EgGubmNEPAivAEiGKlnoHtjZgrCk7PSxmQKdwXuMJalzwllyJ9BqNTNIcDSliYJNIorSAi0weiVVU+h5kjScEE4yB
R8AYs18Y+jQ709bnQt6lBNSPjn+VlNkP6TYFaE/v42SnTtAkHWy80k5SDQAhfdUvYWDh6sEiKGxNbFM0zaQPA2lUOM+FT19D
eLDIVxppkFSgw1uQY8RbY3vVisvnA9eC6wEYR1gbM7X6ZTF4Z78cMI9D5LuTZ18I8N46EwfJMDxjApPbBDr+FKvwCRIPMnie
xANkagE/RTy8YG1kUBPl46ytMEmykRRpyjm0sl7CgmOnza+uQ+NNNDkqmRiLcYI/g6qsUgDjJN2LzQ/+6wcNUr3sQVmvT5x5
r8KXG5OO6N5yVIAXLxANpA8Xua24Wc+3E5hUUC9wXOIsJNo00cCJ7dQYDCZujsLH1qaOFH3pDk4a9Ci2CLyShy1qt9oDhKfb
F13qPsCi+QTXDvyoY2rKoDwRH+tsyd6D+0s/el2p9axbIG1ObInJ8mf6OlWq/K3FqHKkHmUCt5dcddi6As7hyCCLOJnUM1JH
ZZOKFEB5gV6BluhHR4KrWm0WrTGxBOgZjYNYkKYbPLQQIDLjIhCl0J18Y3E6Fvbe507Y4vEtVy/y4SubQvHy9TBTfdRQRRqE
8hH+6QGzhzZ0UQqXH3H7OK3PW9Q4C+f3gsyUiLXjqIqf4rD8+urUISEH+iHJXp/dC8ZX+AGxdw17JrO6zK/wdUi3DvkfZ6wl
ooCskFiRadLMo+tkBaVCEePsrvBgXFr5JILB3KeBe+WWUbdBr7iNx6Sj+T5n8xwEOWNbK5y/8rMq2QbIaskkB14rAQV67wXc
+OxF5Z/Ws8KKlhAt6oBqlVZlTHVrhvTIgRxM1p1NnNz26CLEz0bvHJO3682e5Gqu/Q5DIAPSkyqbTsx4mla0UiCOBtPdgk62
j3Sj7PB05ZqqkYLUzRrTgJQMX55HKrjo4zT70+tsmgNGaEeYJl+7UA/OyBX6LE58t2HQz6htNX8Y0OZBGoqwbmbVCmvkb0Q1
wq290knlAxf4Hjdpw3iu0dbmkF/9BIrhZtdDYFDJukdx5335t9P3ulpMAK2fPY9+RHjdnXMTC6AVPW48OwRE2BUzeOANud3o
N6hfUxgdFd0GEcDG4gjVmYaxiG5aKGSg4gd1JpA7M59xzIDeo0qVN+ayEMvbFwiTLEeSASIIkEG3WIoRRBqB9yfRjhmgOxWV
WHW4kIJU5HHcFuT9ijbX7U62sKBgVUQw7pAgkwWZjZB/hEUKD/LZ7jr8VLju/WTLm9WEFqQ69j5SLkurVTfJFuGBQABZs9pl
hfEpxkZuE6zrPqSUlhutTOlGYaS2pVH9X1D9qUzLUnlMjkoNqfXol14uWe3/LCoRfUVf1E8w9nPEPyCsA9zLl44Lk7w85HxD
ABDWtVHFbAK2UAM85e8WVBBWIKwSe2jRktAuG1y88XjIo+QMpKiX0pGxMqTJYk1gvaYSxesFpaPjWbQ32O8TDfB1D7ZDbxzZ
qE0vd+f/1h7FGZ0O0O8jvJz/wnFBrM3wZxt7213UIe8z4yYSrwmlItP5pvqSNq3Jbv1rQexcI5UHI0qVgUk+h4ui3f6i6OJH
wUFFI7AhHqfwE5sw9TQuM/gtvzbRAAD2pvmKD3IvFdl+8xmfKWo1GKU7WAMmshBoN1ZkHmyN2yc7prD1ihlArrrjyUOiHyqm
WM3J2va72zKmBbt1TBCZJmIfFEtGkwRZ1zpY2Dsl64PzfhgGkgHvGn7TcQlXmTYQX1IwBwBWIPxBPL9dYnoCuuMmTkuPk3um
OGB8ykvqyDM5P+fYIG2R7hm845YuSO7wX+ijDRnN5rZo55Qov/O5WAtwupQyBKdzFLANQqlwABzuSzOBK4JYeoE7/LrrNyXS
UIB20ae7ObCqozRc1SxF+Ad11WNEAyZwzyh3cd1spSCGfELxKzMOQtH3XKQDp74MUe2rLGEj4Z4MRIiiTTLoEayzQkN1WVTU
qsVceqqP8Bhb5criUBSAOKPId7W9zMGzT3o+7IW1wYwT8IlwSMpC0KZgsy0a+czsbjCR16LU6l0SWvuy1h+BrQW2FLoOakfT
UQfoIFKL0CXlZwbh8wxRVQgG11YZq+w7likqGdowoYm6epgn4CsooKnlYhiDK6xxBSy3QK7fiGbsT/IdgtfVcQdZKPZGwSIa
/ICTjzL7peE2bVU5cwJDKcZdstHJNcYKub49ML8VGBou7WO/XJjLGj1g5+SEn3fKKqgnOHZEu+A0XOSiItISPxK3xiF4M0hC
R3ZFXCCHiWjpxUoM+AVoqsmz0gsXAjWvFaFAJ7rPAQF0hu1DKy82AIOY0JLtvOTRA5ye66F9qx1nzzwcd3mFYhSgjwP0lRuD
Q2MuiyY4b315BJ7cX0AGi0RnLPXUz5qTkhWs7OkM5itw0HPtK2eylVsAHDuPMEyBSLPwu75W9Q263Wk9kwepJaaQKLDfElzG
7fNMQ9lfWh0abe77B3tXPiW54ugbUojOFuGwknma0qI0TCH0/vFoOwOJCIiutL4wKDg4BBBc4pk/9sVFpC4Y+cQQHAZGHIUK
sLPA998g2kef4ttqwwLTpgBfgfAnlsrbehjKwKeT/buAd7UvAxftueKtexJ9jSpsvDq7u1O7euTI6qcduZKUt85/XXL1JG47
+4cu9LZDrkY6ZGf64a6Cr/wIroROBO/SLU8YOU5euLsnNC5VA670wET76OG0fYZSHGupn6DsG9a34h/7JEWJfUPccaT16Byb
pAn9SG1M0gjNhJmpmoSt0ZJGPA5Gu4PTaUt9575VRGiOHwplbn5oTcvm7TpFHdt9PvTJWr6IK7bkXxHErboExdZf22d12azO
aw5PtTtwSAQs4rrit+4MfMSZ120z25B805Ejm5y3gM073WMyCa8Bh4hwQ/Tz4lX7l0cdFMgb/IRjYFCI/MO4eXsGyV3wDmWu
uZXXDdDBvQxWvZWMawGggWF77EmHsSFePOSrC3H4uymDYUr28qAYeXgaUc54yShrRaCxSwG2U8v+7XKHyvJurJZlzJtG/92d
rGRpTJP7vYRz6nkD+u/28Eb9uFoO0nGoXzx/HL6wDpAp01/PPP5Y55bt3KNfr3PdK2esamB66yZunn9iBVHnKKbCNshZlR0S
QrXxSNWLk+c+c+dFV8ujqVtCU/xT5Xxp8proTZRBba0BqbZWP9hQNM0uS4WmPFft5ISpQ1RtLpawNo714zWagoVh+6UxW8Xa
xWkPmBlbd0QeHCK2pz9KX3cOdp2IrF0e/e/Bu6Hm3IKlygAhM7RjOB3mtYY4FffGs9m6vzWigGp12tzU0pzIFK5wdyy+kpQa
Uh2UWO4oyGWNyfkjR8X/fHHl483N4+MdpKVfiWMeQ1dXHCkufxzo9Lxcr4N09byW0mpr4lAbvfmkIPplxupkVf/VPzrmH3aZ
uH+QlPtTQpoethOve6RrtGakf6SPV3woXa391Udb9LP26PoojEvJM9jzODy8YMZxMKWrNgglMxsJ+XbUP+mmlFTpxn8nPW1q
jnwN/ygtrVIUId94LCZ9+CdCoYlbNZH/3MIF6BlErhDF6MB+/1Zgx82h+9mbsfEFEe57i1kwnlu+39fQkEd9877OBvK6eEKw
ZsJk6hbu1w/hYZfjQs9z2CGHuSOpj0pelAw2haS7xFhl967rCB0M4Z/95Z6aeGVog82WPrxRIlBSUquUvfIqX6BTusNcrOKq
O8Ro5/tHRc59Ef8AJhXmpLZqRqVzH548ZfOVnYI87E7UaNlm29+Aiol+5RDwMx6kfaBUJ0pjD1vUiirvPVTu0v2S8Tz3f2p8
ZlpSgw65Ml7KLx4rieO8Y/N1VE1bSqvYNe5O1oLA1sedE29nhOBlCdbMvqjevcfLMzzzXZ86OX7SIpxMfJycHAJcyOZBnoYR
Dp7Z1fR3Ife8koLCo5MCdbSyLJI0WrbB/ga3EPaQ8/2LTKtQ89WomphSXvuSvpT3/6mErNTERn8DPZgvyZTBoHijwYN8IAYn
HOafXkn+H0Ye4cQZBpmsh+Fh8Sw/d1DRYyMSykKmJRn+U/QlV1bSXVdcYa4QBGjMdaaEHnv7oSdE+3n5JNZg+7Y0Qn69kpyX
NhcWFhFhm8Bhv8MZoVREcTWmhEW6F3qcDrbIPPVCwkVUUOrB+2/F8DA18vi3nHFCqQTuluQijtwFPXo3z6d8RWAqz0Y1+DA+
VwdLjkm68ej10Ye/moz9fkoI7DQiTklpXtwhvqrn+G9K2xmXyOmP3tGTb5lVL7BqkuDqdBeqp1JFTUHbs84op3PJDAw46M64
8sS4mKwgC3AWW0w+Hn0crz+setyTd1P3ncl9wAX3CYhPjs8/Qzc1xQf6u6a4NNQL3ZwrCQ8OqGQ+GaI5rujKnbxvzhZ89WJg
7t2hIM5JU2Hkbujvv8JqqG9IUVbDiq4Mjx+ro2Ozyidgu/xyx4nPFwlkXra/FKjmRVUq7EB0fw5wwWXww4pgVcQ+ZYhIuQxm
mDjBPWIDX+d1Rc4lw2w7ULOgrTnZZ+xGJLbdVBj5LBS2J6f6F4rAsudlihmvbf02do4EuWm2SQA0WMBn1kfArBsBGoxAd/0V
fPtvfqa8KNc9vhydFfUdIeEyDqz+DF7p4YaDBsNbLjV7FOXEKDGzAGhosQBbEAZE16okTC7UKDCLL5L2uxKHu8sWzqXgjytp
bm7Igdg6HQKvmbYZyq3/qn9g/4ib9/BoQIA8s5A4qw0+RY4jmMrajpDKETtSSZXHjxXoffet5ooyPVbgpJQH/UqNX0R0o1s7
nRDUJGV8xv+q/wZjv6ChYR+3Pu+RGg+/HHxsU3ifvbqPhYHac+s0dedI8C5TMJnbyUa6Rpv6HpigLr24HBtzI0idbBrs9Mu9
uToqKf1i6BWaT7n9kH/Q/MJCLoRX2H9PT4nc//B5nEyw/wb4UIZaQUMN8HBVlr4H4N7Xcji5EcIn9uBf2s7XtNxZivgPjnfN
ZeH8WK9wl1loEYdUNCI1IO4bS2iD5T6ZciPFQViQhYBr3jt5uJx+8nAFrjmVxBgHln3a/2kby+A5CV+p9dgarDH58wWnJRZ2
QfD9yffYYUGNAmIpUdrR6EBz/42GH+Alh80asewi9ztoQO3Tti2AiFlIk2CGKIVJG2GJuA7aq8f/12mbJm3IIcbP6of0w0QP
+TKlyGXp+ix9qCRr+gRcwVCihp2YbF9R3F5XwcwurVDBuOFpxnruLeNmGntNGmPPei+3QgX4X1nQ6FrRya4WQhckPMRiDnf+
sD+s/KOYtC4AOgYC7J4T6yOlD2rC0tLDLNGaRLAo/AgQr0GIi/LCxUUQBfEiCj/yFUpba+o7O2ormytleLmpwYyusRW5YwdQ
/F8vI55lKOz+DzyF5zSnYVdwXaS7rn4pd4n4VbiY+MktlMG7dx+5v9gct2sSWhQYv6r0L6l9e5ueHMfvZ6dRyRuj5OCIt/QI
PNg9IEKID9J2zJizo8N340PkZOOcFKVQfFK/xVZEmivTrLnek/Jj7F00g2JSbBg+jUNfp+9IGnHd6nb8ruLNH1IoQx5ZfzTt
6DDf2t/b2tbbr7DnrelCDdUOzEzBmxMcNciQ8B3ImPNHA+8En08Q4BiIYVpIEzkQDzNDGTrTCAZ0O7LJaVsjXTsraBwKgoPI
v5xrLHO05tj1qrsFF1LiJgozIcr+limbNi94jUCclgtYqfYvvHmqiXsNgGyPNV7HJ+TMAXTe+DY5wM+NBcVx2zGDegYtYoti
b6t6eB6ckGYHKA7qkB7SaMbHIUMh/RCT/3dkVH4fiqsc/RVX5MeHphV3XKqVb5SWkd9W3/D3sarQwkJSUNPMayEudowWn5Up
XwMUmAtKcz1BKL3Q394QWB6cHOiva0ju4x9yEnX/dCB4XMxfH3ZowVC+0VLSWpvWHu+oqbZGwyRfwlobfILgIB6xsfX4KQm1
NUKfXEq+OToz33DaI/BiWPl8z52ZSpMT3wbdOstclY30wjUeuEU1e0mXnC2VxGtELmuu+gm4TeaOjHx8MzGe9K159vfu749O
b2gElTV2h7TQRuOgJcgt510D58mON/XFiXDilkSkcVJszVzNxDMXcMgc3H93Pk587B3nyTeZwXWW5+xihmEf5bijoKP84PFG
ITM2y7+WqvVcmTcPDS3NbtpbPhfm8CtkpiZqVxi/pvynuH5B1TA0WnZ8my7Rariks8GQ8KHaWHZIhtsv+JsI5QgXUHXUvSgU
iqm6lw5FKF+42MLPdqJUsrHxBkU4B11sne3i7uKeZSychS60mLJMOA0B3geBKmlSKNEq9W5/oRxKmPMCEvo1T1qgzwon6VmZ
Tg6Nhi4SzqXcaGyQbLSd8LMQLhbKNyXQk9jRKPs95+P25xVGSTIupvNGOBlP9okVztn2pQq9NGUf6v9XpM6w35WCO5AoXh5G
GrHxjBdloMftYTZErKAEcdJ3UgjHaP1bIMnKxGULsKEtgxesYyjdYvi3mMKp0g3t9XZi2nDUZxN+Wt1WGLbUZ0Z2KEsUeViD
GTyX559tKt14eSvqGE2tyCTdlUOT6evDjEoPHO1J9I+J8guIiAyKuz4YlBkR5enJjE2MuHozJSwi1tsrMi44s+t2eOSqQvmd
+fOlU/NVF6bmiovGn7Y5VbIe5fHNRxZHzkUanzQOnAssDpznnMzTKLAptbFFDdfwDvACH+he0EV01ZnmthnX/3FjudUQy/Hg
zGs/Jt3eOoPeQxm3unC9INmCZmab5TN7uxF/4s9z08oAN0NTeU/r1y7PnRPao/PZMBUYotHpBJU6oqV8j7ymoVUKvsM0QYoO
IdEdSfq6BNHRUZ/k4JhHclxDAokEk0kSqlDUxUWq+eKCvBwtLJhTFxYFuUL36Z7fWfb2rO+e9P1vDMaf5ONjoZIlj76/n1z4
+2mqKBx74r9qnZgjdFJGbmyCIcbnTZ18jq2Jo5Odjo6RlVilkjBmBOfIVMFeDMLYRrq7ujZzRNNFYpGqOpJmbfoqcBUYmTAR
K4jrSyKe0V1Z5g4LWWGiEPIut+YZn9K9XAbDb+uq027Rvt/qY2YUZJHsnvtV4YNZxhpk2UjFUGNtC/1DfaiwNiq6sTE6uqEu
JrruUlR0fTPznLq5uaosU1elvrKg9bw7tXT/1zVYM9D/X1/rXMT7CANlfK/mNWV/SSVChJxpmGemEs/NTOk8+Q8BYX15MRoy
0u2h+olpI8UXy6OleW4WKMUmuGuSxSxTq1PD7HQdwkpVjl+Pl6kXnwq0V/rGQihAPyiIvZHuR9KEYpXzAnNI+HAtW1xgLUgt
2Fe47okelDyHb3658lpuT6JEDuazYAUizxcjUw8G7NKx324iPoGUCSgjVFBhwA/hSlM3Ubh/+OFjNPXF67pEWcFNVHplU05u
ZX16elVDTnZlc/pNgqE19ZShDSG+ugptYYxQgQ+/xAFYkud78XuoVhNSW5AlpI1jiefM4C+bZr/PdstvhVZ9WRqpGM7OE8c6
6g9TI1ZJ22NWc+N30/uQyJ7smNSERhnUCZy7Fks8Zkbjsln2+2zX/FZo5ZelkfLhXr8fFDL0naGQPTzcppExnSPk16vJeelo
+qtt4jkdTBPT9ZPArdDTOcE4ivpC3FmMiIQjkWh0vO2B6P58fNSbOKJOJascax4LfN9kSCA2ZzXbzcBWwt/AZ+KEJ0kfK8Sz
1nJK9uf17dlSkepyJ2t1ZJBwJFpSqOMPWJ3W7Yn4blZ2dNbWf6vna7Jwb8zYwG+BVDgSDoejiZLLrV3ScipyHV1KckriHT0q
CtIKrT1K4hS3DdjASxKCXMD+9UB7X+LiP7BgGjA86r1x1d8p2IysEqZ3FgWm3bbM7+8J169Gl+qNZQ5RSDgcBUfnTNGVCB9e
TvrSqdbve68H9IFLwTHhlz3a6eRMKfT05JPBNqrm2dOeQr/F5OyIYXh9Ve2kG7PDT00pzPKMjinGVD+2yMepokvCouHnQ6Hw
h6/SfPoRyosKepF14432qb0Lqjd/F+V35L9o7auttDFISHeJwPLay54+peta+4Cqtj2o17tGvMb25KpDYuRJz9U0pWCulBIN
c2ErK6JfeFQ4fO/EEGaw08zsFnga+z80jE1TbKz9m20q6DA8PVCNJWNL1wXj/251qD2tYePo8torquCWY7pXhX22uJHGNfuY
AuSF67jA1oB3KCXB5UHxcvcUttpyd5S5xuwKO4A9ajwH6xndSw+teFFPmcY4sip7w4Rfycva0msS9JXYn/311y9zsCWMnNbo
aFzNmtNII8rkxB8PVkBQYJUZdOddCWdgQNKtxY4ZG9rzaA/b8DNwk2XvzK8N6dzwz/7cllCTq+hopajlqWLLQxi24Pdk77Bw
D8nSQrgavLp5kR5VqCrCni8BikSxI9VlEFjO4yy7oK3WbM1dncOLm6LdEz9HnCdmQ7Mz5zNh9y6j3j5fJBwJ97ceCAfdWsLU
oKd3njdoFucM3hXkciEYOt7W4AdD1stNhlDns+NP4B2IPrZtMf/3yR4JhyOPC42wqgW/+gjCo+ss5EcGh3c34v1xicSGP8bl
ZVQgfkWI4uRklrGjbFTI8syKyRluIhBFeCEEB8Hw8cHhkiA/hwUWLiYUK+6I9pPSQaKqCYEewgk6Ci8bon5q8npSgHxzHHAB
WRO4qsnJPilsBW9GRC1kJCOMkBGJhZDMEGk4/DgfXGz1ivNTJROD6j4eJhreSX1iqLQMcXiBwOXUGLLr/IocYKe0AjDVcKIU
rKL3IWZ7lao3Bc925XcdYwlXtaKK33kLrhRdWXv3ifuLDdSuEb14eEN5WK5xQlJ6OK2o/OqG0tbk1ycACefjQyLh8G14gkH4
xP1jWV0zxeoDrNPFA534xHG/9VENze/oko8XkOJB+qGhV+1UeNJOBnVPwL9moZ8csYXNukgk33G4ThwqoWTDtOT4Az0dVN7c
Pnu0e4RmBGbQJHPK0vfA10cH6QwtQwcewjCNP+hcio1Hy6qxN7xGZ+3LeXyEb8aSDDtPi/CwbDUr6z6hi4L/fbqbjH6RWotU
srO0MhaZ25faOYjZFJ+auz4ZFygnvRqhZOdEovG5xrx7lbKTiWyRTKSGBZjmYhzi3Pj4FKSnlyNzaNjylsJjXQUcq5cs3amk
rMjFO/JmBKrkuKcuRyJJ87pwFJM5A07QzN3IzdWI7OpMNnJ5WtkVJmjW4l1Q6I0Kagq3KfQ6JeMos5xQxFkVPWMpngH9ZH5p
5cqfgugXnhZyFnLSUufw43D069MeahSslGQNFU8SPBBXgO4qjBmKKZzQQz43Q5bdQ5VBsfTx3k0IlT0kodBivih1qC/aFICh
gj/p2ENJz3HgiQGq748w//Rx1OEFFPxCbMhlaoMPI2sjHi/EEamirFTVqcrJpH6Uyyz4/nfEvu6qx6BetOns0VWi+z9F9gnx
oybQ0LMaWj1J4H0VUt+O2w6xb114P7zu+YxOvci+qQkdMu6RYsDPHX4qdFxgofLLpB7joWObL7uHr77tYKlo18jwMtvNeNbY
GX7LZYHlmYaaXm6qAPXKsfqaR+vr1frMibEA9cjl5Vr0W4jIw2CX0SsMxtVe52DxR7zCb2uXn4qvyHz9Kozb3hTBqZ5VGUu0
UXiVEdF5slT8mfVkC/+ZbQP/7KD0i8zzXWUxB5bDobLYrliXR0CESUOQT/2A7mwDJaMDJC/Bl199fj7s4dhnjY9Xz72XvAo5
yRWeKHcdJC9xBimlcPNm9qTceYBgf371DtPvPP1GB+RAGP/G11wAVPgeS/e4qe4LfeHt9+p77ha57XpDqf8klH8Y2Ufr9Rvc
A/FjHN769JXQ7bhOjU1D3lTNvao8UblvhXIl/qcUlP37mrCLtSPZur74qMFsNY1fCRK9Y1oTNtqmvhu9eMRdJy0PP1VPVIZ4
maFdQlDl3YdPCy3hhr9SpcvNaVpKkkbqlYrfzJtXWx5Arr17PCO3vUIwlOzk8aXiLa39oxOVcRCH9JBWs0BNWTrqr1RTSopE
/x5JD5MlSzfz5yKelD8JDoVJ4U+43vL8qv4ZH1v1E4Qh3A5AI2ijN5Pz9yaxZ2Isrl37FBoh9h0mWwWT5GjmaIYJHc7+Mmba
7Gv1hK40DZOr2qe2Pas+W/e2SPVR+NMb3ye+1nkJWytzq89xs3aY1wmV244MEgiIsD+HzRAv7yCaKFlgBXc5HU2cIkKItvLS
PiW+MyDn18HaX9/gwPzlEVURoJnxNaS7ntArq+hvbwm+EJPpcjZdk9gqn69QakYpDI0sV9mh5hVnVC2MjKWmipE4BvTPFvsq
u6gVynyidP14AJMXghfGiaibVBDJ6R2jBlePnuTiaF290E5wEO36E9lMiOI78c2h0ok8RGWhC7j86T7n055B+ZD8xc3V2Vl6
QV/MV9Fu7oTUgHw+xY8Sq3eu+5U1vuLjItJ58mROiZyYR6gfw34UUOGtKW7L5gywoOCuPBn9o4z9pfQLdm21a0cZuSW5VSUb
g4PQwqFZqU8PeA+wf6F/BasgVX+Rf49j18f/NCs5HBqjhglzyb07vih0pNjxh7t13Vi5yZIYnhYCDtPKpGWTsnGpch+aiken
qfGJUBTKAj8MCkNqs569tKgzMZaZ9V+5U4lA4PLN6YPkIV9Ju+frrV+fNxA28FB1pdnjjxXu5cL3uwVW4KzVhoSAYez4+ked
xrHsbqTOLqU+AFzor7wfZH9lzxUx31YkuUUMowerFK2ijf6b/G+uhBkdWRhJd45NrLme/ygQPVZ04fhROtsvAT5Rq+Py/DJL
gBXlndFZ2GVR19IUAU7+4QHXK9Tk1oSZOzD/BAlzyMOjmwWJ+945PfPq3A3JU8do3wL4bZm+tsqZUpp0n1OOrFo2fXWGO3+1
F6aYNeXxY+VKHl7yaMiI+w+Gb07enC+uvIg+rF5/OAWnTYvSSKbBjzuvboJk9IDFhoLT430RaPvGPfmPulEDQHOr4xo3nLuV
xkkp0tnZ2pEW1LJyeDk8PFvoREq3ZpfMMZXUT98BXDAvt5o095aaBMJtQnViqMvpk8ReQ/d4X7Upxkm4Sri5qEHzboIpMHGR
HmzfPvBvLKLdPBSTq5Bxj1QDflgqyQPGGzZefe3Iq64fPXaRoA6rUvyc0ANmGzrHRlkCBrZqwaGEsMxBi01ql1Ydcby99utw
R9s/lwXfdIXbgl/VvrBf2Oznw7ybwFNj3SQZ7oGiHDxU8OGeHu3AUsCYxhabwQSNKnqaMbomBX13NPufjAQ/GbmTTPUPuo0r
cJbASjdiP1fxHu/j2d8fQ9HIB6Zc4WM30gKvXgxmI/L+YB8RNbV2B83pCNVfyriZ9cZJWCKMmUtIcf3RSZVVLftiaW9llU5t
1KNMBKnH3Ihdxi2qJQIBw6Ndg11kLRB4FK2vJiwtqTqq2V0X2frCwW/sI1LxLxBT+lH9gZk0g92+89PD5ORMj97a+69cDK3s
Mr7h8nQcuSwlveB/a/QhCNzTfDFjnQiItahbNr39ZeOH98fy2jxMCARVEzVGS/7VBDqJSHDWTxlw0hjBwbeGSFCcJHwzy5JO
6RkqNDSo43uqIPNdQebp0IkKumqzJMXOFmw79Mn+PP76ZEwH/Xb4xVEzakoqTyo1xezi6O3woHW9VtPrg1/a5pfrTQS+jgVe
j3SnWptHmKT0jfUFMwim2tYqzhW3FWBzbbj+Fw/ySkocIAgcNPlcEhQHTTyXACXqC9peY90enbh+zsAoPWRTfwgOknwmyazr
5mqQ2eXPhFAclBRq8zHxNdPiWlKgR6gqokMGQJcwNL20tdhxK4U5BTkL9lUsKTxf2DtyfgRq7Fdi4k6QnElQFb02HHrLjsSq
Xb+trJE0p7/OJa+zesvXtZDa15Z8GG02zlfCj2sDdKAQYLWviP5nhSbvC5ExdO1j5hZ07ZhIbTa1H9zgpXpBxLuyCjIwHNTA
2eCEwSU+3VlofoNe6PjFa8sUqLGfka2LZbTSDFTb+wlwHQcxNsXgyXRy37RyAMK/s93FL+AKpw4/tnbTlp+SVtBb553uHN2J
dzIwsg5VNxA6MhY3ef3QU5OcheAgFptDMoziUviRpVSJlFgfnO9VmWMvzVfYlI8TKUSIi8D/wWUlLtIMfbVV65Kz8PfPfojK
/wOO0vfnyE8SL95/fO2sRGqL9N9zuamgxQl5WGLX0vCinq0OB21rkKOG6KXF/7N0uEW5gfFeaGmshTqR7KzjV66LMii9N1p9
7HrVREFlSuxE4Z/JkqmmM8cyz0HOyY/J/5wy2atQ+BzRE+FQmeudHxBfR1XSUGTprhVZGvtNRQTjIeeuPhBDJ0N7Y4dcMPKU
CzZNO90+un1yAsvpyvOWJnKkmD44MXdPjR0Xr6H3JuPRLMEWdmOyvLLqdB+DMzEX87q8wr+kkokbmoWODpFUyOW6Im9EoBkt
uhnj4I24PhOJ+PdO6xoxkfYAw0axG9JV2sM6bVGM1EH94CWurxN3SipFdw7iNzFxVdpIG868Jnp6OXyqjP8ZyrvKtP9pqji9
TIiq6/+uypvKmO1TZVcVo2qKXHTOvjVemD3+IOPc/fs55+7e/meLYuRgajHyqQuv8F8q+ycJvEn5215dDP4HlkhMYaxbsmtV
CuCB7+tT3bwZKsQ2qDSiHfYvcz4QqUX2n89k8mmMdSmfZLtO6DGwQ9rJzVkYEpCK/Ye19w7HdKois2KP9gSVjv4iFb/uIxX/
HiGV+OER6Wc76i6d70xNP9t5qeHsZfHi+em/7Xj8WEfQo4AQ7NOFoufl23XVT0kjx5/8eNKrq3abP30unV/mVtPpr40uS1fw
XgXq0owCrQan1VCudqOBTsch4uTPHb0nT3e0fz7pI3ZYjhh36qPbovP08qU/NLYo8pxsnVR4V1bWdqsefVk7BcJx4H/fu8RJ
+xd0NyXnc5wVg2mee1JPa2ZA1jRYkEtI6HBKJHtKhhe34IgBxCd3AffFp/LUGBeBjC4stS3LcHDLVRBgTV9vyrQK4lo5NXRb
OplrTJi+FisonSsm/je12SkaPL5OguPogZuKUrUwnsAFk3HVUuRQDMir9LLNvTRYcnuszNe20isgP7fnUuD5+vDQ8/WBl2Z4
Cl6m2nvEeXh7JtD9cM8Ta31J5nRjY0sXUqANtCKdX409Qn3c05+iRjNUVrSQl8y4zCdvKMobKxXPVshXxf5Y/rlvAk2LbKyt
8ENBRvMiXJYkyOMlrdNN0PgwG5DQ7911qlSfmptSsBQXVc9oMqswPJ2SvzcbGNvn208tIpoEmwuXYiIbMeXpk+z9tXnidv7d
2+hO3wT4cqfsvbHcu2MinX4J8HedAda/OvUHL46oSeqqSRqNsH2dbp82IJf/3g9sGvl375/OSBkDxTruFKNiHYuap5rvR+Td
z5vS9bQPsPf389cSjNISrRUVIxXCF+wGgPxgOlZcXGk18qsPEi4gAzCANGBtAhdY6cUZLH7Sy7b8YrZpO3+P2y6za8Pw6HG7
zc3moXOlnF3IHstBkuVECM4AfKXSSDh6tVLfmBWLvvEQFuF4JOpLYUw03IDFGvitNcLg1RphQLRGGNyiY4BU1sFVrdNrW+HW
R7vodyG7pOg8IwBt8TJv3mSb5uHnnBlp+TgL8TyzY1yodlhhKg6Yiu9CVXylld86uODnby69/OsbbNN/ugIe/4OcXgZrYsdC
eAsHAXDYDkMLLl8veLwuzoJ5dsYuO29fhjyucL/xspwWtFPPXTxjp6klReaZGtvyaNYh8+NZ7gNExi7atbo0k2N8kXts1J8O
BQDLuv0QucngZOr43tiVbO7drohHc+yP8kdUmNVCnYrCwtGGl9hi+4LEv1kGKJWke8xMphXV+//wr2o8Nx4pLyoD4C5oPdFv
zwJRJVDAnIjNvqGvVB0IaScbNU0NSQD8b3i683QCRX0S3PXpbVdXR6k3B5xjbJQaPkxAGglSM2mE0Bhe8CMNsSsiJOyiJYWw
XemY6spLorU6LftqE9tvf/crkEQdVfq0aBsBqunFC/D9gGh65RUhzMCMdqrABNBFx0/UazKch2d44y6tv8I2b7/xj+b5MiaG
Fxxwrp1DfkI/ftw4c4W0ODLlTCD0yqpLIi7xmdHl8R8sJHUkhYdoO0O1pst/t651tarirySnpu6lr0MUf8MAK6GFNfCc9ZXd
vdXBbBTVrbbDIVoc7sMdHPgMB+FQnIUjcCSOxrE4DsfjBJyCM+mMZe9DPp80j2OeolU2qyfYhLYAbRvyc6ddnRxUz3d8AMbw
IgUTitueyfUd7qw20AcehzdCiDAKmGjfakAa7ysvQ8nDvWzJQf9dyZrt3OBkN/S4duIoUemh82sxXEms+SNwMJ9igQtxaXH9
M9yAi3Hnzjve+xSfVS5lkbqHO7hZ6coV73OGO7iqj1vy75mIgPlcoyfeFNT/DK6JVf6cOuec9/cf/7vx8/DnO8z/w+D/Ry8c
O3r5m44fNE27f5kS1lFOLNK/+pdZJ1v9+x72njP3d7mqvNm7hipTmeuCGQudzqCSg6jL3FAwLZYVPWe0wikE2ljuEX/WxvKW
yt13Q4wAtNRn7cZW1jQWda8om9ra75G6DqlVu3d3Ceha+kmHxE0B7/LQARCxneM65EBlrYkYeK8nDyw7KpH1qIftrRHk2tqa
EO+z2BU9RbI75rLsQWaFb8v32Ew3j+kcslu1AjRgGbfPRFL3KvPK+fyFzzkLqlnCL5QizehYnDXRUWNKJpI7R2/OZrRZcgDe
nHKpuOVNHmqaL/8Oy4JyN5HaQVUFlalOnrfGtELPLmehVm60tp9eJHUq3ixlVlEkWx4lv1coVpzPfJRPMQRT1N5KzMpetuUi
YnFuhlG8EylbBxhAI2vCiKyclfOzcNh9k//ZoqumLXwmM0rIQUl0JAtf6vxTOqplVzGLs+koszj3FCnyqe7ykGeaRCkGLV+J
BXbnp1nU6FpmZyV7hptSspI18dGNOka99eJFW+YKdXZOAruzil03mR7roqhH0N1Ro8RImfgXOtV0qBkdM1EyFdcSbESDDd2M
aA/+acQWtwAWZvY3Qr0tASh64aSvSDuWss12koodScuKmWkjPmvF+RdkkUb5Hx01rgSicioeHyznPCW3tVmsx3Pz+X7Hkt3K
OUN/anqKpP4FJdqdKGmlyQG3292F88p9InBKJNGCbN+PAFjXUQw64tgntrQ9Qfn7B/QKACoeXBeP6iGV5wZwrpCEMBym1U1Q
AUKsSryB1qu5S9x2mCvwUASXHpfxOJKWIf3AumiR9p/UhDk4ljmODhEY81qZaVWCRCvapw/x8vAWSS1zYq2KUeZzq20jVtmj
eIgiWEo4g7XrYr5feDhnWWejiNRpQlDauaiOyBtG7zzdYxAUGWWoZ4IKURGp/v90r+LT+RWt1uqMyKwFMZ2cZHzGLCxG+R5F
B7WGVm4ip1O2gLbOO5nfLP0A/nhxU21TZF9mG0cXF2V0FOTPdof1YYnrWWfNv+1dN21L+gKzm2FYBrLWc5+XqRvgFnDuUe9U
Dm7mt/3y6txjsW9ssLzQ22EdMvfxYLay9pKa9N3SKjZkK1aNgeF/xu5hnlyVbXA1S3NjCzg2W+EXMlwNswydb8ZVytxEUFcP
zHirmRhU02IdfK0FNpv1m3KsYPYUQwdrTq0nhnwKHSk6PlD+bCkhZ0QmE+3PuXX/J033bFSZJKTH3yThPonm9IhXxYU/C80n
s7SbIbQEoM1maFWO5u7g/s8j4lDNZZmzyjZE2EJkOzLtIgrMc/Gdcjrf2EYCWI7k95vg8hlv21AbG0N+BXx6OEgklzkZWYUA
c6GoClrrLu8kziEQsL7Fwj1YNIiQDgx7UoiM97fYz+9AG5gj/MNwNMuzFFiPD+KRg/Ew/lEv+U0w4WeCl+IJzY4cj+4xiQx/
pVQgsdNbOCoKfvYTxhckxGmxCGr7fMT+7BaN+h+zoUb9pHJQfwJz8zHdu7RTa3acvf42100n3x6Pxh/1Mrq5McNr+/QTwWt3
ET9YfEEkDD8e7eJpGfRexnH8gUufVJvftfAoIhN/8uCwIec0HzVlG/oZS8lBBnhCnfx9KvFZOF5UINDBPp+NP/PpRf0+XHUL
6Ws83AkJIKItoihDYqIlq4hbVSfS2dqC8VxufaCkujH4cUif/fpiMc+2t4na5W27dG92Zn2k+Fyph3hxFHgfiAcyCMN95DTi
PylNAIgZ4x0i4BqekJwavDFebR4Xo4kOngBh37nF3dt6md7uzfkjsXxtEPtHXXflyPFD8YQetA8ESJ2sQqnJ8s1tIt3d7JaR
3mhgOAoZCo3rI9WmKkA6kx1lIyE7ZGaYFoKTgIX70PQwBG6shmcIIFui1oQmmo3xLWNh5wPgTsmZ9nuFjJEEYRqRAThFzBl2
U12Pp4iTgagicFdSScmuGpCdg0bUysF9xO8FSCkYQlMlRC6iwhEJQKuEUFirGvOptREWhTgmaZ173/TN9FEq1t2uM47DD4XC
0cgbmW/KHY1xbPO4uAVPPdLaTcWLydt+XOHjHT7e4t79w/WBJthLz8fxlLy413Myn3r8FIFzZ6u9i2d5ltP8YmBe3kBzZfcM
vpijiOBgW5lWcN+U7GOMKU6mwMUYPs0GY9OvBap+EfJnJtKnv7aYv1Ujg8YSN1yhwbfB9AiU5Ia/rXSvCMk0nbL3EgUQz0RJ
Qm5AKGkauEdBvCkFogYcUpjj/hTdD2rDRGLkK6qMXnnuj7v+1dHdb/PT03q9mm/D5piTkf50PPqtrqI1Ii2dgI6cWy4hDQcd
3tGOtp1D1ktA1/Lllleg51G5QyuaXZ1sYGRNhxf6iJRC53NO/9QJXbWVtSB8kdzkjlyxXO0hCF3Hwlc2gqk6Ho1KtVOFMAK8
coU40i/uHfTpXBWI6ano14e0ylK1gsli2Jsl5OMScN27i+Nx1LqWmlWtnAeCGrMl4jUcU2kUXqkuyIwD1HPJ0FNrIKplrXQd
j5LIuiSDBTjBMVleFEd1SKeEn65XfqNa2hqhb3B/WSZmICjIEtvIVO6V2JIMETfFc/THh/uezVmC/U+WueP2CUuyc8AYkiWH
DJ9B6BE4zRwRSUCRiZVEzBE3aMHD3YYpjQJlPIhgQY24B9dkIYXxAaYx7W9rBoiv59xWlSomUYg1NZVS7NpsSd1rjJlMWHI4
BoZZJpRZ10SUqCoad6NHHGqUcoBc/dPQ/ZFZyVmHdSoKhclkmDHyrAlSJ8ABjolOJHtZBCwJQlIujFU33oIMbxk+tKtpvi9H
Tak/NWGTk32ixUPYaxrX3DDxW4WHTvkIV61sovUyNxbv047GRuWdqQJE71PhdJUIn0H1EGSO5fPWCOqcXHNi/hynBDpjUoiw
h+5RU4vlydbN8SLTI6xIEHfpWL6DblyH1nYN6k6CS5nfGHwZdiTLGffOmqRo7k4t2VF5Iku8agFMsCLYOiqX1rUUg6sP8RSO
MVtVNqoRiw1jt/+2JHM2pxrumXlJmezmpxQrAYuankJWQIxl50oAWMzgpCB6IQxIzQZJ3ykAb8B3UtlgNAdXxJkdrMKdnbu+
z5f0DmA+UF56OannOCwyTt1WdxsFiHK22W4JolmApdUyawC+UPjGLp6ekYDOCVp9B+7YhYwKxbPnk4p5ZgwdItxNhequzBEU
IjBTZx8oXOtYVUew8wnUl2FSYDeiAB6DihCuMtq16BekZIGyacpniDTZ62aLHC9x5pAXTskf1pXg0nJCmC/91uHH8p9I9q9X
nFCB+x0/1jvRgF3Iq0ve0MpoWAJsz69kIW4bcLlHDubqjAvC0fs/fs+GWOJmmi1NaK3KFoP4WQH1AVltHtTDn0+y4cPzts0V
cTuDtNoOSmmDADO7sIZpY0CcZGwmVsBOnE8ltN4Y14TwNUOA7xHzpPO+lJ6UgkFFqHqACj2BXwIB78dK7uCFc+8es2xkUOPr
lRl8N83Rjby09V4SNM0QiGENw8YQmZX8ECJAsw+7k5wTmQ95qas/GV6FH/ZnhtV1pD3ZeStbYCAOPYz5RhGpVRuZkBs3Jlyy
IO2H7zKvs0od3bYQArhKMFz+n3hDz+kRT39xmL9l892bB/XsklwOYe3+MGoWU+wnTt7DvEvkfnyEUEjh5GPobjDhuOOYOvZ0
3MMn5Qo+8zRZjAY7qYtOM3sGcyo7D4EtmP7pEWPsQt8pAYIAhzXECB3MER3zHSWSyxUHwrI4wwRNS3inj0VkwPirG/P2KapU
3EMwF5dKlzgjmK3Nqq75jGvjGjonx+T/6mnkYQNlWsK9LokV9yOEJeibixi2vYzaxx8nz5UCHYrQvgOPMjoKgBAZl/COigUC
qO4cxCb0q5Gfr+hKWxWh65khSXPgGKhjO1xzhBp7sn4JL+QIO2Senzb14nuLDoUOgw50hyKjV/q67nKeD7OVqmw0yZnP1sAK
1KH8zl7bepBC1Nfg92X1zsn7rXXWSjXypBIVieohQ4qROA5TAaqgF506CbbFVlp6l1OyAXxYQ0GDkI+grF4PR7tURyoOUWPr
kqHD0NNjrttkup0E5cZT+h5KgCuKVWmFVUzg/CdB88JC+ZjzHHSUady1ySv+xIkfL763RtWaRBDZdlKXeWklFFVYCBUJkMFm
NHpxvoNMOkKBakqp40wp6IAWYQSfB2CazWsEewBV9Bkwvn7lPETmw8saQx1uDl2c8qxGLuGa/7AZl8g4Ty/ke80V6Cjvw1WA
MBY4EaepOAECgqX48/gBZ3Pi3EvG5KPRTg96Xc3ldRq1eGmkozYj62osjc/200Jw8R1KJWViS4V5nQWj/HcQ34VX8H8KwS/p
hX5pLpLOi0Lum+5FJz59+NrjwyYtHluIN3XsqK1wo/Q2xkxaCWltb7WRc60lhxARbIRPfT/vy3xjaBhdx/GoRuLPWufDA407
gHlqRziDBV1irBKk/AWRJp7sqbKtC2H4RWIsIt11KCKwlJheMPeGpR7cR1fhlz93FmZDMxGQAPMgMprcNDBKKACfvYSEaJM3
vnTup9jl0uvtIuzyRMy9kxwjR3vBoefJGyjxiylJDkL+dJ0au7r5+fEbIY2hvX+bRvdWZ1Zh5uuq2XtNxUXkM2bCOU3T8NpV
fDtSMy3WJTh9bK3GrfnPhfWZ6uNaR2If8ULwVpp89oKAwyrhHWMyQuSqDWEQgg8Gso6aUvuyjzQiUi4SYIvA8yKnbZXWbp2e
iuIDgz4hwEK897VzvxsFEOzv9mzzWUAO1gRdTyF0Dj8K+bLmgJ4v+JTEfC3nXhPtb0Lo9ex7WDIGJppNJ5vWJbvYYtSHNy0b
Hwav6TNINkXkNtkLoJapNEpkAhC5qL9LPI61VCnuIBCvToTYjXb7fX1Nz5zRVinPPEkeplTdKNYqHbVNJOl1iq4NRyPw/g23
CgLtBTF1w4hyeJZGF2EpDpiHHSAj9xaUZ9a9OTANkE2gA/JeR8YsnBlDiYUdRbPjL9ejjc9WeK5Cjew4wotRP3KufifHjbMh
dFuQ1F6dLays1PFAB5mIT2WgDFka0XCjx1k/jnztGQTkmzyE0EvoYAwrlqrTKuINzYWlgYS9gHN1xU4ywa3GrGXMo8yFtWkJ
VuKgkZg3to50qVVUlZRGoAISE/LncXz2U6t1nK669CBEcCJAVJJfcsdcT3w+WJaleSXf2jLhJWLMxBc0LEO4O+IYDnaHbTma
+dZWUYwLKFvcL53bJaFXSERoUEvb6bPn65DG0GqgON3IXLS2dNN1kxFSCIsCSAJcQA+OyASJ1mctwDywEQ0IDzUNM7dPFFSJ
HGwW47pIqf4W49mwTT2U7q32aAGClL2eOUz1PcgY/2ggskjJ6X+Awpi+isJ16noMlYEdD1MLOyRSVKOnjtA0I9rzvLKoGBIo
iJZB1go96TEYdojI54h6GzWKH5DaFfy2VHMlOOu0v5VzRkJEiL2hfmAPa5RPNahkYb6CmtEjcYVaS6IFB+y9AomSIXlLX3QG
TaD59vM7DSH0w3JBm0gMSAfgjeHeR6cH/LRg06l6nRoJifEoCyASkxSMaQTnAsT5t4LxmP1wc6+85RUBd4ZlkiXudd6RdgTt
KoFYmlwIvjOIkntjGbqd1govA1XpDaz52vN4DvuIgZHPgoWhPnddYptblpuQSdZJgKMij3VkCmBnNyEMqnYzO0lm8GNcDyWa
ZMUsjAkTG4k3tSlMEgSKkopOFAUAngx0iAcB4UZEv2jA3G1dz1xR19P61YQT+VTeGy4d6KZTGQIpzLHUP6MxnW7sCN+hYpqO
/SszyqX86krm/Rv3Du+enOwdHxzs7t2pyQTsK3Vb3La7do8xMdpReHfYfr3X6e7k7gfBqg5u3bh9+6ib7R/eulvuHh2d1bPd
Bu3JniTA6XwDhlsRHCAQE2x4gaOugQL1el0p0xzJWB13iwN/jkwkW0IRG6KL+ZLyoHwzbE0yujHX0lAjfkh/AjqrnbvkBFyC
RlxGN85IsG5u0Ee36zychQ49uVjYFJltSvxK0DtPvxM7mxKY7aWQsoOI5R380nTziRFJeaZN35Eqy8uTKak841qKytoaghKh
UtWrEUI6lZxChzU5fYjZYaCYC2gS3T3T5Svtj9Gck9jdtNDCtsmhGiQN5X6JLMMgNOJZdGPXUDvJR/BD23UtnIVWPCUU+9tA
iF427h36SYAbyItqmzRnlC8KNQ63C2SC/tftpk4Pn1uEsFAZFX7DB8V8sCR1Hhvw1VbRghhDKzUyr+WO/GD6YnkVR+sk/JE8
Cz0zl/wmGEi9LswlqaEoamtahYTyF/xgfGLbf7kY1q3WnvOFS05Af896kBCqvw5njhQCpXYOlKIq4ZJs1I1RsLrSHyAQZewo
mFbfMa4xGaxdtsdnkItrj+kmE5u8RM7kQop0p8G6hzstO3rTqua3WWYXtGnOLYFv0I6WjcmXMYKpoYF7mMeDiWXGRHaRL7o0
CpOgsqaBa8m/Mx5t+/eLYSgcKHnWFaGl+ouQq/lhcALqXdkqnhpjTd8CWGVbJi5lnssdwYUIoWR/kZHSJGDRjw+Xm6vtzODu
bUVkeL5zdGeT2yRlnE82lUn9PDxEYAwOJQ3qAIswD9q4hgc023Q+E6N2u8b43bb/3uLbN/xvTHT/Xo4BlqDnl/uUYPQshZTu
O+3grv+t5+mlh2170ekDcrC1XZr47Iuh+Y4nddtLGThqI3VAMnZwUGW3Nopzqof92WbO2fkK8uQ3Pr3b8Sd4EvIkTDD9W8Ip
5O688NjPVMP+FCvdGngDYqBbTesmTP3HxnPb/u3ielow7fkKezQJU3ygKIiviLd7/EztJzMXAXhYrfEzp06880Fjr+xgisSQ
erhAy4aNsLAUym1qTaJxocBA/bCfqf3IasxqjKvnQXjwM1czLVxXkudQQQUyZ0XcNHHBApllU3n8HyXacaUDQgL9DGtY9Fy6
mAhpVwWhIIsiqGLHK1c+K4ptQePPmHuUfdTXYJsr7QVtO8+U69wL1NPUjNDtrDG+ve1fLAaHnZKsgFul0O33zQp82A2jM3IT
z0Mb74JY120sIeORKyh+RbCrIqjfoWOIvk2YchO/3zt7+yJtwAOWcuK9Wb8327c6syqvRtpUpsyr/CVYKAHKwp/HS3VWWlso
p7/Pi0UBuWHWlgXLTXcJVQW2ga/CaGdH2LVu5px3ffmU2rzVNFMF7v5QPd2eq3NirG/jjZPNjPGNNYAjGTtycXmeqGqCmJ3S
I2aifN78r4cdzzKNm4O/H71dUNO0eJzjzA9HioCgyaaZjYJQuTn6WLnWW1e+ExiwWNplg4EsMB2ETtdfS2/QLZc+P5+RSVbJ
5zWjTpOMHh2Qb9nNQ4nz80LJTh5LVl/zocgkxHVeeuy9DOR5PXFUalYj4MLedEZBFQq1zXnW0aFk2jRqUAA2jWfZH81P1Cmc
tIHKdqIOlQRtLpqpAnuWZ6+uS593x+/xrNduCvzkaL5XpuX3+hTBqIDWNc9TjstUvaFZHtPnwTRLIuWQD7pbn9y42vjw1Zlh
t24Jqzx63xuNk1tyIye76Nn0a7gVrIioFeKckc85DWg4ehXIlp8L4rICfD98X74KmwQ84Z1uTG/vb8uX0uwme2bjvIxP6RLy
f373Ldvv3j2ZzB4njw957WQ40w3JcVYac4T5kMjb8gxhmRSNeQrTA/Y46VhSx561fgTMZkLunYHAhe2pbNeZm2R1YBXTzzvM
8pw8Pj9J/FXmOJBWsUk8wTXZegnvbGZGCBmsyCPrk3ti6Kiamtj33ygDMm/dBIY8uckxYuAHBmhk8ztBptUE+brdu/uq3WzP
5eE/7PnjtObXy1VdsSLuTNHs9WGUUkLj8eThCagZlsB/oNjOu8ARgm8QS5WqRtxIX4g7qhAkaugY8PXrLWYaABNWsR8IjuLL
B0JXtEomeBqFbLKd4H7jtYnfI/yAF8B++B3fVsX9X8+EXmHMiZaJzK68dXHgZd/YjmfuK/BR3S/92NNua9Zy7VhuN0fWdmJo
qQatalkbZPYbIA0Xy0bjCytMU7ut7Exxg2o+iS7lueTnb+RdUX6mvCplwtvSUosTMa9OzDw76cvwJJUyePHyUofViwPvDl+D
u4eeH9iWFUHOHX+htdtGOptu3KSkHWxSeFQ6pbG5Bm0767e/UWiGfs0/P4AfZBsBfL/hD96fK9E2f+PGBTPyL6XU/Ln9lup1
5947zbN6DKnrt1t5foemotnhvQffgZtNypMJT6GRyC10faZzb0ylOEU/Hw8v3uGdO5LcL17x9ooRz0uNc3WyyM1swSfod+j8
XCFDl5QVcIkKN68DoQHd6e6jkCXvELo4MXOjOyk5czl6h/7wDO6re2ug7oVytB/oPIHpPItQrzSh+pF+FAlLCQnPM0ni+bL+
so3Fsn+Pvf/4bp3ljHLu41VbmxrzpuBq+21YND6Xc3yJzlqmbXTGrKBZctLmue+kXN0c/gd9teaw/ksy/AFnOGdC13LamKJw
pdad9W1vWa2Ft7W/9EtLLzKjXStqr/mliYWL54e7aDyld3Ok1u+qk/OTMQMi/W6Laq6isTOBYuAggIlOWHSGQ+fOVyYTcGDo
9VzwOTM9obWE5IScSprRkr1L5Zxa7KfsTUWdPxr4VCLnjP1KoSAwhkM5WCQlFpW2BVdMEqE/6Y8XcWqFUSheG7XTbKOY4WT+
MIETKZBUSRW6iVaxhBNSiCsswKbF8h+NupD1CDJMjRXQbrvg1HfYxvl34UUi9lRHKg07BLPvEjR2WqUfl0vymdV4SOOfGFfb
WESkuIUavZMiiBlQap4A++swyfIHatAHV+CXC6Bn9AURwQ5y9P8iuwMIRJ3j8e/pOpG/E8q7rbcNgsK75mSe8I8Uag910q/b
qVH3x6fjl2Xd5Xj1Pn2P/jNtFzOo03IpocOTrLlqLfyjpoxqCqEQl8clCeJS7/gRRobA6kbqJUwS0LOyuVcEF6W5PGXADMQl
krEA1yXuwouc+Ii5HN07He+5BX+J+uIPrGFYLPf8CqRP87nlMa+quTzeHIF41TLI944YFkXURI+X0tPMWYT5BXRe7VnbE00P
HMDj4w9wI3Ehj8YzaFc5zpC9LWSPia+3YFkSnKDDYcnkTfhC531aeNLyHhm6hycD4GwuMijT1RiCrk4PlYWQRqBlxtsdxW+S
TXrTBjAka1xkSwQoMFWAesQOAJfYZiWs96nUI5VVlmSmNTcVliVVioNJZQis9Hci1EfqadbMiQ+CkBLNOcefvZi8I6qt8TaR
falrS2Ek4YWkSy3E+PVS5qN8n6TAJ5nN8Dl5R/OlxTmdnZCGttYRfWH/qHpxL6bbM+ucPkFqeg1EMsbDTYhpLAu4FE0dEDlk
qnPqJVc8y14vxyMysDzqv8tO/RM7DTRn4E8wg0lwkHcW+N7Fbxb06a8RV4pc9BKoOZQkPnxtQZsxPe8ph62TIRd8n+OIM4KV
aEzexh2tmw5vNOOALUx77NhobHYw1NaL/uSyyh7YIjSjAIgSi84e+5SYx86bY6/MI//zxZ1Itz8kZQM6qCMYwvWQkksYgfRX
LJHZXc57Lc8jatzYk1PlPNuyO+FMT3UPIypSJS9Y+hV7B2sTXMcDSrqqu64dpjWmoK189syLaFu7g7lNMsZ5bTx5JD0MFoGe
/bAoFkBYhBTj/ZSosOVMRchEgoE4EVSyRHweHHsAb/s4SchCpVFgqQxe+OF2ogKS2S0sE82aJih8dOCxCghjcUXIRFXoKF0P
E2aKLA9B4vSaW3ZY3qLrFek+uOY7oCyRdsi5U7RiSoUjPznxSlqXUSORFnBDtexht6MwGpOAS5fKoT6DzB6i79Zmfl/NJsaw
SQcVhj2G0+uMaeXon6VQft4xuX0SbAFUUu4i+CrWa9uodw23281V5bWVh8gaFNMCHYnoYRWvYPfFrjcA7sJUyBpxIUjcWLFP
oxc1eEnegZmqesgoYrrqWruLS70Jh+kHxQQaPfs3BS70KCpmjc5hDo/ZD6GRjwAd431A3S0kXKE3PguQWipkjRAJVhZl2CEZ
IsHXI8KE1QZ/pLR3OFukqpqmJcwATEluiCsAJ3FeIWpNrHhCDVa8+rI8YrSLLgnzqePHH2G7mT9bL6i/v1b7FmqH7FR3AUjG
oMWivTTEO0zu9HCOgXFgQeo90kP/WtO53Clm8BSJvU/CJQporEI6bTSXNMaUR3FuwAIWeQ/OW+esdV7IoibWPaQMgQhWB/i6
iqj6tEb9mbQOxiVCfxeshkvFrK2XbMmU+GGorF7Z+JJrH2QcEgX1sEZLbldw2sZCBoY/cRaeWbtAeMyfwsjCryAIIc7ty2b8
lYaa9DQnOFlgtGI+WqDNkG0EDLjk5m3Pq8ux8lSqSCjStj6rysBpR1OHpSaUhjVW9usn+eOawM45WTa1PeZQvV2usQharF53
iS6dGkb1WD6pRlaN9lYFs5BRG5idqkV4ItvIbrxX90bo6l4l1kwIDP6YXCMP0CRTPtKwdOwcBbZxHPYFi8F+Or4L9F/4MK5k
y0mhMglEE5oShMpyNDiGTFgj1tCw4tT3cxqx2i4BGvJCg7qb5yvR4+XLcj6cLr63+N7IrMyhHhivtK9eWYq3TLw+OZ3fbmae
3AgFYLShqcGicIQT30nfnl6mBJO/THVxK+HzaUXj/aNxdX59Tu87R67sSsWYQS50ZTmiR+G+gI9W0GWPEnwhk4+i34H1xILb
FtKOEhLViGe/ei8rZT247XLTtn6u2DmmMafDYDR4Cs3IzLTXDZHbSAPdO/7WguPFd6DqJRxi6dOH97GQRxfK1AQUm1vzI6tJ
1OuDVkPLvaPUbo6Bx1O0cz0bWzGP/FYaiMam9NijD/IZlYv4KTuat4zYq2Frd0FrKqk8iDZs9dKag/MD1YTVUuat3FGe1tSq
AjfOONCVXAAdW1qM1YgMWZjPbiVoKnfRtXU8uq0xLlOTMHTNHJkvTdXxzhYgefagFyb5UZ4QpOWKF91zzndpPJWy0yKXUtsE
C6UUrhXDc3NMNWu0lkP10njRchhqyTPpjDGBY8KcWdcucNIbT0s8tH0jIDj3C9s533uxWRJP6S75ZRJ/C7hIRgoApKKuAn3y
ZhAd7NqiT+ETYqqOVdcXSthqKtJERbNFWTbH7RJJGMjiH913lPJ1d12/tytilRJ89RVXECwtVZ6ZHUl/4nnwh9D7vLWuRniM
UkT7s+Hk6mYJo84rVN5aERvdu8A5dQeqITfHk40OdH24p7GCDYBApU993Plupg/F70b6JP4E20b9E71S+QmlD4Xvhk8m811D
n4SfAAFXnayz4SOTLWWpAyN1aFhhRenIXMNqpaErXWho5n4XBi0Mxwid7fKED4d48n6R+TZaC3TzzpsXkK7WmhtVt6jdyMSs
FbngL6tUBF+KHQlkXc/1ZiFz1Q+DvWiQOGaMoHNHMBLAzYnhYI5JnhB7aiDD+kDFl+zW5pOJS52FzDTSdXriTuuIjv6w2DYf
UBbl29AazMYN+/AIM07muFjMU7fVOinjyp3QZiuXlIL/cN/ppsy42yHML1jF8KnRRx4ompVNQ4wo4sARydoIlPFdgsgo41Mp
ip2o6C2mWoFucLAMvfDL3Ra/RaZq8b7xG/ILJwg8yk/lScqoG9ZNt9tpj0Rdk0za1+t3ShY0r1g7jEQBiwlrjcbzghzRPBHI
Aj1tVzZcDCl7mjM0mQB5UxrFOqaKuugG4WW8O0XHXuLgqgEnW9rIAgox0BbeRy/SiX5Jf0XGk2fHpIuRHx/m4qJ0K9OMO2dV
y4YHzaQPXlYgR+cF52mWTQjUFgjonVNA7xMvCpExJb0YLqZEJCMlQXbLI6Vluy5prIjgq6laBYH+xBhu0lLmmewMeW4g54fV
9LqqVBMqhgwNdUvNugoRC6/K+xBOAs/7amx0Sf8PqWK/SLs1FN4Kw02iOzFUb0JYKMkuuLDVuSzXsVm1Sq3RqrdmnMy+AxmX
0hEpojIpYj6yzk5tma1SdXNE2SuxbNRayUisG9Yjz8SaHQy7e7HFsYa7MDO46HXLVHg+TUbbQvauBdFBOz5nJ7sk3qTjv0Pf
qM9akKLAvrcvzuC/p9qOvHWxrVUxRkVf9QCzTaKXVoP1/CdadaQbOpAjkoHMh/FqNKRSDZAtitQ6XD1k3QgTZVKq5bBpmdao
wjmCSke9ykQDw52sRrsSfZ8LonwsNmaOihAoUDagkY2JoIo+DzPQoWSVW+YJLn4w57dc1ORJQkBERRC4VGqWKJUxHK3tDQ/z
EShWlefuN91Te23tnLnimzBEsoEFLgZ9vtL+9+amZ+XkuBeL+S6AMsQMFVQRyXZCO2yukkG5GTbSjVUbmbinBqQG6pVq6dT5
2F054rCldWMHO0UdI4pFVKDCUGsgBHRYDv0EIY8wGHQYGPKnTEyWFFa7XCZqM5NKpM0MZkReQ8RVrogKzankueWHw8xJl9A0
TCFwcRSqhNAoTkGfbpuyStPtGBbcYnfdsjBLAsc9ZoEMIEhmaYACR6zhm46mts7H5q/SG5zCK+hUT2VBuEsixeKYJ5Qg8CEE
AT6AkGES1mAD1Jh6CY+sU6oXi8x+2waMhxS+QfXTd/+u80yQUr+5EqkLQDGBGkuvgRIMmIGmWV1ndMuhgRAu9GCZcAitQb/Z
K80ohQM86Wjo2WKc9e+T5gLc+Bt/cl2O7WRru0fb7L07noquAcbpZrGfZbZDxX0LCGfhX1RS7DTs4R5WJnUFn9/D2akak9uY
CXojqDJM4jpJiBAZQBx6KoIpgbbLEhk4hSO6P8HnAW/CGZK0lU1EnI4U1RgQjdY1uKcrboVOT0VVFHgsAnW7etOdQEZMpmuo
vCpNWkKW5HnrvUlz65vWO+k5NQsVMNeaidRQo0POKyAqixMi5/GqL3A8TeV6a2Rn8kR+ILIRf9NKJo02I0lKvLlm84IFN6TF
lPg9v0pDDiIZIZAPtsQeMsnRdsPEbhS+6nkXN9itnQ/DP31xJtGfMkpvVAtKbIRtDQFQErjlGOaKpXXF5n9geKoHzNDiWrub
6683+m7mRPIhdzrxSfVKlYYJoZifOfFZnJMo3FRMHJEVjLaHe6ObIgxmD6Fd4F7d5uaX8kUmyYKAgSt858ZbMTVBxeNqEPc1
uLrl6efXGRl852QQD2frXEfODTo3G6qF0tMYDYVZE6pG0WP58C5YgmqU4rNifn6pdKePNZNMD/NZUaaepxIw5Uqoa5GzWRDk
AoES1tCPqyI3x2bCIVxAZFmjTMLV1fEMsi0tAfSNVKe7l4e2HRKgY9ntbIkdGCKRTIdeP6yvnLf0Fetio+GVZ1BDDwu+qm3a
38G5rEbDCSn3MJkc1o9mA1NFVUWkZnk12JL4llOsU0UqJvWpcZu5XIGBpVAsHQrFnZMrVDFgyHKw3cwZqYguI+lOMS5ZnO6z
zpbYLreOzHbPJfZUsspIVdRFx1FzNKhWkApSI9ffvF0qISqFXUVphQvibg3LUXwO9a9a48KI9bwdQD3bs48tmp+1Y5jW58Gd
cS99DlXN8tbZNZ6X7hJahKPk8wBW23lcZT0t14SH8WgHesq5vf5vPFpAj4SH5OJQAWIUz3C8itFj+QHe7ffsvuPCJ4zjTkA2
VHnN4Hl2iPkqTFRiLfvIoFf7YhwcGxWjFFXiN1I9GIQfEZauqeWcz57jsnMdCCFUU8rAcesF8MxhoUirIfivu7CHrvDc4k4S
RgoO4iUGQCPXXAQLrwSaYsdsV3SUge9ZMCKIcxkbNhdeboIf75n+3/3R0EzQ5VneAC+BYfB93xERi1BPXc/hA9bpbMeK2+xK
kTFiQ84zk8cJxKDjUNfx3KYBZqe5UsLsm8B5AGKuvZW9fb3K6wx2dpVjzKndIG7bmzFEGHInESzKbN2s64vRE/fvX+zEzrhY
RC5ONpKyjh7dmd0ZT595affZozuhWf2z9I7v0g5WQbtQ8OMHUJSRAEP3HiYCZ76D4q8dbsTLkAJqw6DaO2SwFXZPflzz1ovU
/L0Za1/Qvqn9NZ+kin66e1S+oVAYajZ+v/EN48p4rz9slVLEPy+u7Bc6Kt1195QPbRu7t8RS3CpmE7roUhnYYvWaV7cBTBma
FZTBi+/V7yf0jzCHyfHnWVymMdC+W0DCzW+13qa9Lo9Szk9AbHN8QRw6R2MeR8VBOKZ95wdidZDyxtQZA47TMkx/kvEy9v7q
JbevlDdRBmkCMW4M1cQ12YUFBT5x1CStEdKCDIgzZocpcCr8pE1G85GSMlpRxBJFVZVrmPmlDeT3Uu4pzVr3sEoCU3uWWJEG
8H8WEBapxwynRhPrpm8+uzohAnSC6hGvUxcDzjroIy0ja1r9gwdMwxO8pYlt6SjMMovNt1QJDEEsCvmg3f+yTbFSVwowIiMV
+ESP/VRZp4VwBIwpZWkn6ck51jVfpifa6ruEuUDLyOVfejvogu3FD8sKg7F+ZRlu8ycRkvaUFtaMtiDONcQ+jZ0R0mdhNwSe
TIO4Lcl5NPihZ1/dJGOIz3tejgezWbESwaXOKhkNWKGU/VJ2oCkwHSrN7bszj2ifDAZGEFeG2oVeno+02WikJQlvbV+yQuqO
SWI5dhr9yVDsRINwUuUCmyjiblcIN01okqimQzZ/+vYYK9cjJmsiWeuQdXuJ7yZO8ZQXvFPTvo1lindtAh9kY0iyEIZ1houW
Nj5SE5W1jiUEDZKULHQa74wmms1MN63rjG0UYNFdN7L/LO3xAEJgN2fzhbgSO5JlDMhWPNIDSKU9NG+dPRpESTMLNSm+V8Hy
dOFLSpmcNd3G2Nldk3LGkATR0lY0+LuwG3cUBOkWOEn9xM7NyVJg0MFb5g7MFiG6uUsHRg7IV0pLktRCY1RN5e7FPWb5ZL6A
UIYg6HcuGoPdgMi8dteTkxKwAQjA/fX+bk9+/f9BJGbw/2sfdvT+KOBdMyvP8Ax/lhIwK69/B7Dx3gUl5OWWLvfmf3aE/GVl
JlUpArVxNtTdaadCmK0NB2ugRicOrkDIXvC5hHqgMM75N9UNAfwrfqAWsRlB1ZoMXy/IbLJE11ZWqVnsygyEiAb0DgsxhHMJ
WevKOvtZELMV1A6FVUNQ1eqgdWGqagQUGO6yOxKzwhCDRmoboaLum3g1CXrHoFVnmgegtnxf3Z2dA4jOCR+1KSSmIiS6JIhW
RqkSKPGF+JKAj2rlc2ksFHu8HN0jpgLY1Tky8+cdAflWmVDYyFkDX8FkBi88E5A4tphvJBp6na+itZmZ1iQ1Ng2p7MsKjfhE
D66jEu2nfTWLY5HAsCjE3U8wazMpl8YsdzqzXBWMigbfSoC1wyWvmbrHD/0A96BBrXtIMAPA9SrFHm+lMtlRQlKmTWkIswx9
xSCxqaPImuwxu9DqjJujJToDZm+COUlM47OQmOe8OFMnIAYagSKA8OgunKzNaXPPY4iEZEV3LiEQoRiDSwAQl9NkyWSeU3t6
LoZaxBdNwRs3NcCNvvi6XsaeR5BVunMJ+ejCMbgEYDQp+IRZWQV8w2xZII9muwt4hWxQFnvoq5BZyI0sK4XhmAreuLcBTh+A
c8R7WYY8Ua5dvHCn1qzZ6VxbIxJUvuAxqwJj8ClL2oZ4iFMD/nsAWcwmImY3IJ9ZBfYFMisLRAScRiXyLkGes8qG0h3Rluui
G+6Lb5yg33zcxIGI5hsXR/7hFsnpMGa4X5ORcbaLDcUwHGJ9bHS1smWWe23ggjeWPQ/sJMS5YSNgXqSLZQ8EWEJdPg1OPcAf
0Ia8ORUMSWF1Z3KGehbC/fdMrI3aoyOX573BLtsyBfGQubC/7qinuTFmWz3ZCyl3aAt6aITxeCR2j4oByaCvGfbm4p/fd/TK
yWUQ58Rur1qmROsakzs3zbIRrKBwDf7C/f3UCDyLwJSfQXHlFH6TORcw/44DdlSnEZDwKXOSWS1vCyDCc3aMF9mBC9Vl8gAk
5uIEswBg1zP5QoNGoDma5BkWnhWXlvsMifGzHSNtkFciH7DD2rybGWRmutNQIDBlz4WX40NI1U4mMPO8ioHYdKnsstF0hp18
BYKjr/8PJ1TL7ofTcO99ZPbbq+8FoCszh94Ytb9RInXs7eGmYECmW+gV8Yhn+MIJ/0YTGN9DANowH+xhJnCC/i0KNY4Scvka
/gK+Xd5cXpxwbYV4g1gXwH+jN8zoFGDBdXUgdY+qiRNO6DjSkwWhNhDpVbS0EZLPkpMCw+I8EzSHk/ve2lAHLBpe6mjkiVAV
/TxquSjXBgL1BVgS2YMN4B5GHAMXwwkCrFDFEWAgA7ADAezI98/5XYdid20RbAHfaQlQ4PctCbbzGtXpwnGrtWrpYM3slgkc
6lbLihPsq/uvwclegsP4OPu6rgW0WwS7k9wSYAa/aklwICO4aj+jdroyLR3sr2fLBK61smUFOmCzWQPCvhNrEgkliFiiwQww
0Lw4FkOUWuGLH2GxKEL2sQj8tWa8SgQRQ6YFcoxMEGjxRkvcPVJw1WuhQbHRmI/aOaNe2naHaeaiODkoqnYVioH3JqyJeNc5
UYRzYE3JGQPgfKWFXghQF1WjpsrD4Lnpfz8xHf1mHC3a6RGYvRqooFbHMCirrU2x5zSgeTluooaKmhpGGWNqfzoz96BNZdbz
KH/p9YgdYPVLLe/FwnJv91c4iL/y4L+6yf5Oa5Uj1y1VtokVO6del7b4bJKtwsFhUVqQvRNs0O0b63uLK6ZZrPD2KeU748WH
7DycXNz+8NKCc85bFbBX5rlLvF756B8ywUEhYaHhTSKYkVHRsTFx8bveTkpMTk25rll6WoZMn/xry9Zwa5iW7bQuJi66Hj6M
oBhOkP10uKcZFpvTb74sjy8QisSSVjVyMrlCqVJrtLoGAj4NRpPZYrX1x4mfmaLD6XJ7vD4/Dk9o2xeJTOlX1ll/tPxqUpmi
kqpEpQ5CMILSGUwWm8Ntzj9fIBSJJVKZHFMoVWqNVqdvlMTQyNjE1MzcwtLK2sbWzt7BsV42Z5cGOd3cHq/PHwiGIBhBMZwg
KZphOT4cicbiiWQqncnm8oViqVyp1uqNZqvdAd1efzAcjSfT2XyxXK1Ny3Zczw/CKE7SLC9KQqu6abt+GKd5WTfsx3ndz/th
v2vD8Dm1a+5TGvUA3wSJHpP23Ln42edX+q4NuOirHfdMK1enQ4kLKt3w7N443S8yGENWCxblzLLa5J6kHEzNV9BmxriSeLyn
ONaiGDfi+v/Cc4TYMopcdiWl2MgO0oUY6z8DtezooJh7DKPqamLyer+RRO+tN1qpGLcJak05WPdJAudTW3slT93JSjNPM7yx
3N50kCbY20slN7Xw9uMYDvkPTUR/+/atxNHzcYx6Wo/YEsZIDDLgnbQirSZe4pl0EF3MmL7o56jsQ90+ir3v/L65aRFB09y7
aGW2WnhNzyKC1oB32cputfLaxmqlWse7bgJnUQvWz8Y7Z71W6w5yCHvXEQwnew5jbBhu/WEcW+v4L01Y8SDH1VVauq8Pb7jT
bUOV0VGUpSmgdkjHax7S1uky2nh2mPYqOVvmO5iqDUbKeu77qFu4B0TSxGdbg+L73lMW4STcajHEwJZJJNMxAD8ibhNRysGz
wkd4j8ge+UqZHkGka2zKCJbWvY9tv5MCA2JdNBZE1f2xrGUeeEHl+zGW6fopK3VZl33zv9PNNMlYWqTtBmpd5Ea1PukhqbNV
2mfDfBQmy12WT4/xQnVw+UlhaU6yKtbJLCnD7AxT636xW3+9ZGvwNGcqa9M/hlWq68gX1wfzHAWUilibcqMDCj5Qog4QAIAJ
Zx1UxNqUGzMApSLWptxYASgVsTY1Fy+ImZkfHR2UilibcuMEoFTE2pQbNwClIq6ZUBMwb4/M4gwoFbE25Q5p3q6pGxXZjIOf
tuZY83P09lJe25DigcqbpsbAaYM4mjS98wdK+OF3zuZVy9O/bvu/UwZ6TkYTW3xI/PAzXvnukalvaHTjuF94tMNApUT51vLm
2H7sdAtWvmvQLC+BPf0FzoVK+YGsTD2Qd5os+AuKlE8lqloXVatyQYVyZUs2tf2Jr79m917zqle82H/xmt57Qed58JzGgSX6
t+n48PpC1udEeFaIM4KfFs7LwnxJkBdF5wWBJ4Fui42hfXXoXB/ZN0fO+Z59sedsCntbOCEy01jXSKUb4ne+qkRA1gCtR6cT
6eP51/l//v8N/xP4cfEL8T8sVtKeNapLa0YDK8u9tYOtti+1L7ZX2Yv2gs3sOZvak/aYnbZH7D67207YYFc2NQimHXAaBcwQ
QPWVN1lOlGA13FgC+ypH3JCQs15gR6THIwINTPbYiKaLh8PxiEyNq3iErgDBgI565IxnWbP43X+uwrx/9o7YeELnTXDMQ1hF
KgrW6s5uuvmmm+5vWnh7lcRLpY+rpSphFZJBAdx9YxzxM069QF/AAYa0a4XOjGilVmjDvTXsrRzBaeHhN/DW+KAoAAA=
`````
