// Scam check signals (Milestone 10). Run on demand with ExecuteScriptAsync when the person presses
// "এটা কি প্রতারণা?". Returns JSON: the title, the visible text (capped), what each form asks for and
// where it sends the answers, and whether there is a countdown. The app treats the result as
// untrusted (PageSignals.Parse). Nothing is sent anywhere from here.
(() => {
    const OTP = /(otp|one[-_ ]?time|passcode|verification[-_ ]?code|\bpin\b|ওটিপি|পিন)/i;
    const CARD = /(card[-_ ]?(number|no)|cc[-_ ]?num|cvv|cvc|expir|কার্ড)/i;
    const PHONE = /(phone|mobile|msisdn|contact|মোবাইল|ফোন)/i;
    const ID = /(\bnid\b|national[-_ ]?id|passport|birth[-_ ]?cert|জাতীয়|এনআইডি|পাসপোর্ট)/i;
    const IGNORED = new Set(['hidden', 'submit', 'button', 'checkbox', 'radio', 'image', 'reset', 'file']);

    const visible = (el) => !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
    const describe = (el) => [el.name, el.id, el.placeholder, el.getAttribute('aria-label'), el.getAttribute('autocomplete')]
        .filter(Boolean).join(' ');

    const inspect = (root) => {
        const found = { password: false, otp: false, card: false, phone: false, id: false };
        for (const el of root.querySelectorAll('input, textarea')) {
            const type = (el.type || 'text').toLowerCase();
            if (IGNORED.has(type) || !visible(el)) { continue; }
            const label = describe(el);
            const auto = (el.getAttribute('autocomplete') || '').toLowerCase();
            if (type === 'password') { found.password = true; }
            if (auto === 'one-time-code' || OTP.test(label)) { found.otp = true; }
            if (auto.startsWith('cc-') || CARD.test(label)) { found.card = true; }
            if (type === 'tel' || PHONE.test(label)) { found.phone = true; }
            if (ID.test(label)) { found.id = true; }
        }
        return found;
    };

    const asks = (f) => f.password || f.otp || f.card || f.phone || f.id;

    const forms = [];
    for (const form of document.forms) {
        const found = inspect(form);
        if (!asks(found)) { continue; }
        let actionHost = null;
        try {
            const host = new URL(form.getAttribute('action') || '', location.href).hostname;
            if (host && host !== location.hostname) { actionHost = host; }
        } catch (error) { /* an action that is not an address */ }
        forms.push({ actionHost, ...found });
        if (forms.length >= 20) { break; }
    }
    // Pages built without <form> (most single page apps): look at the whole page once.
    if (forms.length === 0 && document.body) {
        const found = inspect(document.body);
        if (asks(found)) { forms.push({ actionHost: null, ...found }); }
    }

    let countdown = false;
    const TIMER_NAME = /(count(down)?|timer|clock|deadline)/i;
    let scanned = 0;
    for (const el of document.querySelectorAll('[id], [class]')) {
        if (++scanned > 2000) { break; }
        const name = (el.id || '') + ' ' + (typeof el.className === 'string' ? el.className : '');
        if (TIMER_NAME.test(name) && /\d{1,2}\s*:\s*\d{2}/.test(el.textContent || '') && (el.textContent || '').length < 60 && visible(el)) {
            countdown = true;
            break;
        }
    }

    // A link that starts a private chat with a number or channel (not a share button).
    let contactLink = false;
    const CONTACT = /(wa\.me\/\+?\d{8,}|whatsapp\.com\/send\/?\?(?=[^#]*phone=)|(^|\/\/)t\.me\/(?!share)[A-Za-z0-9_+]{3,})/i;
    for (const a of document.querySelectorAll('a[href]')) {
        if (CONTACT.test(a.getAttribute('href') || '')) { contactLink = true; break; }
    }

    const text = document.body ? document.body.innerText.slice(0, 20000) : '';
    return JSON.stringify({ title: (document.title || '').slice(0, 300), text, forms, countdown, contactLink });
})();
