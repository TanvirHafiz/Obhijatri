// Reader mode extraction (Milestone 9b). Run on demand with ExecuteScriptAsync; it returns a JSON
// string of plain text blocks ({t: h|p|li|q, x: text}). No HTML, images or scripts are returned. The
// app treats the result as untrusted (ReaderArticle.Parse).
(() => {
    const SKIP_TAGS = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'NAV', 'FOOTER', 'HEADER', 'ASIDE', 'FORM', 'IFRAME',
        'BUTTON', 'SVG', 'SELECT', 'INPUT', 'TEXTAREA', 'FIGURE', 'TEMPLATE']);
    const BAD_NAMES = /(comment|share|social|related|sidebar|advert|promo|newsletter|subscribe|breadcrumb|popup|modal|cookie|footer|widget)/i;
    const BLOCKS = 'p, h1, h2, h3, h4, li, blockquote, div';
    const NESTED = 'p, h1, h2, h3, h4, li, blockquote, div, ul, ol, table';

    const clean = (s) => (s || '').replace(/\s+/g, ' ').trim();
    const visible = (el) => !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);

    const unwanted = (el, root) => {
        for (let e = el; e && e !== root && e !== document.body; e = e.parentElement) {
            if (SKIP_TAGS.has(e.tagName.toUpperCase())) {
                return true;
            }
            const names = (e.id || '') + ' ' + (typeof e.className === 'string' ? e.className : '');
            if (BAD_NAMES.test(names)) {
                return true;
            }
        }
        return false;
    };

    const linkDensity = (el) => {
        const total = clean(el.textContent).length;
        if (total === 0) {
            return 1;
        }
        let links = 0;
        for (const a of el.querySelectorAll('a')) {
            links += clean(a.textContent).length;
        }
        return links / total;
    };

    // Find the element that holds most of the article's paragraphs.
    const scores = new Map();
    for (const p of document.querySelectorAll('p')) {
        const length = clean(p.textContent).length;
        if (length < 40 || !visible(p) || unwanted(p, document.body)) {
            continue;
        }
        const points = 1 + Math.min(length / 100, 3);
        let depth = 0;
        for (let e = p.parentElement; e && depth < 3; e = e.parentElement, depth++) {
            scores.set(e, (scores.get(e) || 0) + points / (depth === 0 ? 1 : depth === 1 ? 2 : 4));
        }
    }

    let root = null;
    let best = 0;
    for (const [element, score] of scores) {
        const adjusted = score * (1 - Math.min(linkDensity(element), 0.9));
        if (adjusted > best) {
            best = adjusted;
            root = element;
        }
    }
    if (!root) {
        root = document.querySelector('article, main, [role=main]') || document.body;
    }

    const blocks = [];
    let last = '';
    for (const el of root.querySelectorAll(BLOCKS)) {
        const tag = el.tagName.toUpperCase();
        if (unwanted(el, root) || !visible(el)) {
            continue;
        }
        if (tag === 'DIV' && (el.querySelector(NESTED) || clean(el.textContent).length < 60)) {
            continue;
        }
        if ((tag === 'LI' || tag === 'BLOCKQUOTE') && el.querySelector('p')) {
            continue;
        }

        const text = clean(el.textContent);
        const heading = /^H[1-4]$/.test(tag);
        if (text.length < (heading ? 3 : 25) || text === last) {
            continue;
        }
        if (linkDensity(el) > 0.6 && text.length < 200) {
            continue;
        }
        last = text;
        blocks.push({ t: heading ? 'h' : tag === 'LI' ? 'li' : tag === 'BLOCKQUOTE' ? 'q' : 'p', x: text });
        if (blocks.length >= 1500) {
            break;
        }
    }

    // The article's own heading is better than the first h1 on the page (often the site's name).
    const meta = document.querySelector('meta[property="og:title"]');
    const heading = root.querySelector('h1') || (root.closest && root.closest('article, main') && root.closest('article, main').querySelector('h1'))
        || document.querySelector('article h1') || document.querySelector('h1');
    const title = clean((meta && meta.content) || (heading && heading.textContent) || document.title);
    return JSON.stringify({ title, site: location.hostname.replace(/^www\./, ''), blocks });
})();
