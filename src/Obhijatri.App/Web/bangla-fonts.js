// Fix Bangla fonts (Milestone 9b). Added to a tab only while the setting is on. Bangla letters on
// the page are drawn in a good Bangla font that Windows ships; everything else (English text, icon
// fonts, sizes, colours) is left as the site made it. Talks to nothing.
(() => {
    if (window.__obhijatriBanglaFonts) {
        return;
    }
    Object.defineProperty(window, '__obhijatriBanglaFonts', { value: true });

    const FAMILY = 'ObhijatriBangla';
    // Bangla letters and signs, the joiners used inside conjuncts, and the danda.
    const RANGE = 'U+0980-09FF, U+200C-200D, U+0964-0965';
    const HAS_BANGLA = /[ঀ-৿]/;

    // The face only covers Bangla code points, so listing it first in a font stack never changes
    // how Latin text or icons are drawn.
    const faces = [
        ['normal', 'local("Nirmala UI"), local("Vrinda"), local("Noto Sans Bengali")'],
        ['700', 'local("Nirmala UI Bold"), local("Vrinda Bold"), local("Noto Sans Bengali Bold")'],
    ];
    for (const [weight, source] of faces) {
        try {
            const face = new FontFace(FAMILY, source, { weight, unicodeRange: RANGE });
            document.fonts.add(face);
            face.load().catch(() => { });
        } catch (error) {
            // A page that has replaced the font API: nothing to do.
        }
    }

    const done = new WeakSet();
    const pending = new Set();
    let scheduled = false;

    const fix = (element) => {
        if (done.has(element) || !(element instanceof HTMLElement)) {
            return;
        }
        done.add(element);
        const stack = getComputedStyle(element).fontFamily;
        if (!stack.includes(FAMILY)) {
            element.style.setProperty('font-family', '"' + FAMILY + '", ' + stack, 'important');
        }
    };

    const flush = () => {
        scheduled = false;
        let budget = 300;
        for (const element of pending) {
            pending.delete(element);
            fix(element);
            if (--budget === 0) {
                break;
            }
        }
        if (pending.size > 0) {
            schedule();
        }
    };

    const schedule = () => {
        if (!scheduled) {
            scheduled = true;
            setTimeout(flush, 60);
        }
    };

    const scan = (root) => {
        if (root.nodeType === 3) {
            if (HAS_BANGLA.test(root.data) && root.parentElement) {
                pending.add(root.parentElement);
            }
            return;
        }
        if (root.nodeType !== 1) {
            return;
        }
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        for (let node = walker.nextNode(); node; node = walker.nextNode()) {
            if (HAS_BANGLA.test(node.data) && node.parentElement) {
                pending.add(node.parentElement);
            }
        }
    };

    new MutationObserver((records) => {
        for (const record of records) {
            if (record.type === 'characterData') {
                scan(record.target);
            } else {
                for (const node of record.addedNodes) {
                    scan(node);
                }
            }
        }
        if (pending.size > 0) {
            schedule();
        }
    }).observe(document, { childList: true, subtree: true, characterData: true });
})();
