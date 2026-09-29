// Collects the page's Bijoy font text (Milestone 9b). Run on demand with ExecuteScriptAsync when
// the person asks to convert. Returns the texts as a JSON string; the text nodes themselves are kept
// so that bijoy-apply can put the converted text back in the same places. Nothing is sent anywhere.
(() => {
  const BIJOY_FONT = /sutonny|bijoy|[A-Za-z]MJ\b/i;
  const SKIP = new Set(['SCRIPT', 'STYLE', 'NOSCRIPT', 'TEXTAREA', 'TEMPLATE']);
  const MAX_NODES = 20000;
  const MAX_CHARS = 2000000;

  const nodes = [];
  const texts = [];
  let total = 0;
  const verdicts = new WeakMap();
  const walker = document.createTreeWalker(document.body || document.documentElement, NodeFilter.SHOW_TEXT);
  for (let node = walker.nextNode(); node && nodes.length < MAX_NODES && total < MAX_CHARS; node = walker.nextNode()) {
    const parent = node.parentElement;
    if (!parent || SKIP.has(parent.tagName.toUpperCase()) || node.data.trim().length === 0) { continue; }
    let isBijoy = verdicts.get(parent);
    if (isBijoy === undefined) {
      isBijoy = BIJOY_FONT.test(getComputedStyle(parent).fontFamily);
      verdicts.set(parent, isBijoy);
    }
    if (!isBijoy) { continue; }
    nodes.push(node);
    texts.push(node.data);
    total += node.data.length;
  }
  Object.defineProperty(window, '__obhijatriBijoy', { value: nodes, configurable: true, writable: true });
  return JSON.stringify(texts);
})();
