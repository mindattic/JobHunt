// WCAG 2.2 AA verification of the JobHunt panel, run against the REAL app.
//
//   cd tools && npm install && node verify-ui.mjs [path-to-JobHunt.App.exe]
//
// Starts JobHunt against a throwaway data folder with a DevTools port, attaches over CDP, fills the
// panel with an applicant (every list section populated) plus fixture best-fits and applications,
// and then, in BOTH themes and on EVERY tab:
//   • axe-core with the wcag2a/2aa/21a/21aa/22aa rule sets — ANY violation fails (not just serious);
//   • keyboard walk: Tab through every control; each must show a focus indicator (2.4.7) and must not
//     be entirely hidden behind the sticky header or save bar (2.4.11);
//   • reflow at 320 CSS px wide: no horizontal page scrolling (1.4.10);
//   • text spacing overrides (1.4.12): no horizontal overflow with the spacing bumped.
// Exits non-zero on any failure. Colour contrast of the native WPF command bar is not visible to a
// DOM audit; it is checked arithmetically at the end against the XAML colours.
import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');
const axePath = require.resolve('axe-core/axe.min.js');

const here = path.dirname(fileURLToPath(import.meta.url));
const exe = process.argv[2] ?? path.join(here, '..', 'JobHunt.App', 'bin', 'Debug', 'net10.0-windows', 'JobHunt.App.exe');
const port = 9344;
const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'jobhunt-verify-'));
const failures = [];
const passes = [];
const ok = (name) => passes.push(name);
const fail = (name, detail) => failures.push(`${name}\n    ${detail}`);

