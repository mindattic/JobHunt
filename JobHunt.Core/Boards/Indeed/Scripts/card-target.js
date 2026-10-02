// Finds the clickable title of the card for job __JOB_ID__, scrolls it into view, and returns its
// center for a trusted click. JSON { found, x, y }. The key is on the title link
// (a.jcs-JobTitle[data-jk]); the card itself only carries a job_<jk> class.
(function (jobId) {
  var link = document.querySelector('a.jcs-JobTitle[data-jk="' + jobId + '"], a[data-jk="' + jobId + '"]');
  var card = link ? link.closest('div.cardOutline, div.result') : document.querySelector('div.job_' + jobId);
  var target = link || card;
  if (!target) return JSON.stringify({ found: false });
  target.scrollIntoView({ block: 'center' });
  var r = target.getBoundingClientRect();
  return JSON.stringify({ found: r.width > 0 && r.height > 0, x: r.left + Math.min(r.width / 2, 60), y: r.top + r.height / 2 });
})(__JOB_ID__)
