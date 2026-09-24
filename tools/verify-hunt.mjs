// End-to-end hunt through the REAL app against a local LinkedIn stand-in.
//
//   cd tools && npm install && node verify-hunt.mjs [path-to-JobHunt.App.exe]
//
// Serves a small offline "LinkedIn" (the reduced markup from fixtures/linkedin, generated per
// request), starts JobHunt with JOBHUNT_LINKEDIN_BASE_URL pointing at it, adds an applicant through
// the panel, clicks "Save & hunt now", and checks what lands in Best Fits: the real WebView2 pane,
// trusted clicks, scrolling, details reading, de-duplication, filtering, scoring and the UI — every
// part of the hunt except LinkedIn's live markup, which only a signed-in run can prove.
import http from 'node:http';
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
const cdpPort = 9366;

// ── the stand-in's data ───────────────────────────────────────────────────────────────────
const JOBS = {
  '4100000001': { title: 'Senior .NET Developer', company: 'Acme Health', place: 'United States (Remote)', facts: ['United States', '1 day ago', '18 applicants'],
    tags: ['$150K/yr - $170K/yr', 'Remote', 'Full-time'], apply: 'Easy Apply', text: 'Own the claims API. C#, ASP.NET Core, Azure.' },
  '4100000002': { title: '.NET Developer (Contract)', company: 'Foo Corp', place: 'United States (Remote)', facts: ['United States', '3 days ago', '40 applicants'],
    tags: ['$70/hr - $85/hr', 'Remote', 'Contract'], apply: 'Easy Apply', text: 'Six-month contract, likely to extend. .NET 8 migration.' },
  '4100000003': { title: 'Senior .NET Engineer', company: 'Initech', place: 'Austin, TX (Hybrid)', facts: ['Austin, TX', '2 days ago', '25 applicants'],
    tags: ['Hybrid', 'Full-time'], apply: 'Easy Apply', text: 'Hybrid, three days a week in the Austin office.' },
  '4100000004': { title: 'Lead .NET Developer', company: 'Globex', place: 'United States (Remote)', facts: ['United States', '5 days ago', 'Over 100 applicants'],
    tags: ['Remote', 'Full-time'], apply: 'Apply', text: 'Apply on our careers site.' },
  '4100000005': { title: 'C# Engineer', company: 'Umbrella', place: 'Chicago, IL (On-site)', facts: ['Chicago, IL', '1 week ago', '9 applicants'],
    tags: ['On-site', 'Full-time', '$130K/yr - $150K/yr'], apply: 'Easy Apply', text: 'On-site in Chicago.' },
};
const SEARCHES = { '.NET Developer': ['4100000001', '4100000002', '4100000003', '4100000004'], 'C# Engineer': ['4100000003', '4100000005'] };

const esc = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
// Deliberately NOT the nav markup JobHunt's page probe knows: like the real site after a redesign,
// the only reliable sign of a session is LinkedIn's li_at cookie, which the stand-in sets below.
const nav = '<header class="app-header-2026"><a href="/in/me/">Me</a></header>';
function card(id, keywords) {
  const j = JOBS[id];
  const href = `/jobs/search/?keywords=${encodeURIComponent(keywords)}&currentJobId=${id}`;
  return `<li class="scaffold-layout__list-item" data-occludable-job-id="${id}"><div class="job-card-container" data-job-id="${id}">
    <a class="job-card-container__link job-card-list__title--link" aria-label="${esc(j.title)}" href="${href}"><strong>${esc(j.title)}</strong></a>
    <div class="artdeco-entity-lockup__subtitle">${esc(j.company)}</div>
    <div class="artdeco-entity-lockup__caption"><ul class="job-card-container__metadata-wrapper"><li>${esc(j.place)}</li></ul></div>
    <ul class="job-card-container__footer-wrapper">${j.apply === 'Easy Apply' ? '<li>Easy Apply</li>' : ''}</ul></div></li>`;
}
function details(id) {
  const j = JOBS[id];
  return `<div class="jobs-search__job-details--container"><div class="jobs-details">
    <div class="job-details-jobs-unified-top-card__company-name"><a>${esc(j.company)}</a></div>
    <div class="job-details-jobs-unified-top-card__job-title"><h1><a href="/jobs/view/${id}/">${esc(j.title)}</a></h1></div>
    <div class="job-details-jobs-unified-top-card__primary-description-container"><div>${j.facts.map(esc).join(' · ')}</div></div>
    <div class="job-details-fit-level-preferences">${j.tags.map(t => `<button><strong>${esc(t)}</strong></button>`).join('')}</div>
    <button class="jobs-apply-button" aria-label="${esc(j.apply)} to ${esc(j.title)}"><span>${esc(j.apply)}</span></button>
    <div id="job-details"><h2>About the job</h2><p>${esc(j.text)}</p></div></div></div>`;
}
const page = (title, body) => `<!DOCTYPE html><html lang="en"><head><title>${esc(title)}</title></head><body>${nav}<main>${body}</main></body></html>`;

