// Low data mode (Milestone 8). Added to a tab only while the mode is on, and removed when it is
// switched off. Nothing here talks to the app.
(() => {
    if (window.__obhijatriLowData) {
        return;
    }
    Object.defineProperty(window, '__obhijatriLowData', { value: true });

    // Video and audio start only after the person clicked or pressed a key in the page. A script
    // that calls play() without that is refused, the same answer a browser gives for autoplay.
    const userActive = () => !navigator.userActivation || navigator.userActivation.isActive;
    const originalPlay = HTMLMediaElement.prototype.play;
    HTMLMediaElement.prototype.play = function () {
        if (!userActive()) {
            return Promise.reject(new DOMException('Autoplay is off in low data mode', 'NotAllowedError'));
        }
        return originalPlay.apply(this, arguments);
    };

    // Media that started by itself (the autoplay attribute) is stopped as soon as it plays.
    document.addEventListener('play', (event) => {
        const media = event.target;
        if (media instanceof HTMLMediaElement && !userActive()) {
            media.pause();
        }
    }, true);

    const tame = (element) => {
        if (element instanceof HTMLMediaElement) {
            element.autoplay = false;
            if (element.preload !== 'none') {
                element.preload = 'none';
            }
        } else if (element instanceof HTMLImageElement) {
            if (!element.hasAttribute('loading')) {
                element.loading = 'lazy';
            }
            if (!element.hasAttribute('decoding')) {
                element.decoding = 'async';
            }
        } else if (element instanceof HTMLIFrameElement) {
            if (!element.hasAttribute('loading')) {
                element.loading = 'lazy';
            }
        }
    };

    const tameTree = (root) => {
        tame(root);
        if (root.querySelectorAll) {
            for (const element of root.querySelectorAll('img, iframe, video, audio')) {
                tame(element);
            }
        }
    };

    // Watch from the very start of the document so images that the parser adds get the
    // attribute before they are fetched.
    new MutationObserver((records) => {
        for (const record of records) {
            for (const node of record.addedNodes) {
                if (node.nodeType === 1) {
                    tameTree(node);
                }
            }
        }
    }).observe(document, { childList: true, subtree: true });
})();
