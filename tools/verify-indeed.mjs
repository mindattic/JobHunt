// Runs JobHunt's Indeed page scripts — the SAME files the app embeds
// (JobHunt.Core/Boards/Indeed/Scripts/*.js) — against Indeed's markup in jsdom, and checks what
// they read. Layout-free by design: the scripts read text nodes, never layout.
//
//   cd tools && npm install && node verify-indeed.mjs
//
// fixtures/indeed/results.html and details-easy-apply.html are REAL Indeed markup, captured from
// a signed-in pane with tools/inspect-board.mjs on 2026-09-30 (styles, icons and tracking tokens
// scrubbed; job keys kept). The external-apply case is modelled from the easy-apply capture.
// When Indeed changes its markup, capture the new page into fixtures/ and extend this file.
import { JSDOM } from 'jsdom';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const scripts = path.join(here, '..', 'JobHunt.Core', 'Boards', 'Indeed', 'Scripts');
const fixtures = path.join(here, 'fixtures', 'indeed');
const read = (dir, f) => fs.readFileSync(path.join(dir, f), 'utf8');
const results = [];
const check = (name, ok, detail) => results.push({ name, ok: !!ok, detail });

function run(fixture, script, url, html) {
  const dom = new JSDOM(html ?? read(fixtures, fixture), { url, runScripts: 'outside-only' });
  return JSON.parse(dom.window.eval(read(scripts, script)));
}

// ── results list (signed-in feed) ─────────────────────────────────────────────────────────
{
  const r = run('results.html', 'results.js', 'https://www.indeed.com/jobs?q=.NET+Developer&l=Remote');
  const [a, b, c] = r.cards;
  check('results: state is results', r.state === 'results', r.state);
  check('results: three cards, job key from the title link (not the card)', r.cards.length === 3 && a.id === '658def0d84fa7505', r.cards.map(x => x.id).join(','));
  check('results: title from span[title]', a.title === 'Senior Software Engineer', a.title);
  check('results: company and location from data-testid', a.company === 'Thyme Care' && a.location === 'Remote', `${a.company} | ${a.location}`);
  check('results: salary snippet', a.salary === '$175,000 - $200,000 a year', a.salary);
  check('results: "Easily apply" read even with no space before it', a.quickApply === true, JSON.stringify(a));
  check('results: a card without "Easily apply" is not quick-apply', b.quickApply === false && c.quickApply === false, `${b.quickApply} ${c.quickApply}`);
  check('results: sponTapItem class alone does NOT mean sponsored', r.cards.every(x => x.promoted === false), r.cards.map(x => x.promoted).join(','));
  check('results: "Remote in Chicago, IL" location kept whole', b.location === 'Remote in Chicago, IL', b.location);
}

// ── details pane, Indeed Apply ────────────────────────────────────────────────────────────
{
  const d = run('details-easy-apply.html', 'details.js', 'https://www.indeed.com/jobs?q=x&vjk=658def0d84fa7505');
  check('details: ready', d.state === 'details', d.state);
  check('details: id from the pane\'s own fromjk=, not the URL', d.id === '658def0d84fa7505', d.id);
  check('details: title and company', d.title === 'Senior Software Engineer' && d.company === 'Thyme Care', `${d.title} | ${d.company}`);
  check('details: location drops the "·" and the star rating', d.location === 'Remote', d.location);
  check('details: facts are whole (a pay range is not split on " - ")',
    JSON.stringify(d.facts) === JSON.stringify(['Remote', '$175,000 - $200,000 a year', 'Full-time']), JSON.stringify(d.facts));
  check('details: job-details pills, heading excluded',
    JSON.stringify(d.insights) === JSON.stringify(['$175,000 - $200,000 a year', 'Full-time', 'On call', 'Remote']), JSON.stringify(d.insights));
  check('details: description is what follows the heading, block-aware', /^OUR MISSION/.test(d.description) && d.description.includes('\n'), JSON.stringify(d.description.slice(0, 80)));
  check('details: Indeed Apply recognized', d.apply === 'easy', d.apply);
}

// ── details pane, apply on the company's site (modelled) ──────────────────────────────────
{
  const html = read(fixtures, 'details-easy-apply.html')
    .replace(/<a [^>]*data-testid="viewjob-indeed-apply"[^>]*>[\s\S]*?<\/a>/,
      '<a href="https://careers.example.com/job/1" target="_blank" role="link"><div>Apply on company site</div></a>');
  const d = run(null, 'details.js', 'https://www.indeed.com/jobs?q=x', html);
  check('external apply: not Indeed Apply', d.apply === 'external', d.apply);
}

// ── pages that must stop a hunt ───────────────────────────────────────────────────────────
{
  const challenge = run(null, 'results.js', 'https://www.indeed.com/jobs?q=x',
    '<html><body><iframe src="https://challenges.cloudflare.com/turnstile/v0/x"></iframe></body></html>');
  check('Cloudflare challenge iframe → challenge', challenge.state === 'challenge', challenge.state);
  const queryWord = run(null, 'results.js', 'https://www.indeed.com/jobs?q=verification+engineer',
    '<html><body><main></main></body></html>');
  check('a search FOR "verification" is not a challenge', queryWord.state === 'loading', queryWord.state);
  const signedOut = run(null, 'details.js', 'https://secure.indeed.com/auth?continue=x', '<html><body></body></html>');
  check('sign-in page → signedOut', signedOut.state === 'signedOut', signedOut.state);
  const none = run(null, 'results.js', 'https://www.indeed.com/jobs?q=zzzz',
    '<html><body><h1>The search zzzz did not match any jobs</h1></body></html>');
  check('no-results text → noResults', none.state === 'noResults', none.state);
  const loading = run(null, 'results.js', 'https://www.indeed.com/jobs?q=x', '<html><body><main></main></body></html>');
  check('empty page → loading (keep waiting, don\'t guess)', loading.state === 'loading', loading.state);
}

for (const r of results) console.log(`${r.ok ? 'PASS' : 'FAIL'} ${r.name}${r.ok ? '' : '\n    got: ' + r.detail}`);
const failed = results.filter(r => !r.ok).length;
console.log(`\nRESULT: ${results.length - failed}/${results.length} passed`);
process.exit(failed ? 1 : 0);
