// Preview page logic, driven from PreviewControl through ExecuteScriptAsync (window.markview.*).
(() => {
    'use strict';

    const post = message => window.chrome.webview.postMessage(message);
    const highlightCache = new Map();
    let content = null;
    let syncEntries = null;
    let syncLine = -1;
    let syncFrame = 0;

    // Runs from <head> before the first paint, so the initial theme (?theme=dark) never flashes.
    function setTheme(dark) {
        document.documentElement.classList.toggle('dark', dark);
        document.getElementById('md-light').disabled = dark;
        document.getElementById('hljs-light').disabled = dark;
        document.getElementById('md-dark').disabled = !dark;
        document.getElementById('hljs-dark').disabled = !dark;
    }

    function setFontSize(px) {
        content.style.fontSize = px + 'px';
        scheduleSync();
    }

    function setBase(href) {
        let base = document.querySelector('base');
        if (!href) {
            base?.remove();
            return;
        }
        if (!base) {
            base = document.createElement('base');
            document.head.prepend(base);
        }
        base.href = href;
    }

    function setContent(html) {
        const y = window.scrollY;
        content.innerHTML = html;
        highlight();
        syncEntries = null;
        if (syncLine >= 0) {
            applySync();
        } else {
            window.scrollTo(0, y);
        }
    }

    // Highlighting is the expensive part of an update: reuse the output of unchanged blocks.
    function highlight() {
        for (const code of content.querySelectorAll('pre code[class*="language-"]')) {
            const key = code.className + '\n' + code.textContent;
            const cached = highlightCache.get(key);
            if (cached === undefined) {
                window.hljs.highlightElement(code);
                // Note: wholesale clear bounds memory; switch to LRU if large docs thrash it.
                if (highlightCache.size > 2000) {
                    highlightCache.clear();
                }
                highlightCache.set(key, code.innerHTML);
            } else {
                code.innerHTML = cached;
                code.classList.add('hljs');
            }
        }
    }

    // Line-ordered [data-line] elements; out-of-order ones (e.g. footnotes rendered at the end) are skipped.
    function buildSyncEntries() {
        const entries = [];
        let last = -1;
        for (const el of content.querySelectorAll('[data-line]')) {
            const line = Number(el.dataset.line);
            if (line >= last) {
                entries.push({ el, line });
                last = line;
            }
        }
        return entries;
    }

    function sync(line) {
        syncLine = line;
        scheduleSync();
    }

    function scheduleSync() {
        if (syncLine >= 0 && !syncFrame) {
            syncFrame = requestAnimationFrame(applySync);
        }
    }

    // Scrolls to the last block starting at or before syncLine, interpolating towards the next block.
    function applySync() {
        syncFrame = 0;
        if (syncLine < 0) {
            return;
        }
        syncEntries ??= buildSyncEntries();
        let lo = 0;
        let hi = syncEntries.length - 1;
        let index = -1;
        while (lo <= hi) {
            const mid = (lo + hi) >> 1;
            if (syncEntries[mid].line <= syncLine) {
                index = mid;
                lo = mid + 1;
            } else {
                hi = mid - 1;
            }
        }
        const topOf = el => el.getBoundingClientRect().top + window.scrollY;
        const current = index >= 0 ? syncEntries[index] : null;
        const next = syncEntries[index + 1];
        const fromLine = current ? current.line : 0;
        let y = current ? topOf(current.el) : 0;
        if (next) {
            y += (topOf(next.el) - y) * (syncLine - fromLine) / (next.line - fromLine);
        }
        window.scrollTo(0, y);
    }

    function find(term, backwards, restart) {
        const selection = window.getSelection();
        if (restart && selection.rangeCount) {
            selection.collapseToStart();
        }
        return window.find(term, false, backwards, true, false, false, false);
    }

    function onLinkClick(e) {
        if (e.button > 1) {
            return;
        }
        const link = e.target.closest('a[href]');
        if (!link) {
            return;
        }
        e.preventDefault();
        const href = link.getAttribute('href');
        if (href.startsWith('#')) {
            const id = decodeURIComponent(href.slice(1));
            (document.getElementById(id) ?? document.getElementsByName(id)[0])?.scrollIntoView();
        } else {
            post({ type: 'link', href });
        }
    }

    window.markview = { setTheme, setFontSize, setBase, setContent, sync, find };

    setTheme(new URLSearchParams(window.location.search).get('theme') === 'dark');

    document.addEventListener('click', onLinkClick);
    document.addEventListener('auxclick', onLinkClick);
    window.addEventListener('wheel', e => {
        if (e.ctrlKey) {
            e.preventDefault();
            post({ type: 'zoom', delta: e.deltaY < 0 ? 1 : -1 });
        }
    }, { passive: false });
    window.addEventListener('resize', scheduleSync);

    document.addEventListener('DOMContentLoaded', () => {
        content = document.getElementById('content');
        // Late image loads move the blocks below them.
        content.addEventListener('load', scheduleSync, true);
        post({ type: 'ready' });
    });
})();
