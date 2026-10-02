// Reads an Indeed job-search results page (or the signed-in home feed). Returns JSON:
//   { state: results|noResults|signedOut|challenge|loading, cards: [{ id, title, company, location, quickApply, applied, promoted, salary }] }
// Selectors read from Indeed's live markup through tools/inspect-board.mjs (2026-09-30): a card is
// div.cardOutline (also .result, plus a job_<jk> class); the job key lives on the inner title link
// a.jcs-JobTitle[data-jk], not on the card; company/location/salary carry data-testid hooks. The
// css-xxxx classes are build hashes and are never used.
(function () {
  function clean(t) { return (t || '').replace(/\s+/g, ' ').trim(); }
  function first(root, selectors) {
    for (var i = 0; i < selectors.length; i++) {
      var el = root.querySelector(selectors[i]);
      if (el) return el;
    }
    return null;
  }
  function text(root, selectors) {
    var el = first(root, selectors);
    return el ? clean(el.textContent) : '';
  }
  function jkOf(card) {
    var link = card.querySelector('a.jcs-JobTitle[data-jk], a[data-jk]');
    if (link) return link.getAttribute('data-jk');
    var m = (card.className || '').toString().match(/\bjob_([a-f0-9]{8,})\b/i);
    return m ? m[1] : null;
  }
  function titleOf(card) {
    var span = first(card, ['h2.jobTitle span[title]', 'h3.jobTitle span[title]', 'a.jcs-JobTitle span[title]']);
    if (span) return clean(span.getAttribute('title'));
    var link = first(card, ['a.jcs-JobTitle']);
    var aria = link && link.getAttribute('aria-label');
    if (aria) return clean(aria.replace(/^full details of\s+/i, ''));
    return text(card, ['.jobTitle', 'a.jcs-JobTitle']);
  }

  var url = location.href;
  if (/challenge|captcha|checkpoint|blocked/i.test(location.pathname) ||
      document.querySelector('iframe[src*="hcaptcha.com"], iframe[src*="recaptcha"], iframe[src*="challenges.cloudflare.com"], #cf-challenge-running')) {
    return JSON.stringify({ state: 'challenge', cards: [] });
  }
  if (/secure\.indeed\.com\/auth|\/account\/login/i.test(url)) return JSON.stringify({ state: 'signedOut', cards: [] });

  var candidates = document.querySelectorAll('div.cardOutline, div.result');
  var seen = {};
  var cards = [];
  for (var i = 0; i < candidates.length; i++) {
    var card = candidates[i];
    if (card.parentElement && card.parentElement.closest('div.cardOutline, div.result')) continue;
    var id = jkOf(card);
    if (!id || seen[id]) continue;
    seen[id] = true;
    // innerText keeps the line breaks between blocks; textContent runs "…Engineer" straight into
    // "Easily apply", so a word-boundary match on it never fires. (textContent is the fallback
    // for a test DOM that doesn't lay out.)
    var words = (typeof card.innerText === 'string' && card.innerText.trim()) ? card.innerText : card.textContent;
    cards.push({
      id: id,
      title: titleOf(card),
      company: text(card, ['[data-testid="company-name"]', '.companyName']),
      location: text(card, ['[data-testid="text-location"]', '.companyLocation']),
      salary: text(card, ['[data-testid*="salary-snippet-container"]', '.salary-snippet-container']),
      quickApply: /easily apply/i.test(words),
      applied: /\bapplied\b/i.test(text(card, ['[data-testid="applied-snippet"]', '[data-testid*="applied"]'])),
      // Not the sponTapItem class: Indeed puts it on every card in the feed. Only the visible label.
      promoted: /(^|\n)\s*sponsored\s*($|\n)/i.test(words) || !!card.querySelector('[data-testid*="sponsored" i]'),
    });
  }

  if (!cards.length) {
    var body = clean(document.body ? document.body.textContent : '');
    if (/did not match any jobs|no results found|no jobs found/i.test(body)) return JSON.stringify({ state: 'noResults', cards: [] });
    return JSON.stringify({ state: 'loading', cards: [] });
  }
  return JSON.stringify({ state: 'results', cards: cards });
})()
