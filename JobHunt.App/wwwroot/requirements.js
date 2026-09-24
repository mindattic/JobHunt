// The Job Requirements tab: what to search for and what to filter out. LinkedIn applies what it can
// in the search itself; JobHunt enforces all of it afterwards against what each posting says.
(function () {
  'use strict';
  var JH = window.JH;
  var el = JH.el;

  function f(key, label, type, extra) {
    var o = { key: key, label: label, type: type || 'text' };
    Object.keys(extra || {}).forEach(function (k) { o[k] = extra[k]; });
    return o;
  }
  function choices(pairs) { return pairs.map(function (p) { return { value: p[0], label: p[1] }; }); }

  var WORKPLACES = choices([['Remote', 'Remote'], ['Hybrid', 'Hybrid'], ['OnSite', 'On-site']]);
  var TYPES = choices([['FullTime', 'Full-time'], ['Contract', 'Contract'], ['ContractToHire', 'Contract-to-hire'],
                       ['PartTime', 'Part-time'], ['Temporary', 'Temporary'], ['Internship', 'Internship']]);
  var LEVELS = choices([['Internship', 'Internship'], ['Entry', 'Entry level'], ['Associate', 'Associate'],
                        ['MidSenior', 'Mid-Senior'], ['Director', 'Director'], ['Executive', 'Executive']]);

  var SCHEMA = [
    f('name', 'Name for this search', 'text', { wide: true }),
    f('searchTerms', 'Search keywords & job titles', 'tags', { help: 'Each one is its own LinkedIn search, e.g. ".NET Developer", "Full Stack Engineer". Results are merged and de-duplicated.' }),
    { group: 'Where and how', key: 'filters', fields: [
      f('workplaces', 'Workplace (first checked is preferred)', 'multi', { options: WORKPLACES }),
      f('employmentTypes', 'Employment types I will take', 'multi', { options: TYPES }),
      f('preferredEmploymentTypes', 'Scored higher', 'multi', { options: TYPES }),
      f('location', 'Location'), f('distanceMiles', 'Within (miles)', 'number', { min: 0 }),
      f('minContractMonths', 'Shortest contract (months)', 'number', { min: 0 }),
      f('experienceLevels', 'Experience levels (none = any)', 'multi', { options: LEVELS }),
    ] },
    { group: 'Pay', key: 'filters', fields: [
      f('minAnnualSalary', 'Minimum salary (yearly)', 'number', { min: 0, step: 1000 }),
      f('minHourlyRate', 'Minimum hourly rate', 'number', { min: 0, decimal: true }),
      f('excludeUnknownSalary', 'Skip jobs that don\'t state pay', 'bool', { help: 'Off: they are kept, flagged, and get half salary credit.' }),
    ] },
    { group: 'Posting', key: 'filters', fields: [
      f('datePosted', 'Posted within', 'select', { options: choices([['PastDay', '24 hours'], ['PastWeek', 'Past week'], ['PastMonth', 'Past month'], ['Any', 'Any time']]) }),
      f('quickApplyOnly', 'Easy Apply jobs only', 'bool'), f('underTenApplicants', 'Under 10 applicants', 'bool'),
      f('maxApplicants', 'Skip jobs with more applicants than', 'number', { min: 0 }),
      f('sort', 'Sort LinkedIn results by', 'select', { options: choices([['MostRecent', 'Most recent'], ['MostRelevant', 'Most relevant']]) }),
      f('maxPagesPerTerm', 'Result pages per keyword', 'number', { min: 1, max: 20 }),
    ] },
    { group: 'Rule out', key: 'filters', fields: [
      f('companyBlocklist', 'Never these companies', 'tags'),
      f('mustHaveAnyTech', 'Must use at least one of', 'tags'),
      f('mustAvoidTech', 'Must not use', 'tags'),
      f('excludeStaffingAgencies', 'Skip staffing agencies', 'bool'),
    ] },
    { group: 'Best-fit list', fields: [
      f('scoreThreshold', 'Show jobs scoring at least', 'number', { min: 0, max: 100 }),
      f('maxDocumentsPerHunt', 'Write résumés for the top', 'number', { min: 1, max: 200, help: 'Caps how many jobs per search get tailored documents (LLM cost).' }),
    ] },
    { group: 'How the score is weighted', key: 'weights', help: 'Relative weights; they are normalized to 100.', fields: [
      f('requiredSkills', 'Required skills', 'number', { min: 0, decimal: true }), f('niceToHaveSkills', 'Nice-to-have skills', 'number', { min: 0, decimal: true }),
      f('titleAlignment', 'Title match', 'number', { min: 0, decimal: true }), f('salary', 'Salary', 'number', { min: 0, decimal: true }),
      f('workplace', 'Workplace', 'number', { min: 0, decimal: true }), f('employmentType', 'Employment type', 'number', { min: 0, decimal: true }),
      f('freshness', 'Freshness', 'number', { min: 0, decimal: true }),
    ] },
  ];

  var form = null;
  var dirty = false;

  JH.renderRequirements = function (search, preview) {
    var root = document.getElementById('requirements-form');
    var none = document.getElementById('requirements-none');
    none.hidden = !!search;
    root.hidden = !search;
    document.getElementById('requirements-actions').hidden = !search;
    if (!search) { root.textContent = ''; form = null; dirty = false; renderPreview([]); return; }
    form = JH.buildForm(root, SCHEMA, search, function () { dirty = true; document.getElementById('requirements-dirty').hidden = false; });
    dirty = false;
    document.getElementById('requirements-dirty').hidden = true;
    renderPreview(preview || []);
  };

  function renderPreview(preview) {
    var list = document.getElementById('requirements-preview');
    list.textContent = '';
    preview.forEach(function (p) {
      var open = JH.button('Show on LinkedIn', 'Show the ' + p.term + ' search on LinkedIn', function () {
        JH.post({ action: 'openPosting', url: p.url });
      });
      list.appendChild(el('li', {}, [el('span', { text: p.term }), ' ', open]));
    });
  }

  JH.collectRequirements = function () { return form ? form.collect() : null; };
  JH.requirementsIsDirty = function () { return dirty; };
})();
