// Reads the job-details pane (search page, right side) or a /jobs/view/ page. Returns JSON:
//   { state: details|loading|challenge|signedOut, id, title, company, location, facts[], insights[], description, apply }
// apply: "easy" (Easy Apply button), "external" (Apply on company site), "applied", or "none".
(function () {
  function clean(t) { return (t || '').replace(/[ \t\u00a0]+/g, ' ').replace(/\s*\n\s*/g, '\n').trim(); }
  function oneLine(t) { return (t || '').replace(/\s+/g, ' ').trim(); }
  function undouble(t) {
    t = oneLine(t);
    var h = (t.length - 1) / 2;
    if (t.length > 2 && t.length % 2 === 1 && t.charAt(h) === ' ' && t.slice(0, h) === t.slice(h + 1)) return t.slice(0, h);
    return t;
  }
  function first(root, selectors) {
    for (var i = 0; i < selectors.length; i++) { var el = root.querySelector(selectors[i]); if (el) return el; }
    return null;
  }
  function all(root, selectors) {
    var out = [];
    for (var i = 0; i < selectors.length; i++) {
      var found = root.querySelectorAll(selectors[i]);
      for (var j = 0; j < found.length; j++) if (out.indexOf(found[j]) < 0) out.push(found[j]);
    }
    return out;
  }
  // Block-aware text: innerText where the browser lays out (keeps paragraphs and bullets), a
  // newline-per-block textContent walk where it doesn't (test DOMs).
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

  var url = location.href;
  if (/\/checkpoint\/|\/challenge|captcha/i.test(url) || document.querySelector('iframe[src*="captcha"], #captcha-internal')) {
    return JSON.stringify({ state: 'challenge' });
  }
  if (/\/authwall|\/login/i.test(url)) return JSON.stringify({ state: 'signedOut' });

  // ── The 2026 layout (read live 2026-09-30) ─────────────────────────────────────────────────
  // [data-view-name="job-detail-page"] with hashed classes; read by data-view-name, link shape
  // (/jobs/view/<id>, /company/), and visible text pieces (aria-hidden subtrees skipped).
  var page = document.querySelector('[data-view-name="job-detail-page"]');
  if (page) {
    var pieces = function (el) {
      var out = [];
      if (!el) return out;
      var w = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
      for (var t = w.nextNode(); t; t = w.nextNode()) {
        var s = oneLine(t.textContent);
        if (s && !t.parentElement.closest('[aria-hidden="true"]')) out.push(s);
      }
      return out;
    };
    var titleLink = page.querySelector('a[href*="/jobs/view/"]');
    var m = (titleLink && titleLink.getAttribute('href') || '').match(/\/jobs\/view\/(?:[^\/?#]*-)?(\d{6,})/);
    var aboutSlot = page.querySelector('[id^="JobDetails_AboutTheJob_"]');
    var jobId = m ? m[1] : (aboutSlot ? aboutSlot.id.replace('JobDetails_AboutTheJob_', '') : '');

    // The top card: the nearest ancestor of the title that also holds the apply/save buttons.
    var top = titleLink;
    for (var up = 0; top && top !== page && up < 15 && !top.querySelector('[data-view-name="job-apply-button"], [data-view-name="job-save-button"]'); up++) {
      top = top.parentElement;
    }
    top = top || page;

    var companyEl = top.querySelector('[aria-label^="Company, "]');
    var companyName = companyEl ? companyEl.getAttribute('aria-label').replace(/^Company,\s*/, '').replace(/\.$/, '')
      : oneLine((top.querySelector('a[href*="/company/"]') || {}).textContent);

    // "United States · Reposted 6 days ago · Over 100 people clicked apply", then
    // "Promoted by hirer · Responses managed off LinkedIn".
    var factList = [];
    [].forEach.call(top.querySelectorAll('p'), function (p) {
      if (titleLink && p.contains(titleLink)) return;
      if (p.querySelector('a[href*="/company/"]')) return;
      pieces(p).join(' ').split(/\s*·\s*/).forEach(function (s) { s = s.trim(); if (s && factList.indexOf(s) < 0) factList.push(s); });
    });

    // Pills ("Remote", "Full-time", pay) link back into search; they carry no aria-label.
    var insightList = [];
    [].forEach.call(top.querySelectorAll('a[href*="/jobs/search-results/"]:not([aria-label]), a[href*="/jobs/search/"]:not([aria-label])'), function (a) {
      var s = pieces(a).join(' ');
      if (s && s.length < 120 && insightList.indexOf(s) < 0) insightList.push(s);
    });

    var loc = factList.filter(function (f) {
      return f !== companyName && !/\bago\b|applicants?|clicked apply|^promoted|^reposted|responses? managed/i.test(f);
    })[0] || '';
    var body = blockText(page.querySelector('[id^="JobDetails_AboutTheJob_"] [data-testid="expandable-text-box"]')
      || (aboutSlot ? aboutSlot : null));
    body = body.replace(/^about the job\s*/i, '');

    var applyBtn = page.querySelector('[data-view-name="job-apply-button"]');
    var applyLabel = applyBtn ? oneLine(applyBtn.textContent) + ' ' + (applyBtn.getAttribute('aria-label') || '') : '';
    var applyKind = 'none';
    if (pieces(top).some(function (s) { return /^applied\b/i.test(s); }) || /application submitted/i.test(pieces(top).join(' '))) applyKind = 'applied';
    else if (/easy apply/i.test(applyLabel)) applyKind = 'easy';
    else if (/apply/i.test(applyLabel)) applyKind = 'external';

    var heading = oneLine(titleLink ? titleLink.textContent : '');
    return JSON.stringify({
      state: heading && body ? 'details' : 'loading',
      id: jobId, title: heading, company: companyName, location: loc, facts: factList, insights: insightList, description: body, apply: applyKind,
    });
  }

  // ── The previous layout ────────────────────────────────────────────────────────────────────
  var root = first(document, ['.jobs-search__job-details--container', '.jobs-search__job-details', '.job-view-layout', '.jobs-details', '.details-pane__content', 'main']) || document;

  // The job the PANE shows — not the URL's currentJobId, which changes the instant a card is
  // clicked while the pane is still showing the previous job. Only a /jobs/view/ page (one job,
  // no pane) takes its id from the URL.
  function idFromHref(h) { var m = (h || '').match(/\/jobs\/view\/(?:[^\/?#]*-)?(\d{6,})/); return m ? m[1] : ''; }
  var titleLink = first(root, ['.job-details-jobs-unified-top-card__job-title a[href*="/jobs/view/"]', '.jobs-unified-top-card__job-title a[href*="/jobs/view/"]',
                               'h1 a[href*="/jobs/view/"]', '.top-card-layout__title a[href*="/jobs/view/"]']);
  var holder = first(root, ['.jobs-apply-button[data-job-id]', '[data-job-id]']);
  var id = idFromHref(titleLink && titleLink.getAttribute('href')) || (holder ? holder.getAttribute('data-job-id') : '')
    || (/\/jobs\/view\//.test(location.pathname) ? idFromHref(location.pathname) : '');

  var title = undouble((first(root, ['.job-details-jobs-unified-top-card__job-title', '.jobs-unified-top-card__job-title', '.top-card-layout__title', 'h1.t-24', 'h1', 'h2.t-24']) || {}).textContent);
  var company = undouble((first(root, ['.job-details-jobs-unified-top-card__company-name', '.jobs-unified-top-card__company-name', '.topcard__org-name-link', '.top-card-layout__second-subline a', '.jobs-unified-top-card__subtitle-primary-grouping a']) || {}).textContent);

  var facts = [];
  all(root, ['.job-details-jobs-unified-top-card__primary-description-container', '.job-details-jobs-unified-top-card__tertiary-description-container',
             '.job-details-jobs-unified-top-card__primary-description', '.jobs-unified-top-card__primary-description', '.jobs-unified-top-card__subtitle-primary-grouping',
             '.topcard__flavor-row', '.top-card-layout__second-subline']).forEach(function (el) {
    oneLine(el.textContent).split(/\s+·\s+|\s*·\s*/).forEach(function (part) { part = part.trim(); if (part && facts.indexOf(part) < 0) facts.push(part); });
  });

  var insights = [];
  all(root, ['.job-details-preferences-and-skills__pill', '.job-details-fit-level-preferences button', '.job-details-jobs-unified-top-card__job-insight',
             '.jobs-unified-top-card__job-insight', '.job-details-jobs-unified-top-card__workplace-type', '.ui-label', '.description__job-criteria-text',
             '.compensation__salary']).forEach(function (el) {
    oneLine(el.textContent).split(/\s+·\s+/).forEach(function (part) {
      part = part.replace(/^(matches your job preferences?,?|workplace type is|job type is)\s*/i, '').trim();
      if (part && part.length < 120 && insights.indexOf(part) < 0) insights.push(part);
    });
  });

  // The location is the first fact that isn't the company (older top cards lead with it), a date,
  // or an applicant count.
  var location_ = facts.filter(function (f) {
    return f !== company && !/\bago\b|applicants?|clicked apply|^promoted|^reposted|responses? managed/i.test(f);
  })[0] || oneLine((first(root, ['.topcard__flavor--bullet', '.job-details-jobs-unified-top-card__bullet']) || {}).textContent);
  var description = blockText(first(root, ['#job-details', '.jobs-description__content .jobs-box__html-content', '.jobs-description-content__text', '.jobs-box__html-content', '.jobs-description__content', '.show-more-less-html__markup', '.description__text']));

  // "Applied" is looked for in the top card and apply area only — a description that says "see
  // application instructions" or "candidates who applied before" must not mark the job applied.
  var apply = 'none';
  var descEl = first(root, ['#job-details', '.jobs-description__container', '.jobs-description__content', '.jobs-description-content__text', '.show-more-less-html__markup', '.description__text']);
  var top = root.cloneNode(true);
  if (descEl && descEl.id) { var d2 = top.querySelector('#' + descEl.id); if (d2) d2.remove(); }
  ['#job-details', '.jobs-description__container', '.jobs-description__content', '.jobs-description-content__text', '.show-more-less-html__markup', '.description__text']
    .forEach(function (sel) { var n = top.querySelectorAll(sel); for (var k = 0; k < n.length; k++) n[k].remove(); });
  var body = oneLine(top.textContent);
  var button = first(root, ['button.jobs-apply-button', '.jobs-apply-button--top-card button', '.jobs-s-apply button', 'a.jobs-apply-button', '.apply-button']);
  var buttonText = button ? oneLine(button.textContent) + ' ' + (button.getAttribute('aria-label') || '') : '';
  if (/\bapplied\s+\d+\s+\w+\s+ago\b|application submitted|see application/i.test(body) || root.querySelector('.artdeco-inline-feedback--success .jobs-s-apply__application-link, .post-apply-timeline')) apply = 'applied';
  else if (/easy apply/i.test(buttonText)) apply = 'easy';
  else if (/apply/i.test(buttonText)) apply = 'external';

  var ready = !!(title && description);
  return JSON.stringify({
    state: ready ? 'details' : 'loading',
    id: id, title: title, company: company, location: location_, facts: facts, insights: insights, description: description, apply: apply,
  });
})()