const app = spawn(exe, [], {
  env: {
    ...process.env,
    JOBHUNT_DATA_DIR: path.join(scratch, 'data'),
    JOBHUNT_DOCUMENTS_DIR: path.join(scratch, 'docs'),
    JOBHUNT_SECRETS_DIR: path.join(scratch, 'secrets'),
    WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${port}`,
  },
  stdio: 'ignore',
});

async function connect() {
  for (let i = 0; i < 60; i++) {
    try {
      const browser = await chromium.connectOverCDP(`http://127.0.0.1:${port}`);
      for (let j = 0; j < 40; j++) {
        const page = browser.contexts().flatMap(c => c.pages()).find(p => p.url().includes('jobhunt.local'));
        if (page) return { browser, page };
        await new Promise(r => setTimeout(r, 250));
      }
    } catch { /* not listening yet */ }
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error('JobHunt panel never appeared on the DevTools port');
}

const FIXTURE_JOBS = [
  {
    id: 101, title: 'Senior .NET Developer', company: 'Acme Health', location: 'Remote (US)', url: 'https://www.linkedin.com/jobs/view/101/',
    score: 91, status: 'Recommended', selectedForApply: true, workplace: 'Remote', employmentType: 'FullTime', duration: '', salary: '$150k–$170k/yr',
    plainDescription: 'Build and run the billing APIs for a mid-size healthcare company. C# and ASP.NET Core on Azure; small team, real ownership.',
    highlights: ['Owns the claims API end to end', 'Mentors two engineers'], redFlags: ['On-call one week in four'], techStack: ['C#', 'Azure'], evidence: {},
    breakdown: [
      { criterion: 'Required skills', weight: 35, earned: 35, detail: 'Matched 5/5' },
      { criterion: 'Salary', weight: 15, earned: 13, detail: 'Up to $170k/yr vs your $130k minimum' },
      { criterion: 'Freshness', weight: 5, earned: 4, detail: 'posted 1d ago, 18 applicants' },
    ],
    resumePath: '', coverLetterPath: '', resumeExists: false, coverLetterExists: false,
  },
  {
    id: 102, title: 'Full Stack Engineer (C#/React)', company: 'Foo Corp', location: 'Chicago, IL (Hybrid)', url: 'https://www.linkedin.com/jobs/view/102/',
    score: 64, status: 'Recommended', selectedForApply: false, workplace: 'Hybrid', employmentType: 'Contract', duration: '6 months, likely to extend', salary: '$70–$85/hr',
    plainDescription: 'Six-month contract modernizing an internal React + .NET app.', highlights: [], redFlags: [], techStack: [], evidence: {}, breakdown: [],
    resumePath: '', coverLetterPath: '', resumeExists: false, coverLetterExists: false,
  },
];
const FIXTURE_APPLICATIONS = [
  { id: 1, appliedAt: '2026-09-22T15:00:00Z', isDryRun: false, method: 'LinkedIn Easy Apply', outcome: 'Interviewing', job: { title: 'Staff Engineer', company: 'Initrode', url: 'https://www.linkedin.com/jobs/view/9/' }, events: [], runLog: [] },
  { id: 2, appliedAt: '2026-09-23T15:00:00Z', isDryRun: true, method: 'LinkedIn Easy Apply', outcome: 'Submitted', job: { title: 'Senior .NET Developer', company: 'Acme Health', url: 'https://www.linkedin.com/jobs/view/101/' }, events: [], runLog: [] },
];

const TABS = ['tab-applicant', 'tab-requirements', 'tab-jobs', 'tab-applications', 'tab-settings'];

try {
  const { browser, page } = await connect();
  // "Discard unsaved changes?" confirmations: answer yes, as a person continuing would.
  page.on('dialog', d => d.accept().catch(() => {}));
  await page.waitForSelector('#user-picker');

  // ── seed an applicant through the UI ──────────────────────────────────────────────────────
  await page.click('#tab-applicant');
  await page.click('#user-add');
  await page.fill('#sec-contact input >> nth=0', 'Renée');
  await page.fill('#sec-contact input >> nth=2', 'Okafor');
  await page.fill('#sec-contact input[type=email] >> nth=0', 'renee@example.com');
  await page.click('#applicant-save');
  await page.waitForFunction(() => document.querySelector('#user-picker option:checked')?.textContent === 'Renée Okafor');

  // Let the host finish pushing the new applicant's data (its empty best-fits list would otherwise
  // land after, and wipe, the fixtures below).
  await page.waitForSelector('#requirements-form .tags', { state: 'attached' });
  await page.waitForTimeout(1500);

  // Every section open, every list with one entry (and one nested bullet), so every control
  // type the form engine can produce is on the page for the audit.
  await page.evaluate(() => {
    document.querySelectorAll('#applicant-sections details').forEach(d => { d.open = true; });
    document.querySelectorAll('#applicant-sections button.add').forEach(b => b.click());
    document.querySelectorAll('#applicant-sections .list-item button.add').forEach(b => b.click());
  });
  await page.evaluate(({ jobs, applications }) => {
    window.JH.receive({ type: 'jobs', jobs });
    window.JH.receive({ type: 'applications', applications });
  }, { jobs: FIXTURE_JOBS, applications: FIXTURE_APPLICATIONS });
  await page.click('#tab-jobs');
  await page.waitForSelector('#job-list .job-title');
  await page.click('#job-list .job-title >> nth=0');
  await page.addScriptTag({ path: axePath });

  const cdp = await page.context().newCDPSession(page);

  for (const theme of ['dark', 'light']) {
    await page.evaluate(t => { document.documentElement.dataset.theme = t; }, theme);

    for (const tab of TABS) {
      const where = `[${theme}] ${tab.replace('tab-', '')}`;
      await page.click('#' + tab);
      await page.evaluate(() => window.scrollTo(0, 0));

      // 1. axe — every WCAG 2.x A/AA rule, any impact.
      const violations = await page.evaluate(async () => {
        const res = await window.axe.run(document, {
          runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] },
          resultTypes: ['violations'],
        });
        return res.violations.map(v => `${v.id} (${v.impact}): ${v.help} — ${v.nodes.slice(0, 4).map(n => n.target.join(' ')).join(' | ')}${v.nodes.length > 4 ? ` (+${v.nodes.length - 4} more)` : ''}`);
      });
      if (violations.length) fail(`${where}: axe`, violations.join('\n    '));
      else ok(`${where}: axe — no WCAG 2.2 A/AA violations`);

      // 2. keyboard walk — focus visible and not obscured, for every Tab stop in the visible view.
      await page.evaluate(() => { document.activeElement?.blur(); window.scrollTo(0, 0); });
      await page.focus('#' + tab);
      const count = await page.evaluate(() =>
        document.querySelectorAll('button, input, select, textarea, summary, a[href], [tabindex]:not([tabindex="-1"])').length);
      const problems = new Set();
      let stops = 0;
      for (let i = 0; i < count + 5; i++) {
        await page.keyboard.press('Tab');
        const info = await page.evaluate(() => {
          const el = document.activeElement;
          if (!el || el === document.body) return { done: true };
          const r = el.getBoundingClientRect();
          const cs = getComputedStyle(el);
          const host = el.closest('.tags');
          const ring = (s) => (s.outlineStyle !== 'none' && parseFloat(s.outlineWidth) >= 1) || (s.boxShadow && s.boxShadow !== 'none');
          const visible = ring(cs) || (host && ring(getComputedStyle(host)));
          const header = document.querySelector('.top').getBoundingClientRect();
          const bars = [...document.querySelectorAll('.save-bar')].filter(b => b.offsetParent !== null).map(b => b.getBoundingClientRect());
          const insideBar = el.closest('.save-bar') || el.closest('.top');
          const hiddenByHeader = !insideBar && r.bottom <= header.bottom;
          const hiddenByBar = !insideBar && bars.some(b => r.top >= b.top && r.bottom <= b.bottom + 1);
          const label = (el.id ? '#' + el.id : el.tagName.toLowerCase()) + `<${el.tagName.toLowerCase()}${el.type ? ' type=' + el.type : ''}>` +
            (el.labels && el.labels[0] ? ` "${el.labels[0].textContent.trim().slice(0, 30)}"` : el.textContent ? ` "${el.textContent.trim().slice(0, 30)}"` : '');
          return { done: false, label, visible, obscured: hiddenByHeader || hiddenByBar, zeroSize: r.width === 0 && r.height === 0 };
        });
        if (info.done) break;
        stops++;
        if (info.zeroSize) continue;
        if (!info.visible) problems.add(`no visible focus indicator: ${info.label}`);
        if (info.obscured) problems.add(`focused control hidden behind a sticky bar: ${info.label}`);
      }
      if (problems.size) fail(`${where}: keyboard (2.4.7 / 2.4.11)`, [...problems].slice(0, 12).join('\n    '));
      else ok(`${where}: keyboard — ${stops} tab stops, all visibly focused and unobscured`);

      // 3. reflow at 320 CSS px (1.4.10).
      await cdp.send('Emulation.setDeviceMetricsOverride', { width: 320, height: 640, deviceScaleFactor: 1, mobile: false });
      await page.waitForTimeout(150);
      const overflow = await page.evaluate(() => {
        const doc = document.documentElement;
        const wide = [...document.querySelectorAll('main *, header *')]
          .filter(e => e.offsetParent !== null && !e.closest('.table-wrap'))
          .filter(e => e.getBoundingClientRect().right > doc.clientWidth + 1)
          .slice(0, 5).map(e => (e.id ? '#' + e.id : e.tagName.toLowerCase() + '.' + e.className));
        return { scroll: doc.scrollWidth, client: doc.clientWidth, wide };
      });
      if (overflow.scroll > overflow.client + 1) fail(`${where}: reflow at 320px (1.4.10)`, `page is ${overflow.scroll}px wide; widest: ${overflow.wide.join(', ')}`);
      else ok(`${where}: reflow — no horizontal scrolling at 320 CSS px`);
      await cdp.send('Emulation.clearDeviceMetricsOverride');

      // 4. text spacing (1.4.12).
      const spaced = await page.evaluate(() => {
        const s = document.createElement('style');
        s.id = 'wcag-spacing';
        s.textContent = '* { line-height: 1.5 !important; letter-spacing: .12em !important; word-spacing: .16em !important; } p { margin-bottom: 2em !important; }';
        document.head.appendChild(s);
        const doc = document.documentElement;
        const clipped = [...document.querySelectorAll('button, label, .tag, .chip, legend, summary')]
          .filter(e => e.offsetParent !== null && !e.closest('.sr-only') && e.scrollWidth > e.clientWidth + 2 && getComputedStyle(e).overflow === 'hidden')
          .slice(0, 5).map(e => e.textContent.trim().slice(0, 30));
        const result = { overflow: doc.scrollWidth > doc.clientWidth + 1, clipped };
        s.remove();
        return result;
      });
      if (spaced.overflow || spaced.clipped.length) fail(`${where}: text spacing (1.4.12)`, `overflow=${spaced.overflow} clipped=${spaced.clipped.join(', ')}`);
      else ok(`${where}: text spacing — nothing clipped or overflowing`);
    }
  }

  // Error identification (3.3.1): saving a nameless applicant marks and focuses the field.
  await page.evaluate(() => { document.documentElement.dataset.theme = 'dark'; });
  await page.click('#tab-applicant');
  await page.click('#user-add');
  await page.click('#applicant-save');
  const err = await page.evaluate(() => ({
    invalid: document.querySelector('#sec-contact input')?.getAttribute('aria-invalid'),
    focused: document.activeElement === document.querySelector('#sec-contact input'),
    alert: document.getElementById('toast-alert').hidden ? '' : document.getElementById('toast-alert').textContent,
  }));
  if (err.invalid === 'true' && err.focused && err.alert) ok('error identification — field marked invalid, focused, and announced');
  else fail('error identification (3.3.1)', JSON.stringify(err));

  // Redundant entry (3.3.7): the LinkedIn sign-in email defaults to the contact email.
  await page.selectOption('#user-picker', { label: 'Renée Okafor' }).catch(() => {});
  await page.waitForFunction(() => document.getElementById('applicant-title').textContent === 'Renée Okafor');
  const signInEmail = await page.inputValue('#acct-email-linkedin');
  if (signInEmail === 'renee@example.com') ok('redundant entry — LinkedIn email reuses the contact email');
  else fail('redundant entry (3.3.7)', `LinkedIn email was "${signInEmail}"`);

  await browser.close().catch(() => {});
} catch (e) {
  fail('harness', e.stack || String(e));
} finally {
  app.kill();
  await new Promise(r => { if (app.exitCode !== null) r(); else app.once('exit', r); setTimeout(r, 5000); });
}

