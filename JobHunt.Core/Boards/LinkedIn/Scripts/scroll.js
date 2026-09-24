// Scrolls the results list one screen further, so LinkedIn draws the next lazily-rendered
// ("occludable") cards. Returns JSON { atBottom }. The list is its own scroll container on the
// signed-in page (which of the nested wrappers scrolls varies by layout) and the window on the
// signed-out one.
(function () {
  function scrolls(el) {
    if (!el) return false;
    var s = getComputedStyle(el);
    return (s.overflowY === 'auto' || s.overflowY === 'scroll') && el.scrollHeight > el.clientHeight + 1;
  }
  var box = null;
  var named = document.querySelectorAll('.jobs-search-results-list, .scaffold-layout__list > div, .scaffold-layout__list');
  for (var i = 0; i < named.length && !box; i++) if (scrolls(named[i])) box = named[i];
  if (!box) {
    var card = document.querySelector('li[data-occludable-job-id], .job-card-container, .scaffold-layout__list-item, ul.jobs-search__results-list > li');
    for (var el = card && card.parentElement; el && el !== document.body && !box; el = el.parentElement) if (scrolls(el)) box = el;
  }
  if (!box) {
    window.scrollBy(0, Math.max(400, window.innerHeight * 0.8));
    var docEl = document.scrollingElement || document.documentElement;
    return JSON.stringify({ atBottom: docEl.scrollTop + window.innerHeight >= docEl.scrollHeight - 4 });
  }
  box.scrollTop = box.scrollTop + Math.max(300, box.clientHeight * 0.8);
  return JSON.stringify({ atBottom: box.scrollTop + box.clientHeight >= box.scrollHeight - 4 });
})()
