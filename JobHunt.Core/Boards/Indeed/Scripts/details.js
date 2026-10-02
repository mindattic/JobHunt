// Reads the job-details pane (right side of a results page) or a /viewjob page. Returns JSON:
//   { state: details|loading|challenge|signedOut, id, title, company, location, facts[], insights[], description, apply }
// apply: "easy" (Indeed Apply), "external" (apply on company site), "applied", or "none".
// Indeed renders this pane with React Native for Web — hashed r-xxxx/css-xxxx classes, no
// #jobDescriptionText — so every field is found by data-testid (read live 2026-09-30).
(function () {
  function clean(t) { return (t || '').replace(/[ \t ]+/g, ' ').replace(/\s*\n\s*/g, '\n').trim(); }
  function oneLine(t) { return (t || '').replace(/\s+/g, ' ').trim(); }
  function tid(root, id) { return root.querySelector('[data-testid="' + id + '"]'); }
  // The visible text pieces under el, one per text node — layout-free, so it reads the same in a
  // live pane and a test DOM. Skips aria-hidden subtrees (Indeed's duplicate compact header and
  // its " - " separators) and anything matching `skip`.
  function pieces(el, skip) {
    if (!el) return [];
    var out = [];
    var walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    for (var n = walker.nextNode(); n; n = walker.nextNode()) {
      var t = oneLine(n.textContent);
      if (!t) continue;
      var parent = n.parentElement;
      if (parent.closest('[aria-hidden="true"]')) continue;
      if (skip && parent.closest(skip)) continue;
      out.push(t);
    }
    return out;
  }
  function isRating(t) { return /^\d(?:\.\d)?$/.test(t); }
  function blockText(el) {
    if (!el) return '';
    if (typeof el.innerText === 'string' && el.innerText.trim()) return clean(el.innerText);
    var out = [];
    (function walk(node) {
      node.childNodes.forEach(function (c) {
        if (c.nodeType === 3) out.push(c.textContent);
        else if (c.nodeType === 1) {
          var block = /^(P|DIV|LI|UL|OL|BR|H[1-6]|SECTION|TR)$/.test(c.tagName);
          if (block) out.push('\n');
          if (c.tagName === 'LI') out.push('• ');
          walk(c);
          if (block) out.push('\n');
        }
      });
    })(el);
    return clean(out.join(''));
  }

  if (/challenge|captcha|checkpoint|blocked/i.test(location.pathname) ||
      document.querySelector('iframe[src*="hcaptcha.com"], iframe[src*="recaptcha"], iframe[src*="challenges.cloudflare.com"], #cf-challenge-running')) {
    return JSON.stringify({ state: 'challenge' });
  }
  if (/secure\.indeed\.com\/auth|\/account\/login/i.test(location.href)) return JSON.stringify({ state: 'signedOut' });

  var root = document.querySelector('#jobsearch-ViewjobPaneWrapper') || tid(document, 'viewjob-main-content') || document;

  // The job the PANE shows, from the pane itself: its company link carries fromjk=. Right after a
  // card click the highlighted card already names the new job while the pane still shows the old
  // one, so neither the card nor the URL is trusted for this. Only a /viewjob page (one job, no
  // pane) falls back to its own ?jk=.
  function jkIn(href) { var m = (href || '').match(/[?&](?:from)?jk=([a-f0-9]{8,})/i); return m ? m[1] : ''; }
  var fromLink = root.querySelector('a[href*="fromjk="]');
  var id = jkIn(fromLink && fromLink.getAttribute('href'))
    || (/\/viewjob/.test(location.pathname) ? jkIn(location.search) : '');

  var title = oneLine((tid(root, 'vj-job-title') || tid(root, 'jobsearch-JobInfoHeader-title') || {}).textContent);

  // "Thyme Care · 3.4 Remote": the company is the /cmp/ link; the location is the rest, minus
  // the separator and the star rating.
  var meta = tid(root, 'company-info-metadata');
  var companyLink = (meta || root).querySelector('a[href*="/cmp/"]');
  var company = oneLine(companyLink ? companyLink.textContent : '');
  var location_ = pieces(meta, 'a[href*="/cmp/"]').filter(function (t) { return t !== '·' && !isRating(t); }).join(', ');

  // The header's own line under the title and metadata — pay and type — minus everything
  // already read above and the apply buttons.
  var facts = [];
  pieces(tid(root, 'desktop-job-header'),
    '[data-testid="company-info-title-row"], [data-testid="company-info-metadata"], [data-testid="job-header-actions"]')
    .forEach(function (t) { if (t !== '·' && !isRating(t) && facts.indexOf(t) < 0) facts.push(t); });
  if (location_ && facts.indexOf(location_) < 0) facts.unshift(location_);

  // "Job details": salary, type, schedule, workplace — one pill per text piece.
  var insights = [];
  pieces(tid(root, 'structured-job-summary'), 'h1, h2, h3, h4, [role="heading"]').forEach(function (t) {
    if (t.length < 120 && insights.indexOf(t) < 0) insights.push(t);
  });

  // The description is everything after the "Full job description" heading.
  var description = '';
  var heading = tid(root, 'vj-job-description-heading');
  if (heading) {
    var parts = [];
    for (var n = heading.nextElementSibling; n; n = n.nextElementSibling) parts.push(blockText(n));
    description = clean(parts.join('\n'));
    if (!description && heading.parentElement) {
      description = blockText(heading.parentElement).replace(/^full job description\s*/i, '');
    }
  }
  if (!description) description = blockText(document.querySelector('#jobDescriptionText'));

  var apply = 'none';
  var actions = tid(root, 'job-header-actions') || root;
  var actionText = oneLine(actions.textContent);
  if (/\bapplied\b/i.test(actionText) && !/apply now/i.test(actionText)) apply = 'applied';
  else if (tid(root, 'viewjob-indeed-apply')) apply = 'easy';
  else if (/apply on company site|apply now/i.test(actionText)) apply = 'external';

  var ready = !!(title && description);
  return JSON.stringify({
    state: ready ? 'details' : 'loading',
    id: id, title: title, company: company, location: location_, facts: facts, insights: insights, description: description, apply: apply,
  });
})()
