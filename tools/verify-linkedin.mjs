// Runs JobHunt's LinkedIn page scripts — the SAME files the app embeds
// (JobHunt.Core/Boards/LinkedIn/Scripts/*.js) — against reduced models of LinkedIn's pages in
// jsdom, and checks what they read. Layout-free by design: the scripts use textContent, so a DOM
// with no rendering is enough to pin their parsing.
//
//   cd tools && npm install && node verify-linkedin.mjs
//
// These fixtures model LinkedIn's markup as of 2024–2026. A live signed-in run is still the real
// test; when LinkedIn changes its markup, save the new page into fixtures/ and extend this file.
import { JSDOM } from 'jsdom';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const scripts = path.join(here, '..', 'JobHunt.Core', 'Boards', 'LinkedIn', 'Scripts');
const fixtures = path.join(here, 'fixtures', 'linkedin');
const read = (dir, f) => fs.readFileSync(path.join(dir, f), 'utf8');
const results = [];
const check = (name, ok, detail) => results.push({ name, ok: !!ok, detail });

function run(fixture, script, url, html) {
  const dom = new JSDOM(html ?? read(fixtures, fixture), { url, runScripts: 'outside-only' });
  return JSON.parse(dom.window.eval(read(scripts, script)));
}

// ── signed-in two-pane search ─────────────────────────────────────────────────────────────
{
  const url = 'https://www.linkedin.com/jobs/search/?keywords=.NET%20Developer&currentJobId=4012345678';
  const r = run('search-signed-in.html', 'results.js', url);
  const [a, b, shell] = r.cards;
  check('signed-in: state is results', r.state === 'results', r.state);
  check('signed-in: three cards, outermost only (nested card markup not double-counted)', r.cards.length === 3, r.cards.map(c => c.id).join(','));
  check('signed-in: title from aria-label, "with verification" dropped', a.title === 'Senior .NET Developer', a.title);
  check('signed-in: company and location', a.company === 'Acme Health' && a.location === 'United States (Remote)', `${a.company} | ${a.location}`);
  check('signed-in: Easy Apply and Promoted flags', a.quickApply && a.promoted && !a.applied, JSON.stringify(a));
  check('signed-in: title from <strong> when there is no aria-label', b.title === 'Full Stack Engineer (C#/React)', b.title);
  check('signed-in: "Applied" footer state', b.applied === true && b.quickApply === false, JSON.stringify(b));
  check('signed-in: an undrawn shell still reports its id, with no title', shell.id === '4012346000' && shell.title === '', JSON.stringify(shell));

  const d = run('search-signed-in.html', 'details.js', url);
  check('details: ready, and for the selected job', d.state === 'details' && d.id === '4012345678', `${d.state} ${d.id}`);
  check('details: title and company', d.title === 'Senior .NET Developer' && d.company === 'Acme Health', `${d.title} | ${d.company}`);
  check('details: location', d.location === 'United States', d.location);
  check('details: top-card facts split on "·"', JSON.stringify(d.facts) === JSON.stringify(['United States', 'Reposted 2 days ago', 'Over 100 applicants']), JSON.stringify(d.facts));
  check('details: salary, workplace and type tags (screen-reader prefix stripped)',
    ['$140K/yr - $165K/yr', 'Remote', 'Full-time'].every(t => d.insights.some(i => i.startsWith(t))), JSON.stringify(d.insights));
  check('details: Easy Apply button recognized', d.apply === 'easy', d.apply);
  check('details: description keeps paragraphs and bullets', /claims API/.test(d.description) && /• Mentor two engineers/.test(d.description) && d.description.includes('\n'), JSON.stringify(d.description.slice(0, 120)));
}

// ── the pane still showing the previous job right after a click ───────────────────────────
{
  const stale = run('search-signed-in.html', 'details.js', 'https://www.linkedin.com/jobs/search/?keywords=x&currentJobId=4012345999');
  check('stale pane: id is the job the pane shows, not the URL\'s new currentJobId', stale.id === '4012345678', stale.id);
  const html = read(fixtures, 'search-signed-in.html').replace('This is a full-time, fully remote role.',
    'Please see application instructions below. Candidates who applied 6 months ago may reapply.');
  const d = run(null, 'details.js', 'https://www.linkedin.com/jobs/search/?currentJobId=4012345678', html);
  check('"applied" words inside the description don\'t mark the job applied', d.apply === 'easy', d.apply);
}

// ── signed-out list ───────────────────────────────────────────────────────────────────────
{
  const r = run('search-signed-out.html', 'results.js', 'https://www.linkedin.com/jobs/search?keywords=.NET%20Developer');
  const c = r.cards[0];
  check('signed-out: base-card read (id from data-entity-urn)', r.state === 'results' && c?.id === '4099000001', JSON.stringify(r));
  check('signed-out: title, company, location', c?.title === '.NET Developer' && c?.company === 'Initrode' && c?.location === 'Austin, TX', JSON.stringify(c));
}

// ── /jobs/view/ page with an external Apply ───────────────────────────────────────────────
{
  const d = run('job-view-external.html', 'details.js', 'https://www.linkedin.com/jobs/view/staff-engineer-at-globex-4077000002/');
  check('job view: id from a slugged /jobs/view/ URL', d.id === '4077000002', d.id);
  check('job view: company and facts from the older top card', d.company === 'Globex' && d.facts.includes('57 applicants') && d.facts.includes('1 week ago'), `${d.company} ${JSON.stringify(d.facts)}`);
  check('job view: location is not the company that leads the line', d.location === 'Springfield, IL', d.location);
  check('job view: external Apply is not Easy Apply', d.apply === 'external', d.apply);
  check('job view: contract, hourly pay and on-site tags', ['Contract', '$70/hr - $85/hr', 'On-site'].every(t => d.insights.includes(t)), JSON.stringify(d.insights));
}

// ── pages that must stop a hunt ───────────────────────────────────────────────────────────
{
  const challenge = run(null, 'results.js', 'https://www.linkedin.com/checkpoint/challenge/AgF', '<html><body><h1>Let\'s do a quick security check</h1></body></html>');
  check('checkpoint URL → challenge', challenge.state === 'challenge', challenge.state);
  const captcha = run(null, 'details.js', 'https://www.linkedin.com/jobs/view/1234567/', '<html><body><iframe src="https://www.linkedin.com/captcha/x"></iframe></body></html>');
  check('captcha iframe → challenge (details)', captcha.state === 'challenge', captcha.state);
  const authwall = run(null, 'results.js', 'https://www.linkedin.com/authwall?trk=x', '<html><body></body></html>');
  check('authwall → signedOut', authwall.state === 'signedOut', authwall.state);
  const none = run(null, 'results.js', 'https://www.linkedin.com/jobs/search/?keywords=zzz',
    '<html><body><div class="jobs-search-no-results-banner">No matching jobs found.</div></body></html>');
  check('no-results banner → noResults', none.state === 'noResults', none.state);
  const loading = run(null, 'results.js', 'https://www.linkedin.com/jobs/search/?keywords=x', '<html><body><main></main></body></html>');
  check('empty page → loading (keep waiting, don\'t guess)', loading.state === 'loading', loading.state);
}

for (const r of results) console.log(`${r.ok ? 'PASS' : 'FAIL'} ${r.name}${r.ok ? '' : '\n    got: ' + r.detail}`);
const failed = results.filter(r => !r.ok).length;
console.log(`\nRESULT: ${results.length - failed}/${results.length} passed`);
process.exit(failed ? 1 : 0);
