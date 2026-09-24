// The Applicant tab: every field an application asks for, entered once. Sections are collapsible;
// the LinkedIn sign-in section is special because the password never travels with the profile —
// it is saved separately, encrypted, and only ever reported back as "saved" / "not saved".
(function () {
  'use strict';
  var JH = window.JH;
  var el = JH.el;

  var EMPLOYMENT = ['', 'Full-time', 'Part-time', 'Contract', 'Contract-to-hire', 'Temporary', 'Internship', 'Freelance', 'Self-employed'];
  var LEVELS = ['', 'Beginner', 'Intermediate', 'Advanced', 'Expert'];
  var PROFICIENCY = ['', 'Elementary', 'Limited working', 'Professional working', 'Full professional', 'Native or bilingual'];
  var DECLINE = "I don't wish to answer";

  function f(key, label, type, extra) {
    var o = { key: key, label: label, type: type || 'text' };
    Object.keys(extra || {}).forEach(function (k) { o[k] = extra[k]; });
    return o;
  }
  function opts(values) { return values.map(function (v) { return { value: v, label: v || '—' }; }); }
  function enumOpts(values) { return values.map(function (v) { return { value: v, label: v.replace(/([a-z])([A-Z])/g, '$1 $2') }; }); }

  // Sections: each is { id, title, nodes } rendered into its own <details>.
  var SECTIONS = [
    { id: 'contact', title: 'Name & contact', open: true, nodes: [
      { group: 'Name', key: 'contact', fields: [
        f('firstName', 'First name', 'text', { autocomplete: 'given-name' }), f('middleName', 'Middle name'),
        f('lastName', 'Last name', 'text', { autocomplete: 'family-name' }), f('suffix', 'Suffix'),
        f('preferredName', 'Preferred name'), f('pronouns', 'Pronouns'),
      ] },
      { group: 'Contact', key: 'contact', fields: [
        f('email', 'Email', 'email'), f('secondaryEmail', 'Secondary email', 'email'),
        f('phoneCountryCode', 'Country code'), f('phone', 'Phone', 'tel'),
        f('phoneType', 'Phone type', 'select', { options: opts(['Mobile', 'Home', 'Work']) }), f('secondaryPhone', 'Secondary phone', 'tel'),
      ] },
    ] },
    { id: 'address', title: 'Address', nodes: [
      { group: 'Address', key: 'contact', fields: [] },
    ] },
    { id: 'linkedin', title: 'LinkedIn sign-in', special: 'accounts' },
    { id: 'summary', title: 'Professional summary', nodes: [
      { group: 'Summary', key: 'summary', fields: [
        f('headline', 'Headline', 'text', { wide: true, help: 'One line, e.g. "Senior .NET engineer, 12 years in healthcare billing"' }),
        f('summary', 'Summary', 'textarea', { rows: 5 }),
        f('totalYearsExperience', 'Total years of experience', 'number', { min: 0 }),
        f('highestEducation', 'Highest education', 'select', { options: opts(['', 'High school', 'Associate', "Bachelor's", "Master's", 'Doctorate', 'Professional']) }),
      ] },
    ] },
    { id: 'experience', title: 'Work history', nodes: [
      { list: 'Work history', key: 'experience', item: 'Job',
        help: 'Most recent first. Bullets are the facts your tailored résumés are built from — keep each one true and specific.',
        title: function (x) { return [x.title, x.employer].filter(Boolean).join(' at '); },
        make: function () { return { bullets: [], technologies: [], mayContact: true }; },
        fields: [
          f('title', 'Job title'), f('employer', 'Employer'), f('location', 'Location'), f('wasRemote', 'Remote role', 'bool'),
          f('startDate', 'Start', 'month'), f('endDate', 'End (blank = current)', 'month'),
          f('employmentType', 'Employment type', 'select', { options: opts(EMPLOYMENT) }), f('industry', 'Industry'),
          f('summary', 'What the role was', 'textarea', { rows: 3 }),
          f('technologies', 'Technologies used', 'tags'),
          { list: 'Accomplishments', key: 'bullets', item: 'Bullet', title: function (b) { return (b.text || '').slice(0, 70); },
            make: function () { return { keywords: [] }; },
            fields: [f('text', 'Accomplishment', 'textarea', { rows: 2, help: 'Action verb + what you did + the result. Only real numbers.' }),
                     f('keywords', 'Keywords this shows', 'tags')] },
          f('reasonForLeaving', 'Reason for leaving'), f('finalAnnualSalary', 'Final annual salary', 'number', { min: 0 }),
          f('supervisorName', 'Supervisor name'), f('supervisorTitle', 'Supervisor title'),
          f('supervisorPhone', 'Supervisor phone', 'tel'), f('supervisorEmail', 'Supervisor email', 'email'),
          f('mayContact', 'May contact this employer', 'bool'),
        ] },
    ] },
    { id: 'education', title: 'Education', nodes: [
      { list: 'Education', key: 'education', item: 'School', title: function (x) { return [x.degree, x.fieldOfStudy, x.school].filter(Boolean).join(', '); },
        make: function () { return { graduated: true, coursework: [] }; },
        fields: [
          f('school', 'School'), f('degree', 'Degree'), f('fieldOfStudy', 'Field of study'), f('minor', 'Minor'),
          f('location', 'Location'), f('graduated', 'Graduated', 'bool'),
          f('startDate', 'Start', 'month'), f('endDate', 'End', 'month'),
          f('gpa', 'GPA'), f('honors', 'Honors'), f('activities', 'Activities', 'textarea', { rows: 2 }),
          f('coursework', 'Relevant coursework', 'tags'),
        ] },
    ] },
    { id: 'skills', title: 'Skills', nodes: [
      { list: 'Skills', key: 'skills', item: 'Skill', title: function (x) { return x.name ? x.name + ' (' + (x.kind || 'Hard') + ')' : ''; },
        help: 'Hard skills (languages, frameworks), soft skills (mentoring), tools, and business domains.',
        make: function () { return { kind: 'Hard', aliases: [] }; },
        fields: [
          f('name', 'Skill'), f('kind', 'Kind', 'select', { options: enumOpts(['Hard', 'Soft', 'Tool', 'Domain']) }),
          f('years', 'Years', 'number', { min: 0, step: 0.5 }), f('level', 'Level', 'select', { options: opts(LEVELS) }),
          f('lastUsed', 'Last used (year)'), f('featured', 'Always feature on my résumé', 'bool'),
          f('aliases', 'Also known as', 'tags', { help: 'Other names postings use, e.g. "dotnet", "ASP.NET Core"' }),
        ] },
    ] },
    { id: 'certifications', title: 'Certifications & licenses', nodes: [
      { list: 'Certifications', key: 'certifications', item: 'Certification', title: function (x) { return x.name; },
        fields: [f('name', 'Name'), f('issuer', 'Issuer'), f('issuedDate', 'Issued', 'month'), f('expiresDate', 'Expires', 'month'),
                 f('credentialId', 'Credential ID'), f('credentialUrl', 'Credential URL', 'url')] },
    ] },
    { id: 'projects', title: 'Projects', nodes: [
      { list: 'Projects', key: 'projects', item: 'Project', title: function (x) { return x.name; },
        make: function () { return { technologies: [] }; },
        fields: [f('name', 'Name'), f('role', 'Your role'), f('url', 'URL', 'url'),
                 f('startDate', 'Start', 'month'), f('endDate', 'End', 'month'),
                 f('description', 'Description', 'textarea', { rows: 3 }), f('technologies', 'Technologies', 'tags')] },
    ] },
    { id: 'accomplishments', title: 'Awards, publications, volunteering & more', nodes: [
      { list: 'Accomplishments', key: 'accomplishments', item: 'Entry', title: function (x) { return x.title ? x.title + ' (' + x.kind + ')' : ''; },
        make: function () { return { kind: 'Award' }; },
        fields: [f('kind', 'Kind', 'select', { options: enumOpts(['Award', 'Publication', 'Patent', 'Volunteer', 'Speaking', 'Military', 'Other']) }),
                 f('title', 'Title'), f('organization', 'Organization'), f('date', 'Date', 'month'), f('url', 'URL', 'url'),
                 f('description', 'Description', 'textarea', { rows: 2 })] },
    ] },
    { id: 'languages', title: 'Languages', nodes: [
      { list: 'Languages', key: 'languages', item: 'Language', title: function (x) { return x.language; },
        fields: [f('language', 'Language'), f('proficiency', 'Proficiency', 'select', { options: opts(PROFICIENCY) })] },
    ] },
    { id: 'references', title: 'References', nodes: [
      { list: 'References', key: 'references', item: 'Reference', title: function (x) { return x.name; },
        fields: [f('name', 'Name'), f('relationship', 'Relationship'), f('company', 'Company'), f('title', 'Their title'),
                 f('email', 'Email', 'email'), f('phone', 'Phone', 'tel'), f('yearsKnown', 'Years known', 'number', { min: 0 })] },
    ] },
    { id: 'links', title: 'Links', nodes: [
      { list: 'Links', key: 'links', item: 'Link', title: function (x) { return x.label; },
        help: 'GitHub, portfolio, personal site, Stack Overflow — anything you want applications to link to.',
        fields: [f('label', 'Label'), f('url', 'URL', 'url')] },
    ] },
    { id: 'authorization', title: 'Work authorization & background', nodes: [
      { group: 'Authorization', key: 'authorization', fields: [
        f('authorizedCountries', 'Authorized to work in', 'tags'),
        f('requiresSponsorship', 'Will need visa sponsorship', 'bool'), f('citizenship', 'Citizenship'),
        f('visaStatus', 'Visa status'), f('workPermitExpires', 'Work permit expires', 'month'),
        f('securityClearance', 'Security clearance'), f('clearanceActive', 'Clearance is active', 'bool'),
        f('isOver18', 'I am 18 or older', 'bool'), f('hasDriversLicense', "I have a driver's license", 'bool'),
        f('hasReliableTransportation', 'Reliable transportation', 'bool'),
        f('consentsToBackgroundCheck', 'Consent to a background check', 'bool'), f('consentsToDrugTest', 'Consent to a drug test', 'bool'),
        f('hasNonCompete', 'Bound by a non-compete', 'bool'), f('nonCompeteDetails', 'Non-compete details'),
        f('previouslyEmployedAtTarget', 'Previously worked at companies I apply to', 'bool'),
      ] },
    ] },
    { id: 'compensation', title: 'Compensation', nodes: [
      { group: 'Pay', key: 'compensation', fields: [
        f('currency', 'Currency'), f('currentAnnualSalary', 'Current salary (yearly)', 'number', { min: 0 }),
        f('desiredAnnualSalary', 'Desired salary (yearly)', 'number', { min: 0 }), f('minimumAnnualSalary', 'Minimum salary (yearly)', 'number', { min: 0 }),
        f('desiredHourlyRate', 'Desired hourly rate', 'number', { min: 0, decimal: true }), f('minimumHourlyRate', 'Minimum hourly rate', 'number', { min: 0, decimal: true }),
        f('openToEquity', 'Open to equity', 'bool'), f('notes', 'Notes', 'textarea', { rows: 2 }),
      ] },
    ] },
    { id: 'preferences', title: 'What I want next', nodes: [
      { group: 'Preferences', key: 'preferences', help: 'Your first job requirements are built from these.', fields: [
        f('desiredTitles', 'Job titles I want', 'tags'),
        f('wantsRemote', 'Remote', 'bool'), f('wantsHybrid', 'Hybrid', 'bool'), f('wantsOnSite', 'On-site', 'bool'),
        f('wantsFullTime', 'Full-time', 'bool'), f('wantsContract', 'Contract', 'bool'),
        f('wantsContractToHire', 'Contract-to-hire', 'bool'), f('wantsPartTime', 'Part-time', 'bool'),
        f('minimumContractMonths', 'Shortest contract I will take (months)', 'number', { min: 0 }),
        f('willingToRelocate', 'Willing to relocate', 'bool'), f('relocationLocations', 'Would relocate to', 'tags'),
        f('maxTravelPercent', 'Max travel (%)', 'number', { min: 0, max: 100 }), f('maxCommuteMiles', 'Max commute (miles)', 'number', { min: 0 }),
        f('preferredTimeZones', 'Time zones I can work in', 'tags'),
        f('preferredIndustries', 'Industries I prefer', 'tags'), f('avoidIndustries', 'Industries to avoid', 'tags'),
        f('preferredCompanySizes', 'Company sizes', 'tags'), f('companyBlocklist', 'Never apply to', 'tags'),
        f('noticePeriodWeeks', 'Notice period (weeks)', 'number', { min: 0 }), f('earliestStartDate', 'Earliest start', 'text', { placeholder: 'e.g. 2 weeks after offer' }),
        f('canWorkShifts', 'Can work shifts', 'bool'), f('canWorkWeekends', 'Can work weekends', 'bool'), f('canBeOnCall', 'Can be on call', 'bool'),
      ] },
    ] },
    { id: 'keywords', title: 'My keyword lists', nodes: [
      { list: 'Keyword lists', key: 'keywordLists', item: 'List', title: function (x) { return x.name; },
        help: 'Any list you want kept: domains you know, methodologies, words recruiters should see.',
        make: function () { return { keywords: [] }; },
        fields: [f('name', 'List name'), f('keywords', 'Keywords', 'tags')] },
    ] },
    { id: 'textblocks', title: 'My text blocks', nodes: [
      { list: 'Text blocks', key: 'textBlocks', item: 'Block', title: function (x) { return x.title; },
        help: 'Reusable writing: an elevator pitch, a leadership story, why you are looking. Cover letters and open questions draw on these.',
        fields: [f('title', 'Title'), f('body', 'Text', 'textarea', { rows: 5 })] },
    ] },
    { id: 'custom', title: 'Anything else', nodes: [
      { list: 'Custom fields', key: 'customFields', item: 'Field', title: function (x) { return x.label; },
        help: 'A label and a value for anything a form might ask that nothing above covers.',
        fields: [f('label', 'Label'), f('value', 'Value')] },
    ] },
    { id: 'answers', title: 'Saved screening answers', nodes: [
      { list: 'Answers', key: 'answers', item: 'Answer', title: function (x) { return x.question; },
        help: 'Answers to questions applications ask. Only confirmed answers are used without asking you.',
        fields: [f('question', 'Question', 'text', { wide: true }), f('answer', 'Answer', 'textarea', { rows: 2 }), f('confirmed', 'Confirmed', 'bool')] },
    ] },
    { id: 'disclosures', title: 'Voluntary self-identification', nodes: [
      { group: 'US EEO questions (optional)', key: 'disclosures', help: 'Everything defaults to "I don\'t wish to answer".', fields: [
        f('gender', 'Gender'), f('raceEthnicity', 'Race / ethnicity'), f('veteranStatus', 'Veteran status'),
        f('disabilityStatus', 'Disability status'), f('sexualOrientation', 'Sexual orientation'),
      ] },
    ] },
  ];
  // The address lives inside contact.address.
  SECTIONS[1].nodes = [{ group: 'Address', key: 'contact', fields: [
    { group: 'Mailing address', key: 'address', fields: [
      f('street1', 'Street', 'text', { autocomplete: 'address-line1', wide: true }), f('street2', 'Apt / suite', 'text', { autocomplete: 'address-line2' }),
      f('city', 'City'), f('state', 'State / province'), f('postalCode', 'ZIP / postal code'), f('county', 'County'), f('country', 'Country'),
    ] },
  ] }];

  function blankProfile() {
    return {
      id: 0, contact: { phoneCountryCode: '+1', phoneType: 'Mobile', address: { country: 'United States' } },
      authorization: { authorizedCountries: ['United States'], isOver18: true, hasDriversLicense: true, hasReliableTransportation: true, consentsToBackgroundCheck: true, consentsToDrugTest: true },
      compensation: { currency: 'USD', openToEquity: true },
      preferences: { desiredTitles: [], wantsRemote: true, wantsFullTime: true, wantsContract: true, wantsContractToHire: true, noticePeriodWeeks: 2 },
      summary: {}, disclosures: { gender: DECLINE, raceEthnicity: DECLINE, veteranStatus: DECLINE, disabilityStatus: DECLINE, sexualOrientation: DECLINE },
      boardAccounts: [], links: [], experience: [], education: [], skills: [], certifications: [], languages: [], projects: [],
      accomplishments: [], references: [], keywordLists: [], textBlocks: [], customFields: [], answers: [],
    };
  }
  JH.blankProfile = blankProfile;

  var forms = [];
  var working = null;
  var dirty = false;

  /** Renders the whole Applicant tab for `profile` (null = no applicant yet). */
  JH.renderApplicant = function (profile, passwordSaved, boards) {
    var root = document.getElementById('applicant-sections');
    // Re-rendering the same applicant (after a save) keeps the sections the user had open.
    var sameApplicant = working && profile && working.id && working.id === profile.id;
    var wasOpen = {};
    if (sameApplicant) root.querySelectorAll('details.section').forEach(function (d) { wasOpen[d.id] = d.open; });
    root.textContent = '';
    forms = [];
    dirty = false;
    setDirty(false);
    working = JH.clone(profile || blankProfile());
    document.getElementById('applicant-title').textContent =
      working.id ? (displayName(working) || 'Applicant') : 'New applicant';

    SECTIONS.forEach(function (s) {
      var details = el('details', { class: 'section', id: 'sec-' + s.id });
      var key = 'sec-' + s.id;
      if (sameApplicant && key in wasOpen) details.open = wasOpen[key];
      else if (s.open || (!working.id && (s.id === 'contact' || s.id === 'linkedin'))) details.open = true;
      details.appendChild(el('summary', { text: s.title }));
      var body = el('div', { class: 'section-body' });
      details.appendChild(body);
      root.appendChild(details);
      if (s.special === 'accounts') renderAccounts(body, passwordSaved || {}, boards || []);
      // Every section edits the same working record, so two sections touching one object
      // (name and address both live in `contact`) can never overwrite each other.
      else forms.push(JH.buildForm(body, s.nodes, working, function () { setDirty(true); }, true));
    });
  };

  function displayName(p) {
    var c = p.contact || {};
    return [c.firstName, c.lastName].filter(Boolean).join(' ') || c.email || '';
  }

  // Signed-in state of each board pane, pushed by the host as the pane navigates.
  JH.boardStatus = JH.boardStatus || {};
  JH.setBoardStatus = function (boardId, signedIn) {
    JH.boardStatus[boardId] = signedIn;
    var line = document.getElementById('acct-status-' + boardId);
    if (line) paintStatus(line, boardId);
  };
  function paintStatus(line, boardId) {
    var signedIn = JH.boardStatus[boardId];
    var name = line.dataset.board;
    line.textContent = signedIn === true
      ? '✓ Signed in to ' + name + ' in the pane on the right. JobHunt remembers this session for this applicant.'
      : signedIn === false
        ? 'Not signed in. Sign in in the ' + name + ' pane on the right with Google, Apple or your email. JobHunt remembers the session.'
        : 'Checking whether the ' + name + ' pane is signed in…';
    line.className = 'acct-status' + (signedIn === true ? ' ok' : '');
  }

  function renderAccounts(body, passwordSaved, boards) {
    boards.forEach(function (b) {
      var account = (working.boardAccounts || []).find(function (a) { return a.boardId === b.id; });
      if (!account) { account = { id: 0, boardId: b.id, signInEmail: '', autoSignIn: true }; working.boardAccounts.push(account); }

      // 1. The normal way: sign in in the pane (Google, Apple, email — whatever the account uses).
      var status = el('p', { id: 'acct-status-' + b.id, role: 'status', 'data-board': b.name });
      paintStatus(status, b.id);
      body.appendChild(status);
      var open = JH.button('Open ' + b.name + ' sign-in', null, function () { JH.post({ action: 'openSignIn', boardId: b.id }); });
      open.disabled = !working.id;
      if (!working.id) open.title = 'Save the applicant first';
      body.appendChild(el('div', { class: 'actions' }, [open]));

      // 2. Optional: an email + password JobHunt can re-enter if the session ever expires. Only for
      //    accounts that have a LinkedIn password — Google/Apple sign-ins don't.
      var emailId = 'acct-email-' + b.id, pwId = 'acct-pw-' + b.id, autoId = 'acct-auto-' + b.id;
      var email = el('input', { id: emailId, type: 'email', autocomplete: 'username' });
      // 3.3.7 Redundant Entry: a LinkedIn password account usually signs in with the email above.
      if (!account.signInEmail && working.contact && working.contact.email) account.signInEmail = working.contact.email;
      email.value = account.signInEmail || '';
      email.addEventListener('input', function () { account.signInEmail = email.value; setDirty(true); });
      var auto = el('input', { id: autoId, type: 'checkbox' });
      auto.checked = account.autoSignIn !== false;
      auto.addEventListener('change', function () { account.autoSignIn = auto.checked; setDirty(true); });

      // 3.3.8 Accessible Authentication: paste and password managers work (no autocomplete=off).
      var pw = el('input', { id: pwId, type: 'password', autocomplete: 'current-password', placeholder: passwordSaved[b.id] ? '•••••••• (saved)' : 'Password' });
      var savePw = JH.button('Save password', null, function () {
        if (!working.id) { JH.toast('Save the applicant first, then add the password.', true); return; }
        if (!pw.value) { pw.focus(); return; }
        JH.post({ action: 'saveBoardPassword', boardId: b.id, password: pw.value });
        pw.value = '';
      });
      var forget = JH.button('Forget password', null, function () { JH.post({ action: 'forgetBoardPassword', boardId: b.id }); });
      forget.disabled = !passwordSaved[b.id];
      var signIn = JH.button('Sign in now', null, function () { JH.post({ action: 'signInNow', boardId: b.id }); });
      signIn.disabled = !working.id || !passwordSaved[b.id];

      var optional = el('details', { class: 'optional' }, [
        el('summary', { text: 'Optional: sign in automatically with a ' + b.name + ' email and password' }),
        el('p', { class: 'muted help', text: 'Only for accounts with a ' + b.name + ' password. If you sign in with Google or Apple, skip this: the session in the pane is all JobHunt needs.' }),
        el('div', { class: 'form-grid' }, [
          el('div', { class: 'field' }, [el('label', { for: emailId, text: b.name + ' email or phone' }), email]),
          el('div', { class: 'field' }, [el('label', { for: pwId, text: b.name + ' password' }), pw]),
        ]),
        el('label', { class: 'check', for: autoId }, [auto, ' Sign in automatically if the session expires']),
        el('div', { class: 'actions' }, [savePw, forget, signIn]),
        el('p', { class: 'muted help', text:
          (passwordSaved[b.id] ? 'A password is saved, encrypted to your Windows account. ' : '') +
          'A password is never stored in the JobHunt database or any export. If ' + b.name + ' asks for a verification code or a security check, JobHunt stops and leaves it to you.' }),
      ]);
      optional.open = !!passwordSaved[b.id] || !!account.signInEmail && account.signInEmail !== (working.contact && working.contact.email);
      body.appendChild(optional);
    });
  }

  function setDirty(on) {
    dirty = on;
    var bar = document.getElementById('applicant-dirty');
    if (bar) bar.hidden = !on;
  }

  /** The edited profile, ready to send. */
  JH.collectApplicant = function () {
    var out = JH.clone(working);
    out.boardAccounts = out.boardAccounts.filter(function (a) { return a.signInEmail || a.id; });
    return out;
  };

  JH.applicantIsDirty = function () { return dirty; };
})();
