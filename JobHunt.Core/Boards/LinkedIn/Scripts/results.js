// Reads a LinkedIn job-search results page. Returns JSON:
//   { state: results|noResults|signedOut|challenge|loading, cards: [{ id, title, company, location, quickApply, applied, promoted }] }
// Built to survive markup churn: every field tries several selectors LinkedIn has used (signed-in
// "job-card-container" cards and the signed-out "base-card" list), ids come from data attributes or
// the /jobs/view/ link, and text is read with textContent so the same script runs in a test DOM.
(function () {
  function clean(t) { return (t || '').replace(/\s+/g, ' ').trim(); }
  function undouble(t) {
    t = clean(t);
    var h = (t.length - 1) / 2;
    if (t.length > 2 && t.length % 2 === 1 && t.charAt(h) === ' ' && t.slice(0, h) === t.slice(h + 1)) return t.slice(0, h);
    return t;
  }
  function first(root, selectors) {
    for (var i = 0; i < selectors.length; i++) {
      var el = root.querySelector(selectors[i]);
      if (el) return el;
    }
    return null;
  }
  function text(root, selectors) {
    var el = first(root, selectors);
    return el ? undouble(el.textContent) : '';
  }
  function idFromHref(href) {
    var m = (href || '').match(/\/jobs\/view\/(?:[^\/?#]*-)?(\d{6,})/) || (href || '').match(/[?&]currentJobId=(\d{6,})/);
    return m ? m[1] : null;
  }
  function idOf(n) {
    var v = n.getAttribute('data-occludable-job-id') || n.getAttribute('data-job-id');
    if (v && /^\d+$/.test(v)) return v;
    var urn = n.getAttribute('data-entity-urn') || '';
    var m = urn.match(/jobPosting:(\d+)/);
    if (m) return m[1];
    var inner = n.querySelector('[data-job-id], [data-occludable-job-id]');
    if (inner) {
      v = inner.getAttribute('data-job-id') || inner.getAttribute('data-occludable-job-id');
      if (v && /^\d+$/.test(v)) return v;
    }
    var a = n.querySelector('a[href*="/jobs/view/"], a[href*="currentJobId="]');
    return a ? idFromHref(a.getAttribute('href')) : null;
  }
  function titleOf(n) {
    var link = first(n, ['a.job-card-list__title--link', 'a.job-card-container__link', 'a.job-card-list__title', 'a.base-card__full-link', 'a[href*="/jobs/view/"]']);
    if (link && link.getAttribute('aria-label')) return clean(link.getAttribute('aria-label')).replace(/\s+with verification$/i, '');
    var strong = link && link.querySelector('strong, [aria-hidden="true"]');
    if (strong && clean(strong.textContent)) return undouble(strong.textContent);
    var t = text(n, ['.job-card-list__title', '.base-search-card__title', 'h3']);
    return t || (link ? undouble(link.textContent) : '');
  }

  var url = location.href;
  if (/\/checkpoint\/|\/challenge|captcha|\/uas\/login-submit/i.test(url) ||
      document.querySelector('iframe[src*="captcha"], #captcha-internal, form[action*="checkpoint"]')) {
    return JSON.stringify({ state: 'challenge', cards: [] });
  }
  if (/\/authwall|\/login|\/signup/i.test(url)) return JSON.stringify({ state: 'signedOut', cards: [] });

  var CARD = 'li[data-occludable-job-id], div[data-job-id], .job-card-container, li.jobs-search-results__list-item, ' +
    '.scaffold-layout__list-item, ul.jobs-search__results-list > li, .base-card[data-entity-urn*="jobPosting"], .job-search-card';
  var candidates = document.querySelectorAll(CARD);
  var seen = {};
  var cards = [];
  for (var i = 0; i < candidates.length; i++) {
    var n = candidates[i];
    // Card markup nests (li > div.job-card-container > div[data-job-id]); keep only the outermost.
    if (n.parentElement && n.parentElement.closest(CARD)) continue;
    var id = idOf(n);
    if (!id || seen[id]) continue;
    var title = titleOf(n);
    // Off-screen occludable cards are empty shells until scrolled into view; report them anyway
    // so the caller knows to scroll, but with no title.
    seen[id] = true;
    var body = clean(n.textContent);
    cards.push({
      id: id,
      title: title,
      company: text(n, ['.artdeco-entity-lockup__subtitle', '.job-card-container__primary-description', '.job-card-container__company-name', '.base-search-card__subtitle', 'h4']),
      location: text(n, ['.job-card-container__metadata-wrapper li', '.job-card-container__metadata-item', '.artdeco-entity-lockup__caption', '.job-search-card__location']),
      quickApply: /\beasy apply\b/i.test(body),
      applied: /(^|\s)applied(\s|$)/i.test(clean(text(n, ['.job-card-container__footer-wrapper', '.job-card-list__footer-wrapper', 'ul.job-card-list__footer-wrapper', '.job-card-container__footer-job-state']))),
      promoted: /\bpromoted\b/i.test(body),
    });
  }

  if (!cards.length) {
    var noResults = document.querySelector('.jobs-search-no-results-banner, .jobs-search-results-list__no-results, .jobs-search-two-pane__no-results-banner--expand') ||
      /no matching jobs found|we couldn.t find a match/i.test(clean(document.body ? document.body.textContent : ''));
    if (noResults) return JSON.stringify({ state: 'noResults', cards: [] });
    return JSON.stringify({ state: 'loading', cards: [] });
  }
  return JSON.stringify({ state: 'results', cards: cards });
})()