const hits = [];
const server = http.createServer((req, res) => {
  const url = new URL(req.url, 'http://x');
  hits.push(url.pathname + url.search);
  let html;
  if (url.pathname === '/jobs/search/') {
    const keywords = url.searchParams.get('keywords') ?? '';
    const start = Number(url.searchParams.get('start') ?? 0);
    const ids = start === 0 ? (SEARCHES[keywords] ?? []) : [];
    const current = url.searchParams.get('currentJobId') ?? ids[0];
    html = ids.length
      ? page(`${keywords} jobs`, `<div class="scaffold-layout__list"><ul>${ids.map(id => card(id, keywords)).join('')}</ul></div>${current && JOBS[current] ? details(current) : ''}`)
      : page(`${keywords} jobs`, '<div class="jobs-search-no-results-banner">No matching jobs found.</div>');
  } else if (url.pathname.startsWith('/jobs/view/')) {
    const id = url.pathname.match(/(\d+)/)?.[1];
    html = page('Job', JOBS[id] ? details(id) : '');
  } else {
    html = page('Jobs | LinkedIn', '<h1>Jobs</h1>');
  }
  res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'set-cookie': 'li_at=stand-in-session; Path=/; HttpOnly; Max-Age=86400' });
  res.end(html);
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${server.address().port}`;

const scratch = fs.mkdtempSync(path.join(os.tmpdir(), 'jobhunt-hunt-'));
const app = spawn(exe, [], {
  env: {
    ...process.env,
    JOBHUNT_DATA_DIR: path.join(scratch, 'data'), JOBHUNT_DOCUMENTS_DIR: path.join(scratch, 'docs'), JOBHUNT_SECRETS_DIR: path.join(scratch, 'secrets'),
    JOBHUNT_LINKEDIN_BASE_URL: base,
    WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${cdpPort}`,
  },
  stdio: 'ignore',
});