// Native WPF command bar — colours from MainWindow.xaml, checked arithmetically (1.4.3 / 1.4.11).
const lum = h => { const c = [0, 2, 4].map(i => parseInt(h.slice(1).substr(i, 2), 16) / 255).map(v => v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4); return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; };
const ratio = (a, b) => { const x = lum(a), y = lum(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
for (const [name, fg, bg, min] of [
  ['status text', '#C8C8C8', '#252526', 4.5], ['DRY RUN badge', '#FFD54A', '#5C4A00', 4.5],
  ['Apply button', '#FFFFFF', '#0A66C2', 4.5], ['Apply button (hover)', '#FFFFFF', '#004182', 4.5],
  ['Hunt now button', '#E6E6E6', '#2D2D2D', 4.5], ['board placeholder', '#B8B8B8', '#1E1E1E', 4.5],
  ['Apply button edge vs bar', '#5AA9F5', '#252526', 3],
]) {
  const r = ratio(fg, bg);
  if (r >= min) ok(`WPF ${name}: ${r.toFixed(2)}:1`);
  else fail(`WPF ${name}`, `${r.toFixed(2)}:1 is below ${min}:1`);
}

// WebView2's browser processes can hold the profile folder a moment after the app exits.
try { fs.rmSync(scratch, { recursive: true, force: true, maxRetries: 10, retryDelay: 500 }); }
catch { console.log(`(left scratch folder ${scratch} — still locked)`); }
for (const p of passes) console.log('PASS ' + p);
for (const f of failures) console.log('FAIL ' + f);
console.log(`\nRESULT: ${passes.length}/${passes.length + failures.length} passed`);
process.exit(failures.length ? 1 : 0);
