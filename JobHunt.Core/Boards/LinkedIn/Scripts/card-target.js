// Finds the clickable title of the card for job __JOB_ID__, scrolls it into view, and returns its
// center for a trusted click. JSON { found, x, y }.
(function (jobId) {
  var card = document.querySelector('[data-occludable-job-id="' + jobId + '"], [data-job-id="' + jobId + '"], [data-entity-urn$=":' + jobId + '"]');
  var link = card
    ? card.querySelector('a.job-card-list__title--link, a.job-card-container__link, a.job-card-list__title, a.base-card__full-link, a[href*="/jobs/view/"]')
    : document.querySelector('a[href*="/jobs/view/' + jobId + '"], a[href*="-' + jobId + '"], a[href*="currentJobId=' + jobId + '"]');
  var target = link || card;
  if (!target) return JSON.stringify({ found: false });
  target.scrollIntoView({ block: 'center' });
  var r = target.getBoundingClientRect();
  return JSON.stringify({ found: r.width > 0 && r.height > 0, x: r.left + Math.min(r.width / 2, 60), y: r.top + r.height / 2 });
})(__JOB_ID__)
