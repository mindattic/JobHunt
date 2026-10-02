// Attaches to a RUNNING JobHunt.App over CDP and dumps the live DOM of whichever board pane
// matches a URL substring — so a board's sign-in form and job-card markup can be read from the
// real site instead of guessed. Never types, clicks, or submits anything: read-only, always.
//
// Usage:
//   1. Launch JobHunt.App normally — it always opens CDP on 9366 (panel) and 9367 (board panes),
//      one port per WebView2 browser process (see MainWindow.PanelDebugPort/BoardDebugPort).
//   2. In the app, sign in to the board and run a search so the page you want to inspect is showing.
//   3. cd tools && node inspect-board.mjs indeed            # lists pages if the match is ambiguous
//      node inspect-board.mjs indeed --mode=signin          # dump sign-in form fields
//      node inspect-board.mjs indeed --mode=results         # dump job-card structure
//      node inspect-board.mjs indeed --mode=details          # dump the open job's details pane
//      node inspect-board.mjs indeed --mode=html             # dump raw outerHTML (first 20000 chars)
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const { chromium } = require('playwright-core');

const [, , matchArg, ...rest] = process.argv;
const modeArg = rest.find(a => a.startsWith('--mode='))?.slice('--mode='.length) ?? 'signin';
const portFlag = rest.find(a => a.startsWith('--port='))?.slice('--port='.length);
const ports = portFlag ? [Number(portFlag)] : [9367, 9366];

if (!matchArg) {
  console.error('Usage: node inspect-board.mjs <url-substring> [--mode=signin|results|details|html] [--port=N]');
  process.exit(1);
}

// Every reachable browser process's pages, so a match is found whichever process holds it.
const browsers = [];
for (const port of ports) {
  try { browsers.push(await chromium.connectOverCDP(`http://127.0.0.1:${port}`)); }
  catch { /* that process isn't up — the board pane only exists once an applicant is chosen */ }
}
if (browsers.length === 0) {
  console.error(`Couldn't connect to CDP on port(s) ${ports.join(', ')}. Is JobHunt.App running?`);
  process.exit(1);
}
const closeAll = () => Promise.all(browsers.map(b => b.close().catch(() => {})));

const pages = browsers.flatMap(b => b.contexts().flatMap(c => c.pages()));
const matches = pages.filter(p => p.url().toLowerCase().includes(matchArg.toLowerCase()));

if (matches.length === 0) {
  console.error(`No open page matched "${matchArg}". Open pages:`);
  for (const p of pages) console.error(' -', p.url());
  await closeAll();
  process.exit(1);
}
if (matches.length > 1) {
  console.error(`Multiple pages matched "${matchArg}" — pick a more specific substring:`);
  for (const p of matches) console.error(' -', p.url());
  await closeAll();
  process.exit(1);
}

const page = matches[0];
console.log('Inspecting:', page.url());
console.log('Title:', await page.title());
console.log();

if (modeArg === 'html') {
  const html = await page.content();
  console.log(html.slice(0, 20000));
  console.log(html.length > 20000 ? `\n… (${html.length} chars total, truncated)` : '');
} else if (modeArg === 'signin') {
  const inputs = await page.$$eval('input', els => els.map(e => ({
    tag: e.tagName.toLowerCase(), type: e.type, name: e.name || null, id: e.id || null,
    ariaLabel: e.getAttribute('aria-label'), placeholder: e.placeholder || null,
    nearbyLabel: (e.closest('label')?.textContent || document.querySelector(`label[for="${e.id}"]`)?.textContent || '').trim().slice(0, 80) || null,
  })));
  console.log('INPUTS:', JSON.stringify(inputs, null, 2));

  const buttons = await page.$$eval('button, input[type=submit], a[role=button]', els => els.slice(0, 20).map(e => ({
    tag: e.tagName.toLowerCase(), text: (e.textContent || e.value || '').trim().slice(0, 60), type: e.type || null,
    id: e.id || null, ariaLabel: e.getAttribute('aria-label'),
  })));
  console.log('BUTTONS:', JSON.stringify(buttons, null, 2));
} else if (modeArg === 'results') {
  // Heuristic: find the class shared by the largest group of sibling-like elements that each
  // contain a link — the repeating "card" pattern — and report its shape + one sample's outerHTML.
  const analysis = await page.evaluate(() => {
    const byClass = new Map();
    document.querySelectorAll('[class]').forEach(el => {
      if (!el.querySelector('a')) return;
      el.classList.forEach(c => {
        if (!byClass.has(c)) byClass.set(c, []);
        byClass.get(c).push(el);
      });
    });
    const ranked = [...byClass.entries()].filter(([, els]) => els.length >= 3 && els.length <= 60)
      .sort((a, b) => b[1].length - a[1].length).slice(0, 8);
    return ranked.map(([cls, els]) => ({
      class: cls, count: els.length,
      sample: els[0].outerHTML.slice(0, 1500),
      dataAttrs: [...new Set(els.slice(0, 5).flatMap(el => [...el.attributes].filter(a => a.name.startsWith('data-')).map(a => a.name)))],
    }));
  });
  console.log('CANDIDATE CARD CLASSES (largest repeating groups containing a link):');
  console.log(JSON.stringify(analysis, null, 2));
} else if (modeArg === 'details') {
  const main = await page.evaluate(() => {
    const root = document.querySelector('main') || document.body;
    return {
      h1: [...document.querySelectorAll('h1')].map(h => h.outerHTML.slice(0, 300)),
      dataJk: document.querySelector('[data-jk]')?.getAttribute('data-jk') || null,
      bodyStart: root.outerHTML.slice(0, 3000),
    };
  });
  console.log('DETAILS PANE:', JSON.stringify(main, null, 2));
} else {
  console.error(`Unknown --mode=${modeArg}`);
}

await closeAll();
