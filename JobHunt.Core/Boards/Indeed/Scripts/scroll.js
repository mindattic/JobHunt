// Scrolls the results list one screen further so lazily-rendered cards draw. Returns JSON
// { atBottom }. Same generic "find the nearest scrolling ancestor, else scroll the window"
// algorithm as LinkedIn's scroll.js — Indeed's results are usually a plain page scroll, but a
// nested scroll container is tried first in case the two-pane layout is showing.
(function () {
  function scrolls(el) {
    if (!el) return false;
    var s = getComputedStyle(el);
    return (s.overflowY === 'auto' || s.overflowY === 'scroll') && el.scrollHeight > el.clientHeight + 1;
  }
  var box = null;
  var card = document.querySelector('.job_seen_beacon[data-jk], .cardOutline[data-jk], td.resultContent');
  for (var el = card && card.parentElement; el && el !== document.body && !box; el = el.parentElement) if (scrolls(el)) box = el;
  if (!box) {
    window.scrollBy(0, Math.max(400, window.innerHeight * 0.8));
    var docEl = document.scrollingElement || document.documentElement;
    return JSON.stringify({ atBottom: docEl.scrollTop + window.innerHeight >= docEl.scrollHeight - 4 });
  }
  box.scrollTop = box.scrollTop + Math.max(300, box.clientHeight * 0.8);
  return JSON.stringify({ atBottom: box.scrollTop + box.clientHeight >= box.scrollHeight - 4 });
})()
