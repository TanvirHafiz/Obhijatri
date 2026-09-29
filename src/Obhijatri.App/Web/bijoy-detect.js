// Bijoy detection (Milestone 9b). Part of the bridge, so it can tell the app. Looks, once shortly
// after the page has loaded and once more a few seconds later, for text drawn in a Bijoy family
// font (SutonnyMJ and its relatives). Those fonts store Bangla as ASCII and Latin-1 codes, so the
// page is unreadable without the font and needs converting to Unicode. The scan is bounded: a few
// thousand text nodes, and one style lookup per distinct element.
if (window === window.top) {
  const BIJOY_FONT = /sutonny|bijoy|[A-Za-z]MJ\b/i;
  const MAX_NODES = 3000;
  const MAX_ELEMENTS = 300;
  let reported = false;

  const scan = () => {
    if (reported || !document.body) { return; }
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    const seen = new WeakSet();
    let nodes = 0;
    let elements = 0;
    for (let node = walker.nextNode(); node && nodes < MAX_NODES && elements < MAX_ELEMENTS; node = walker.nextNode()) {
      nodes++;
      const parent = node.parentElement;
      if (!parent || seen.has(parent) || node.data.trim().length < 4) { continue; }
      seen.add(parent);
      elements++;
      if (BIJOY_FONT.test(getComputedStyle(parent).fontFamily)) {
        reported = true;
        send("BijoyDetected");
        return;
      }
    }
  };

  const later = () => {
    setTimeout(scan, 1200);
    setTimeout(scan, 4500);
  };
  if (document.readyState === "complete") { later(); } else { window.addEventListener("load", later, { once: true }); }
}
