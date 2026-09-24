// JobHunt panel. Talks to MainWindow over WebView2 postMessage: sends { action, ... },
// receives { type, ... }. Every value from the host is rendered with textContent — posting text is
// untrusted and never becomes HTML.
(function () {
  'use strict';

  var JH = window.JH;
  var el = JH.el;
  var state = {
    users: [], deleted: [], activeId: null,
    profile: null, savedProfile: null, passwordSaved: {}, boards: [],
    jobs: [], selected: new Set(), current: null, applications: [], settings: null, providers: [],
  };
  var $ = function (id) { return document.getElementById(id); };
  var post = JH.post = function (msg) { window.chrome.webview.postMessage(msg); };

  // ── tabs ──────────────────────────────────────────────────────────────────────────────────
  var tabs = Array.prototype.slice.call(document.querySelectorAll('[role=tab]'));
  function showTab(tab) {
    tabs.forEach(function (t) {
      var on = t === tab;
      t.setAttribute('aria-selected', on ? 'true' : 'false');
      t.tabIndex = on ? 0 : -1;
      $(t.getAttribute('aria-controls')).hidden = !on;
    });
    try { localStorage.setItem('jobhunt.tab', tab.id); } catch (e) { /* storage unavailable */ }
  }
  JH.showTab = function (id) { showTab($(id)); };
  tabs.forEach(function (t, i) {
    t.addEventListener('click', function () { showTab(t); });
    t.addEventListener('keydown', function (e) {
      if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft') return;
      var next = tabs[(i + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
      showTab(next); next.focus();
    });
  });
  try { var saved = localStorage.getItem('jobhunt.tab'); if (saved && $(saved)) showTab($(saved)); } catch (e) { /* */ }

  // ── applicant picker: Add / Edit / Delete ─────────────────────────────────────────────────
  function confirmDiscard() {
    var dirty = JH.applicantIsDirty() || JH.requirementsIsDirty();
    return !dirty || window.confirm('Discard your unsaved changes to this applicant or their job requirements?');
  }

  function renderUsers() {
    var picker = $('user-picker');
    picker.textContent = '';
    if (!state.users.length) picker.appendChild(el('option', { value: '', text: 'No applicants yet' }));
    state.users.forEach(function (u) {
      var o = el('option', { value: String(u.id), text: u.displayName });
      if (u.id === state.activeId) o.selected = true;
      picker.appendChild(o);
    });
    if (state.profile && !state.profile.id) {
      var draft = el('option', { value: 'new', text: 'New applicant…' });
      draft.selected = true;
      picker.appendChild(draft);
    }
    // While a "New applicant…" draft is showing, Edit/Delete would act on someone else — off.
    var draft = !!(state.profile && !state.profile.id);
    $('user-edit').disabled = !state.activeId || draft;
    $('user-delete').disabled = !state.activeId || draft || !!state.hunting;
    $('export-profile').disabled = !state.activeId;   // nothing to export until an applicant is saved

    var list = $('deleted-users');
    list.textContent = '';
    if (!state.deleted.length) list.appendChild(el('li', { class: 'muted', text: 'None.' }));
    state.deleted.forEach(function (u) {
      list.appendChild(el('li', {}, [el('span', { text: u.displayName + ' ' }),
        JH.button('Restore', 'Restore ' + u.displayName, function () { post({ action: 'restoreUser', id: u.id }); })]));
    });
  }

  $('user-picker').addEventListener('change', function (e) {
    var id = parseInt(e.target.value, 10);
    if (!id) return;
    if (id === state.activeId) {
      // Back from an unsaved "New applicant…" draft to the applicant who is still active.
      if (state.profile !== state.savedProfile && confirmDiscard()) {
        state.profile = state.savedProfile;
        JH.renderApplicant(state.savedProfile, state.passwordSaved, state.boards);
      }
      renderUsers();
      return;
    }
    if (!confirmDiscard()) { renderUsers(); return; }
    post({ action: 'switchUser', id: id });
  });
  $('user-add').addEventListener('click', function () {
    if (!confirmDiscard()) return;
    state.profile = JH.blankProfile();
    JH.renderApplicant(state.profile, {}, state.boards);
    renderUsers();
    JH.showTab('tab-applicant');
    var first = document.querySelector('#applicant-sections input');
    if (first) first.focus();
  });
  $('user-edit').addEventListener('click', function () {
    JH.showTab('tab-applicant');
    var first = document.querySelector('#applicant-sections input');
    if (first) first.focus();
  });
  $('user-delete').addEventListener('click', function () {
    if (state.activeId) post({ action: 'deleteUser', id: state.activeId });
  });

  $('applicant-save').addEventListener('click', function () {
    var p = JH.collectApplicant();
    var c = p.contact || {};
    // 3.3.1 Error Identification: mark the field, move focus to it, and say what's wrong.
    var first = document.querySelector('#sec-contact input');
    if (!(c.firstName || c.lastName || c.email)) {
      $('sec-contact').open = true;
      if (first) { first.setAttribute('aria-invalid', 'true'); first.focus(); }
      JH.toast('Give the applicant at least a first name, last name or email.', true);
      return;
    }
    if (first) first.removeAttribute('aria-invalid');
    post({ action: 'saveProfile', profile: p });
  });
  $('applicant-discard').addEventListener('click', function () {
    if (!JH.applicantIsDirty() || window.confirm('Discard your unsaved changes?')) {
      JH.renderApplicant(state.profile && state.profile.id ? state.profile : null, state.passwordSaved, state.boards);
    }
  });
  $('import-profile').addEventListener('click', function () { post({ action: 'importProfile' }); });
  $('export-profile').addEventListener('click', function () { post({ action: 'exportProfile' }); });

  // ── job requirements ──────────────────────────────────────────────────────────────────────
  $('requirements-save').addEventListener('click', function () {
    var s = JH.collectRequirements();
    if (s) post({ action: 'saveSearch', search: s });
  });
  $('requirements-hunt').addEventListener('click', function () {
    var btn = $('requirements-hunt');
    if (state.hunting) { post({ action: 'stopHunt' }); return; }
    var s = JH.collectRequirements();
    if (!s) return;
    post({ action: 'saveAndHunt', search: s });
    // The button becomes "Stop hunt" almost at once; a double click must not stop what it started.
    btn.disabled = true;
    setTimeout(function () { btn.disabled = false; }, 1500);
  });
  $('requirements-preview-btn').addEventListener('click', function () {
    var s = JH.collectRequirements();
    if (s) post({ action: 'previewSearch', search: s });
  });

  // ── best fits ─────────────────────────────────────────────────────────────────────────────
  function scoreClass(s) { return s == null ? 'low' : s >= 80 ? 'high' : s >= 60 ? 'mid' : 'low'; }
  function label(v) { return (v || '').replace(/([a-z])([A-Z])/g, '$1 $2'); }

  function renderJobs() {
    var list = $('job-list');
    list.textContent = '';
    $('jobs-empty').hidden = state.jobs.length > 0;
    $('job-detail').hidden = !state.jobs.some(function (j) { return j.id === state.current; });
    $('jobs-count').textContent = state.jobs.length ? state.selected.size + ' of ' + state.jobs.length + ' selected' : '';
    $('select-all').checked = state.jobs.length > 0 && state.selected.size === state.jobs.length;

    state.jobs.forEach(function (j) {
      var box = el('input', { type: 'checkbox', 'aria-label': 'Select ' + j.title + ' at ' + j.company });
      box.checked = state.selected.has(j.id);
      box.addEventListener('click', function (e) { e.stopPropagation(); toggle(j.id, box.checked); });

      var chips = [label(j.workplace), label(j.employmentType), j.duration, j.salary]
        .filter(function (c) { return c && c !== 'Unknown'; })
        .map(function (c) { return el('span', { class: 'chip', text: c }); });

      // The title is a real button (keyboard, screen readers); the row is just its container.
      var open = el('button', { type: 'button', class: 'job-title', text: j.title,
        'aria-expanded': state.current === j.id ? 'true' : 'false', 'aria-controls': 'job-detail' });
      open.addEventListener('click', function () { showDetail(j.id); });
      var row = el('li', { class: 'job', 'aria-current': state.current === j.id ? 'true' : undefined }, [
        box,
        el('div', { class: 'score ' + scoreClass(j.score) }, [
          el('span', { 'aria-hidden': 'true', text: j.score == null ? '–' : String(j.score) }),
          el('span', { class: 'sr-only', text: j.score == null ? 'Not scored' : 'Fit score ' + j.score }),
        ]),
        el('div', {}, [
          open,
          el('div', { class: 'meta', text: [j.company, j.location].filter(Boolean).join(' · ') }),
          el('div', {}, chips),
        ]),
      ]);
      list.appendChild(row);
    });
  }

  function postSelection() { post({ action: 'select', userId: state.activeId, jobIds: Array.from(state.selected) }); }
  function toggle(id, on) {
    if (on) state.selected.add(id); else state.selected.delete(id);
    postSelection();
    renderJobs();
  }

  $('select-all').addEventListener('change', function (e) {
    state.selected = e.target.checked ? new Set(state.jobs.map(function (j) { return j.id; })) : new Set();
    postSelection();
    renderJobs();
  });

  function showDetail(id) {
    var j = state.jobs.find(function (x) { return x.id === id; });
    if (!j) return;
    state.current = id;
    renderJobs();
    var d = $('job-detail');
    d.hidden = false;
    d.textContent = '';

    d.appendChild(el('h2', { text: j.title + ' — ' + j.company }));
    d.appendChild(JH.button('Open posting', null, function () { post({ action: 'openPosting', url: j.url }); }));

    d.appendChild(el('h3', { text: 'What this job actually is' }));
    d.appendChild(el('p', { text: j.plainDescription || 'Not summarized yet.' }));
    if (j.highlights && j.highlights.length) d.appendChild(el('ul', {}, j.highlights.map(function (h) { return el('li', { text: h }); })));
    if (j.redFlags && j.redFlags.length) {
      d.appendChild(el('h3', { text: 'Red flags' }));
      d.appendChild(el('ul', {}, j.redFlags.map(function (h) { return el('li', { class: 'flag', text: h }); })));
    }

    if (j.breakdown && j.breakdown.length) {
      d.appendChild(el('h3', { text: 'Why it scored ' + j.score }));
      var rows = j.breakdown.map(function (c) {
        var pct = c.weight > 0 ? Math.round(100 * c.earned / c.weight) : 0;
        return el('tr', {}, [
          el('th', { scope: 'row', text: c.criterion }),
          el('td', {}, [el('div', { class: 'bar', role: 'img', 'aria-label': pct + '%' }, [el('span', { style: 'width:' + pct + '%' })])]),
          el('td', { class: 'muted', text: c.detail }),
        ]);
      });
      d.appendChild(el('table', { class: 'breakdown' }, [el('caption', { class: 'sr-only', text: 'Score breakdown' }), el('tbody', {}, rows)]));
    }

    d.appendChild(el('h3', { text: 'Your documents' }));
    var docs = el('div', { class: 'actions' });
    [['Résumé', j.resumePath, j.resumeExists], ['Cover letter', j.coverLetterPath, j.coverLetterExists]].forEach(function (x) {
      var b = JH.button('Open ' + x[0], null, function () { post({ action: 'openDocument', path: x[1] }); });
      b.disabled = !x[2];
      if (!x[2]) b.title = 'Not generated yet';
      docs.appendChild(b);
    });
    d.appendChild(docs);
  }

  // ── applications ──────────────────────────────────────────────────────────────────────────
  function renderApplications() {
    var showDry = $('show-dry-runs').checked;
    var rows = state.applications.filter(function (a) { return showDry || !a.isDryRun; });
    var body = $('application-rows');
    body.textContent = '';
    $('applications-empty').hidden = rows.length > 0;
    rows.forEach(function (a) {
      body.appendChild(el('tr', {}, [
        el('td', { text: new Date(a.appliedAt).toLocaleDateString() }),
        el('td', {}, [el('div', { text: a.job.title }), el('div', { class: 'muted', text: a.job.company })]),
        el('td', { text: label(a.outcome) }),
        el('td', {}, [a.isDryRun ? el('span', { class: 'dry', text: 'DRY RUN' }) : null]),
      ]));
    });
  }
  $('show-dry-runs').addEventListener('change', renderApplications);

  // ── settings ──────────────────────────────────────────────────────────────────────────────
  function renderSettings() {
    var s = state.settings;
    $('dry-run').checked = s.dryRun;
    $('daily-cap').value = s.dailyApplyCap;
    $('preflight').checked = s.preflightQuestionScan;

    var select = $('provider');
    select.textContent = '';
    state.providers.forEach(function (p) {
      var o = el('option', { value: p.id, text: p.displayName });
      if (p.id === s.selectedLlmProvider) o.selected = true;
      select.appendChild(o);
    });

    var keys = $('provider-keys');
    keys.textContent = '';
    state.providers.forEach(function (p) {
      var area = el('textarea', { 'aria-label': p.displayName + ' API keys, one per line', placeholder: 'Paste an API key (one per line for several)', spellcheck: 'false' });
      var stateText = p.ownKeyCount > 0 ? p.ownKeyCount + ' key' + (p.ownKeyCount > 1 ? 's' : '') + ' saved'
        : p.hasSharedKey ? 'Using the shared MindAttic key' : 'Not configured';
      var save = JH.button('Add', 'Add the ' + p.displayName + ' key', function () {
        var entered = area.value.split(/\r?\n/).map(function (k) { return k.trim(); }).filter(Boolean);
        if (!entered.length) { JH.toast('Paste a key first.', true); area.focus(); return; }
        post({ action: 'saveKeys', provider: p.id, keys: entered });
        area.value = '';
      });
      var tools = el('div', { class: 'key-tools' }, [save]);
      if (p.ownKeyCount > 0) {
        tools.appendChild(JH.button('Remove keys', 'Remove the saved ' + p.displayName + ' keys', function () {
          if (window.confirm('Remove the ' + p.ownKeyCount + ' saved ' + p.displayName + ' key(s)?')) post({ action: 'clearKeys', provider: p.id });
        }));
      }
      keys.appendChild(el('div', { class: 'provider-row' }, [
        el('div', {}, [el('div', { text: p.displayName }), el('div', { class: 'key-state' + (p.ownKeyCount || p.hasSharedKey ? ' ok' : ''), text: stateText })]),
        area, tools,
      ]));
    });
  }

  function saveSettings() {
    post({ action: 'saveSettings', settings: {
      selectedLlmProvider: $('provider').value,
      dryRun: $('dry-run').checked,
      dailyApplyCap: parseInt($('daily-cap').value, 10) || 25,
      preflightQuestionScan: $('preflight').checked,
    } });
  }
  ['dry-run', 'preflight', 'provider', 'daily-cap'].forEach(function (id) { $(id).addEventListener('change', saveSettings); });
  $('export-backup').addEventListener('click', function () { post({ action: 'exportBackup' }); });
  $('import-backup').addEventListener('click', function () { post({ action: 'importBackup' }); });

  // ── host messages ─────────────────────────────────────────────────────────────────────────
  var toastTimer;
  JH.toast = function (text, error) {
    // Errors go to the role=alert region (announced at once); everything else is polite.
    // 2.2.1 Timing: errors stay until the next message instead of vanishing on a timer.
    var t = $(error ? 'toast-alert' : 'toast');
    var other = $(error ? 'toast' : 'toast-alert');
    other.hidden = true;
    t.textContent = text;
    t.hidden = false;
    clearTimeout(toastTimer);
    if (!error) toastTimer = setTimeout(function () { t.hidden = true; }, 6000);
  };
  $('toast-alert').addEventListener('click', function () { $('toast-alert').hidden = true; });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !$('toast-alert').hidden) $('toast-alert').hidden = true;
  });

  /** Applies one host message. Also the seam tools/verify-ui.mjs uses to render fixture data. */
  JH.receive = function (m) {
    switch (m.type) {
      case 'users':
        state.users = m.users; state.deleted = m.deleted; state.activeId = m.activeId;
        renderUsers();
        break;
      case 'profile':
        state.profile = state.savedProfile = m.profile; state.passwordSaved = m.passwordSaved || {}; state.boards = m.boards || [];
        JH.renderApplicant(m.profile, state.passwordSaved, state.boards);
        renderUsers();
        break;
      case 'search': JH.renderRequirements(m.search, m.preview); break;
      case 'jobs':
        state.jobs = m.jobs;
        state.selected = new Set(m.jobs.filter(function (j) { return j.selectedForApply; }).map(function (j) { return j.id; }));
        renderJobs();
        break;
      case 'applications': state.applications = m.applications; renderApplications(); break;
      case 'settings': state.settings = m.settings; state.providers = m.providers; renderSettings(); break;
      case 'toast': JH.toast(m.text, m.error); break;
      case 'hunt': renderHunt(m); break;
      case 'boardStatus': JH.setBoardStatus(m.boardId, m.signedIn); break;
    }
  };

  // While a hunt runs, the applicant can't change underneath it.
  function renderHunt(m) {
    state.hunting = !!m.running;
    // Nothing that swaps the applicant or replaces the database while a hunt uses them.
    ['import-profile', 'import-backup'].forEach(function (id) { $(id).disabled = !!m.running; });
    $('requirements-hunt').textContent = m.running ? 'Stop hunt' : 'Save & hunt now';
    var status = $('hunt-status');
    if (m.message) status.textContent = m.message;
    status.hidden = !status.textContent;
    ['user-picker', 'user-add', 'user-delete'].forEach(function (id) { $(id).disabled = !!m.running || (id === 'user-delete' && !state.activeId); });
    if (!m.running && m.showBestFits) JH.showTab('tab-jobs');
  }
  window.chrome.webview.addEventListener('message', function (e) { JH.receive(e.data); });

  post({ action: 'ready' });
})();