const results = [];
const check = (name, ok, detail = '') => results.push({ name, ok: !!ok, detail });
try {
  let browser, panel;
  for (let i = 0; i < 60 && !panel; i++) {
    try {
      browser = await chromium.connectOverCDP(`http://127.0.0.1:${cdpPort}`);
      panel = browser.contexts().flatMap(c => c.pages()).find(p => p.url().includes('jobhunt.local'));
    } catch { /* not up yet */ }
    if (!panel) await new Promise(r => setTimeout(r, 500));
  }
  if (!panel) throw new Error('panel never appeared');
  panel.on('dialog', d => d.accept().catch(() => {}));
  await panel.waitForSelector('#user-picker');

  // Applicant who wants remote .NET work at $130k+.
  await panel.click('#tab-applicant');
  await panel.click('#user-add');
  await panel.fill('#sec-contact input >> nth=0', 'Renée');
  await panel.fill('#sec-contact input >> nth=2', 'Okafor');
  await panel.click('#sec-preferences > summary');
  const titles = panel.locator('#sec-preferences .tags input').first();
  await titles.fill('.NET Developer'); await titles.press('Enter');   // 'C# Engineer' is added on Job Requirements, just before hunting
  await panel.click('#sec-compensation > summary');
  await panel.fill('#sec-compensation input[type=number] >> nth=2', '130000');
  await panel.click('#applicant-save');
  await panel.waitForFunction(() => document.querySelector('#user-picker option:checked')?.textContent === 'Renée Okafor');

  // Wait for the board pane to reach the stand-in, then hunt.
  for (let i = 0; i < 40 && !hits.some(h => h.startsWith('/jobs/')); i++) await new Promise(r => setTimeout(r, 250));
  await panel.waitForFunction(() => /Signed in/.test(document.getElementById('acct-status-linkedin')?.textContent || ''), null, { timeout: 30_000 })
    .catch(() => {});
  const acct = await panel.textContent('#acct-status-linkedin');
  check('Applicant tab shows the pane is signed in (no password needed)', /^✓ Signed in/.test(acct), acct);
  await panel.click('#tab-requirements');
  await panel.waitForSelector('#requirements-form .tags');
  await panel.waitForTimeout(800);
  // Add the second keyword and hunt at once: the hunt must use what was just typed (no save/hunt race).
  const kw = panel.locator('#requirements-form .tags input').first();
  await kw.fill('C# Engineer'); await kw.press('Enter');
  const started = Date.now();
  // Two quick clicks: the second lands during sign-in and must not start a second hunt.
  await panel.click('#requirements-hunt');
  await panel.click('#requirements-hunt', { timeout: 400 }).catch(() => {});
  await panel.waitForFunction(() => /Hunt (finished|stopped)|verify|Signed out|didn't look/.test(document.getElementById('hunt-status')?.textContent || '')
    && document.getElementById('requirements-hunt').textContent !== 'Stop hunt', null, { timeout: 240_000 });
  const status = await panel.textContent('#hunt-status');
  check('hunt finished', /Hunt finished: 5 jobs seen, 5 new, 2 best fits/.test(status), status);
  check('took a human pace, not machine speed', Date.now() - started > 5000, `${Date.now() - started} ms`);

  // Best Fits: only the two remote, Easy Apply jobs that meet the salary floor; best first.
  await panel.waitForSelector('#job-list .job-title');
  const titlesShown = await panel.locator('#job-list .job-title').allTextContents();
  check('best fits are the remote Easy Apply jobs', JSON.stringify([...titlesShown].sort()) === JSON.stringify(['.NET Developer (Contract)', 'Senior .NET Developer']), titlesShown.join(' | '));
  const scores = await panel.locator('#job-list .score [aria-hidden]').allTextContents();
  check('best fits are ranked, highest score first', scores.length === 2 && Number(scores[0]) >= Number(scores[1]), scores.join(', '));
  const chips = await panel.locator('#job-list li').first().locator('.chip').allTextContents();
  check('first card shows what LinkedIn stated', chips.includes('Remote') && chips.some(c => /\$1[5-7]\dK/i.test(c)), chips.join(' | '));
  await panel.click('#job-list .job-title >> nth=0');
  const breakdown = await panel.locator('#job-detail table.breakdown tr').count();
  check('score breakdown shown', breakdown >= 5, `${breakdown} rows`);

  // What was visited: every job's details via its card, the duplicate read once, no stray pages.
  const detailHits = hits.filter(h => h.includes('currentJobId='));
  const uniqueDetail = new Set(detailHits.map(h => h.match(/currentJobId=(\d+)/)[1]));
  check('each job opened by clicking its card', uniqueDetail.size === 5, [...uniqueDetail].join(','));
  check('the job found by both searches was read once', detailHits.filter(h => h.includes('currentJobId=4100000003')).length === 1, detailHits.join(' '));
  check('no fallback to /jobs/view/ pages', !hits.some(h => h.startsWith('/jobs/view/')), hits.filter(h => h.startsWith('/jobs/view/')).join(' '));
  check('the keyword added right before hunting was searched', hits.some(h => h.includes('keywords=C%23%20Engineer')), hits.filter(h => h.includes('keywords=')).join(' '));
  const firstPageLoads = hits.filter(h => h.startsWith('/jobs/search/?keywords=.NET%20Developer') && !h.includes('currentJobId')).length;
  check('a double click started exactly one hunt', firstPageLoads === 1, `${firstPageLoads} loads of the first results page`);
  check('filters pushed into the search URL', hits.some(h => h.includes('f_WT=2') && h.includes('f_AL=true') && h.includes('f_SB2=5')), hits.find(h => h.startsWith('/jobs/search/?keywords')));

  // WCAG on the populated Best Fits tab.
  await panel.click('#tab-jobs');
  await panel.addScriptTag({ path: axePath });
  const violations = await panel.evaluate(async () => (await window.axe.run(document, {
    runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] }, resultTypes: ['violations'],
  })).violations.map(v => `${v.id}: ${v.nodes.map(n => n.target.join(' ')).join(' | ')}`));
  check('populated Best Fits passes axe (WCAG 2.2 AA)', violations.length === 0, violations.join('\n    '));

  // A decimal typed into a whole-number field saves (rounded) instead of failing the whole save.
  await panel.click('#tab-applicant');
  await panel.evaluate(() => { document.getElementById('sec-summary').open = true; });
  const years = panel.locator('#sec-summary input[type=number]').first();
  await years.fill('7.5');
  await panel.click('#applicant-save');
  await panel.waitForFunction(() => /saved|didn't work/i.test(document.getElementById('toast').textContent + document.getElementById('toast-alert').textContent));
  const alert = await panel.evaluate(() => document.getElementById('toast-alert').hidden ? '' : document.getElementById('toast-alert').textContent);
  check('a decimal in a whole-number field saves', !alert && (await years.inputValue()) === '8', alert || await years.inputValue());

  await browser.close().catch(() => {});
} catch (e) {
  check('harness', false, e.stack || String(e));
} finally {
  app.kill();
  server.close();
  await new Promise(r => { if (app.exitCode !== null) r(); else app.once('exit', r); setTimeout(r, 5000); });
  try { fs.rmSync(scratch, { recursive: true, force: true, maxRetries: 10, retryDelay: 500 }); } catch { /* WebView2 still holds it */ }
}

for (const r of results) console.log(`${r.ok ? 'PASS' : 'FAIL'} ${r.name}${r.ok ? '' : '\n    got: ' + r.detail}`);
const failed = results.filter(r => !r.ok).length;
console.log(`\nRESULT: ${results.length - failed}/${results.length} passed`);
process.exit(failed ? 1 : 0);
