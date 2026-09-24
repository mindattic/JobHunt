# JobHunt Résumé and Cover-Letter Guide

The rules JobHunt follows when it generates a tailored résumé (.docx) and cover letter (.docx) from the
applicant profile. Each rule is written so it can become an LLM prompt instruction, a code check, or
both. Citations like [12] refer to the numbered **Sources** list at the end. Research date: 2026-09-23.

---

## Summary

1. Build one plain, single-column Word document: no tables, text boxes, columns, graphics, icons, or header/footer content. Every ATS vendor and career office consulted agrees on this [2][13][19][21][22][24].
2. Order: contact block, headline/summary, skills, experience (reverse-chronological), then projects, education, certifications. For an entry-level applicant, move education up [12][24].
3. Keep it to 1 page for under about 10 years of experience and 2 pages at most otherwise. Sources disagree on this; see Disagreements [13][15][24][29].
4. Every bullet starts with an action verb and says what was done and what came of it. Numbers come only from the profile [12][14][19].
5. Tailor every document: use the posting's exact job title and hard-skill terms where the profile honestly supports them. Don't keyword-stuff [16][19][24][30].
6. Recruiters, not bots, make most rejections. Most ATS setups rank and filter rather than auto-reject, so the document has to be easy to skim [10][16].
7. Omit photo, age/date of birth, marital status, gender, religion, salary history, references, and "References available on request" (US norms) [12][15].
8. Cover letter: 250–400 words, 3–4 paragraphs, addressed to a named person if one is known. It adds evidence and motivation instead of repeating the résumé [30][31][33][34].
9. Tailored cover letters beat generic ones. The ResumeGo field experiment found a 16.4% callback rate for tailored letters vs 10.7% with no letter [28].
10. Never fabricate. Inaccurate claims are what tech hiring managers object to most in AI-assisted applications [35].

---

## Résumé rules

Each rule has an ID (R-n) so code checks and prompt text can refer to it.

### Structure and length

**R-1. Reverse-chronological order (or a hybrid), never purely functional.**
List experience with the most recent first. A "hybrid" means a skills summary on top of a full chronological work history, which is still allowed.
*Why:* Parsers and recruiters need to see when and where each skill was used. Functional résumés hide that and read as concealing gaps.
[1][3][7][12]

**R-2. Section order (default, experienced candidate):**
1. Contact block
2. Headline + Summary
3. Skills
4. Professional Experience
5. Projects
6. Education
7. Certifications
8. Optional: Awards / Publications / Volunteer

If the applicant has under about 3 years of experience, or is a student, put Education right after Skills.
*Why:* Order sections by relevance to the employer. Tech Interview Handbook recommends this exact order for software roles and moving education up for early-career candidates.
[12][13][24]

**R-3. Length.**
- Target 1 page when total professional experience is under about 10 years.
- Allow 2 pages when experience is 10+ years or the applicant has an advanced degree.
- Never exceed 2 pages.
- Code check: page count after render.

*Why:* MIT recommends one page unless the applicant has extensive experience or an advanced degree. Tech Interview Handbook says 1 page for software engineers. ResumeGo's simulation found recruiters preferred 2 pages, more strongly at mid and managerial levels. A hard cap of 2 is the consensus ceiling.
[13][15][24][29]

**R-4. Keep roughly the last 15 years of experience.**
Summarize or drop older positions unless they are uniquely relevant to the posting.
*Why:* TopResume lists "outdated positions (beyond 15 years)" as a common mistake. Old dates can also invite age bias.
[20][36]

**R-5. Put a name on page 2.**
If the résumé runs to 2 pages, repeat the name at the top of page 2 as body text, not in a Word header.
[13][21]

### Contact block

**R-6. Contact block contents.** Include exactly:
- Full name (bold, largest text on the page)
- City and state/region (no street address)
- Phone
- Professional email
- LinkedIn URL
- Optionally GitHub or portfolio URL, for tech roles

Label items with text ("Email:"), not icons. Never use a work phone or work email from a current employer.
*Why:* Missing email and phone is one of Harvard's "top five mistakes." Icons can turn into garbage characters when parsed. A full street address is unnecessary and is a bias vector.
[6][7][9][12][13][21][24]

**R-7. The contact block is body text.**
Never put it in the Word header or footer. Code check: the header and footer parts contain no name, email, or phone.
*Why:* Several ATS parsers skip header and footer content.
[21][22]

### Headline and summary

**R-8. Headline.**
One line under the contact block, fewer than 10 words. It starts with a role noun, and uses the posting's job title when the profile truthfully supports it. Example: "Senior Software Engineer – Distributed Systems".
*Why:* Jobscan reports that résumés containing the job title got 10.6× more interview invitations. That is a vendor statistic, so treat it as directional. SHRM recommends a branded headline stating professional identity.
[5][19][24]

**R-9. Summary.**
- 2–4 sentences, under 50 words (Tech Interview Handbook's limit).
- Written in the implied first person, with no "I/me/my".
- Tailored to the posting.
- Never a generic "Objective" statement.

*Why:* TopResume reports that 47% of résumés use a generic objective instead of a tailored summary (vendor data). Summaries must be tailored to be worth the space.
[7][24][36]

### Experience bullets

**R-10. Header line for each role.**
Show job title, employer, city/state (or "Remote"), and start–end dates. Keep it on one line or two lines, formatted the same way for every role. Put the title or employer in bold.
*Why:* The Ladders eye-tracking study found bold job titles and clear headers drew recruiters' attention.
[10][13][24]

**R-11. Number of bullets per role.**

| Role | Bullets |
|---|---|
| Most recent or most relevant | 3–6 |
| Older roles | 1–3 |
| Roles older than about 10 years | 0–2 |

Each bullet is 1–2 lines, with no paragraphs.
*Why:* MIT says bullets should be 1–2 lines. Enhancv's recruiters said short bullets beat paragraphs (72% of 25 interviewed). Tailero suggests 3–5 bullets per role.
[7][14][16]

**R-12. Bullet formula: Action verb + what/context + result.**
Use the "XYZ" pattern where possible: *accomplished X, as measured by Y, by doing Z*. Variants are PAR (Problem/Project, Action, Result) and CAR.
Code check: the first token is a verb, and it is not "Responsible", "Helped", "Worked on", or "Assisted with".
*Why:* Harvard, MIT, and Tech Interview Handbook all prescribe action-verb bullets that show results. The XYZ wording is widely attributed to former Google SVP Laszlo Bock; the primary source was not accessed.
[12][14][24]

**R-13. Quantify only with numbers that are in the profile.**
If the profile gives a metric (%, $, users, latency, team size, count), use it. If it doesn't, describe scope in words using profile facts ("for the company's largest client", "across 3 teams" only if the 3 is in the profile). **Never estimate, round up, or invent a number.**
*Why:* Recruiters value quantified results. A CareerBuilder figure found in search results says 34% treat bullets without them as non-starters; that figure was not verified at its primary source. Fabricated metrics, however, get exposed in interviews and cause rescinded offers.
[4][5][14][35]

**R-14. Tense and person.**
- Current role: present tense.
- Past roles: past tense.
- No personal pronouns (I, me, my, we, our).
- No passive voice.
- One consistent person throughout.

Code check: regex for `\b(I|me|my|we|our)\b` in résumé body.
[12][13][14][36]

**R-15. Don't repeat the same leading verb.**
The same leading verb should not appear more than twice in the résumé, and never twice within one role. Code check: count first tokens.
*Why:* Harvard advises against repetition. TopResume flags repetitive language.
[12][36]

**R-16. Show tech context in engineering bullets.**
Name the specific technologies used in the bullet itself ("…migrated billing to Kafka and Go…"), not only in Skills.
*Why:* Tech Interview Handbook says to pepper keywords through experience, not only the Skills section. Recruiters search skills and need to see when and where each was used.
[3][19][24]

### Skills, education, and other sections

**R-17. Skills section.**
- Use a few labeled lines, for example "Languages: C# | TypeScript | SQL", "Frameworks: …", "Cloud/Tools: …".
- Put hard skills only. Leave soft skills out of the list and demonstrate them in bullets instead.
- Write proficiency as text if at all ("Java (Expert)"), never as bars or stars.

*Why:* Tech Interview Handbook specifies this format. Graphic skill bars don't parse.
[21][24]

**R-18. Acronyms.**
The first time a key term appears, spell it out and add the acronym: "Amazon Web Services (AWS)", "Continuous Integration/Continuous Delivery (CI/CD)". After that, the short form is fine. Exception: universally known terms (SQL, HTML, API) may stay short.
*Why:* Recruiters and ATS searches may use either form.
[1][2][5][22][24]

**R-19. Education.**
Show degree name, institution (spelled out), and graduation month/year. Add GPA only if at least 3.5 and the degree is within about 3 years.
Graduation dates older than about 15 years may be omitted if the profile allows. This is a user setting, not an LLM decision.
[13][20][24]

**R-20. Certifications.**
Give the full official name plus the acronym and the year. Include only certifications that are current or relevant.
[5][13]

**R-21. Projects.**
Include a project when it demonstrates skills the posting requires and the job history doesn't cover. Use the same bullet rules. Link to the repo or demo if the profile has one.
[15][24][26]

### What to omit (US/Canada/UK default)

**R-22. Never include:**
- Photo
- Age or date of birth
- Gender, marital status, religion, health, or nationality (unless work authorization is relevant)
- Full street address
- Salary history or expectations
- A list of references
- The line "References available upon request"
- Hobbies, unless directly relevant to the job

Code check: a blocklist of these phrases and fields.
*Why:* Harvard: "Don't include a picture," "Don't include age or gender," "Don't list references." MIT: employers assume references are available. SHRM's blind-hiring coverage notes that names, addresses, graduation dates, and hobbies can trigger bias.
[6][9][12][15]

**R-23. Work authorization.**
Include a work-authorization line only if the profile provides one and the posting mentions sponsorship or citizenship. An example is "U.S. Citizen" for a clearance role.
*Why:* Knockout questions about authorization are universal (100% of Enhancv's recruiters), so the résumé rarely needs this line.
[13][16]

**R-24. International postings.**
If the posting is for a country with different norms (for example, parts of the EU or Asia, where photos and birth dates are common), don't auto-add personal data. Flag it to the user instead.
[1][6]

---

## ATS formatting rules for .docx

The generator writes .docx files directly, so each of these rules maps to OpenXML output.

**A-1. Single-column body only.**
Don't use `w:tbl` for layout, text boxes (`w:txbxContent`, drawing shapes), multiple columns (`w:cols` with num > 1), or floating frames.
*Why:* Tables, columns, and text boxes scramble reading order. Textkernel explains that left-to-right rendering mixes the content of different columns together.
[2][18][19][21][22]
*Code check:* the document.xml body contains no `w:tbl`, `w:txbxContent`, or `wp:anchor`, and `w:cols@w:num` is absent or 1.

**A-2. No content in headers or footers.**
Leave `header*.xml` and `footer*.xml` empty, or limit them to a page number on page 2+. Nothing else goes there.
[21][22]

**A-3. No images, charts, icons, emoji, or skill bars.**
[1][2][21]
*Code check:* no `w:drawing` or `w:pict`, and no characters in emoji or symbol Unicode blocks.

**A-4. Fonts.** Use one standard font throughout.

| Setting | Value |
|---|---|
| Font | Calibri (default) or Arial; also acceptable: Cambria, Georgia, Garamond, Helvetica, Verdana, Times New Roman |
| Body size | 10.5–11 pt, never below 10 pt |
| Section headings | 12–14 pt |
| Name | 14–18 pt |

Embed no custom fonts.
[1][2][13][21][22][24]

**A-5. Margins.** 0.5–1.0 inch on all sides; default 0.75 in. [13][15][22][24]

**A-6. Standard section headings.**
Each heading is a real paragraph, styled with a Word Heading style (Heading 1/2), plain text, and no symbols. Use these literal strings:
- Summary
- Skills (or Technical Skills)
- Professional Experience (or Work Experience / Experience)
- Projects
- Education
- Certifications

Never use creative names like "My Journey" or "Toolkit".
[2][19][21][22][24]

**A-7. Dates.**
- Use one format throughout, default `MMM YYYY – MMM YYYY` (e.g., `Jan 2021 – Mar 2023`), and `Present` for the current role.
- Never use apostrophe years (`'21`) or day-month-year, and never mix formats.
- Put dates on the role's header line, not at the start of each bullet. Harvard: "Don't start each line with a date."

Code check: every date matches one regex.
[12][20][21][22]

**A-8. Bullets.**
Use Word's native list numbering (`w:numPr`) with a plain round bullet (•) or hyphen. Don't use custom glyphs, arrows, or checkmarks.
[21][22]

**A-9. Emphasis.**
Use bold for the name, section headings, and job titles/employers. Use italics sparingly. Don't underline (MIT) and don't use colored text beyond one dark accent.
[10][13][22]

**A-10. Hyperlinks.**
Write URLs out as visible text (`linkedin.com/in/name`), not hidden behind anchor text. Parsers and printed copies both lose hidden link targets.
[17][24]

**A-11. File format.**
Produce .docx (the app's native output) and offer an optional PDF export from the same document. Greenhouse accepts .doc, .docx, .pdf, .rtf, and .txt; see Disagreements for .docx vs PDF.
[2][17][19][22]

**A-12. File size.** Keep files small (well under 2 MB). No embedded media.
*Why:* A secondary source reports that Greenhouse won't parse files over 2.5 MB. That limit was not in Greenhouse's page; Greenhouse documents a 100 MB upload limit.
[17]

**A-13. File name.**
- Résumé: `FirstName-LastName-Résumé.docx`
- Cover letter: `FirstName-LastName-Cover-Letter.docx`

**Product decision:** the owner's spec is `Résumé` with the accented é. That overrides the sources' ASCII-only advice [23]. NTFS, Word and LinkedIn's upload all handle it, and `DocumentNaming` has a test for it. Otherwise follow the sources: use letters, digits and hyphens, keep the name short, and never add "final", "v2", dates or version numbers. The name comes from the profile, with accents kept (e.g. `Zoë-Núñez-Résumé.docx`).
[7][23]

**A-14. Document properties.**
Set the core properties (`dc:title`, `dc:creator`) to the applicant's name and "Résumé". Clear any template author names.
*Why:* The Pragmatic Engineer notes that people have sent templates still carrying placeholder identity. This is inference plus hygiene.
[25]

**A-15. Text must be real, selectable text,** not an image and not WordArt, so it can be highlighted and parsed.
[18][24]

---

## Tailoring rules

These rules govern how the LLM adapts the résumé to a specific posting.

**T-1. Extract from the job description (JD).** Pull out:
- the exact job title
- must-have hard skills
- nice-to-have skills
- required certifications and degree
- years of experience
- domain terms

Rank them by frequency and position; the requirements section is weighted highest.
*Why:* Recruiters search and rank by JD skills first (76.4% per Jobscan's survey), then education, title, certifications, and years.
[19][24]

**T-2. Match only what the profile supports.**
For each JD term, find profile evidence: a skill list entry, a bullet, a project, or a certification. Terms with evidence may be used, using the JD's spelling ("Kubernetes" not "k8s"). Terms without evidence must not appear anywhere. The validator enforces this.
*Why:* Recruiters value natural keyword use. Fabrication is the top red flag.
[16][35]

**T-3. Placement.**
Put the JD's title in the headline (if truthful; see T-6). Put must-have skills in both the Skills section and at least one bullet that shows them in use. Reflect the top two or three JD themes in the summary.
[2][5][19][24]

**T-4. Selection over rewriting.**
Tailoring mostly means *choosing* which bullets, projects, and skills to include and in what order. Put the most JD-relevant bullets first within each role. Drop irrelevant bullets rather than twisting them.
*Why:* "Look at the skills required for the position and select experiences where you demonstrated those skills" (MIT).
[15][30][36]

**T-5. Rewording limits.**
The LLM may:
- change verb choice
- reorder clauses
- substitute the JD's term for an equivalent term the profile uses ("REST APIs" for "web services" if both describe the same work)
- merge or trim bullets

It may not:
- change numbers, dates, titles, employers, degrees, or scope (team size, ownership level, "led" vs "contributed")
- add technologies not tied to that role in the profile

**T-6. Job titles are facts.**
Never rename a past title to match the JD. The *headline* may use the target role phrased as the applicant's professional identity only if the profile's titles or level are consistent with it. For example, "Software Engineer" can become "Backend Software Engineer" if the profile's work was backend. A title can't be inflated to "Senior" or "Lead" unless the profile says so.

**T-7. No keyword stuffing.**
- A JD term should appear at most about 3 times in the résumé.
- No hidden or white text.
- No block pasted from the JD.
- No skills list longer than about 25 items.

*Why:* Indeed, Jobscan, and Tech Interview Handbook all warn against stuffing. 76% of Enhancv's interviewed recruiters cite natural keyword use.
[2][16][19][24]

**T-8. Both forms of key acronyms** (see R-18), so either search form matches. [2][22]

**T-9. Use the posting's language for soft requirements too.** If the JD says "cross-functional collaboration," a profile bullet showing that is phrased with that term, but it stays demonstrated by an action rather than asserted as an adjective. [30][33]

**T-10. Match score is a hint, not a target.**
The app may compute a keyword-coverage score for the user. It must never push coverage up by adding unsupported terms. Jobscan suggests 75%+ as a target, but it is a vendor heuristic, and most recruiters distrust match scores. See Disagreements.
[16][19]

---

## Cover-letter rules

**C-1. Length.** 250–400 words, never over one page, 3–4 body paragraphs. Code check: word count. [30][31][33][34]

**C-2. Format.**
- Business-letter layout: applicant contact block, date, and the employer name and address if known.
- Same font and margins as the résumé.
- Left-aligned text with no paragraph indents and a blank line between paragraphs.
- Same ATS rules as the résumé: no tables or text boxes, and the contact block not in the header.

[30][33][34]

**C-3. Salutation.**
Use "Dear [Name]:" if the job record includes a hiring-manager or recruiter name. Otherwise use "Dear Hiring Manager:" or "Dear [Team] Hiring Team:". Never "To Whom It May Concern" or "Dear Sir/Madam".
[31][33]

**C-4. Paragraph 1: the hook.**
Name the exact position and company. Give a one-sentence thesis of why the applicant fits, built from their top 1–2 matched qualifications. If the profile or job record contains a referral or connection, name it in the first sentence or two. Don't open with "My name is…" or "I am writing to apply for…" as the whole first sentence.
[30][32][33][37]

**C-5. Paragraphs 2–3: evidence.**
Pick 2–3 JD requirements. For each, tell one concrete example from the profile (situation, action, result, with profile numbers only). End each paragraph by explicitly tying it to the role: "don't assume the reader will make the connection." Don't copy résumé bullets verbatim; expand one or two into a short story.
[31][32][33]

**C-6. Company-specific paragraph (or sentence).**
Show why *this* company: its product, mission, or challenge, as stated in the job posting or company data the app has. Frame it as what the applicant can contribute, not what the job gives them.
*Guardrail:* only use company facts present in the job record. Don't invent company news.
[30][33][37]

**C-7. Closing.**
Restate fit in one sentence, thank the reader, and invite a conversation or interview. Then "Sincerely," and the typed full name.
[31][32][33]

**C-8. Tone.**
- Professional and specific.
- Confident without superlatives ("I am the best candidate…").
- No flattery of the company that isn't grounded in a stated fact.
- First person is expected here, unlike the résumé.
- Vary sentence openings so no more than about 30% of sentences start with "I".

This is a heuristic, not a sourced figure.
[12][30]

**C-9. Always tailored.**
Each letter must name the company and role and cite at least two JD requirements.
*Why:* Generic letters are among the factors hiring professionals say hurt applicants, and 78% of recruiters claim they can spot them (the latter figure is from secondary summaries of the survey, not the primary page).
[28][33]

**C-10. Never mention:**
- salary (unless the posting requires it)
- reasons for leaving past jobs
- weaknesses or apologies ("Although I lack…")
- personal details covered by R-22
- that AI wrote it

Don't raise gaps unless the user supplies an explanation in the profile.
[7][12][35]

---

## Things to never do

**N-1. Never fabricate.**
No invented employers, titles, dates, degrees, certifications, skills, technologies, metrics, team sizes, awards, or company facts. No rounding up numbers, and no upgrading "contributed" to "led". Every factual claim in both documents must trace to a profile field; the code validator enforces this.
*Why:* IEEE-USA reports that about 5–10% of job offers are rescinded for résumé misrepresentation. Interviews exist to verify claims.
[35]

**N-2. Never use hidden text,** white-on-white keywords, or tiny-font JD dumps. [2][19]

**N-3. Never use a functional-only format** to hide chronology. [3]

**N-4. Never use first-person pronouns in the résumé** (R-14). [12][13][14]

**N-5. Never start a bullet with a weak phrase:** "Responsible for", "Duties included", "Helped with", "Worked on", "Assisted in", "Tasked with". [12][14][19]

**N-6. Never use clichés or empty buzzwords without evidence.**
Blocklist for the résumé and cover letter, matched case-insensitively:
- team player
- detail-oriented
- results-driven / results-oriented
- hard worker / hardworking
- go-getter
- self-starter
- dynamic
- synergy / synergize
- think outside the box
- people person
- go-to person
- proven track record (without evidence)
- best of breed
- value add
- motivated
- passionate (as a stand-alone adjective)
- rockstar / ninja / guru
- wear many hats
- References available upon request

Also block these common LLM tells: delve, realm, intricate, showcasing, pivotal, tapestry, leverage (as a verb, max once), spearheaded (max once), "I am excited to apply".
*Why:* The AI reviewer in TNW flagged "team player" and "results-driven". Buzzword lists come from the searches in [36] and the other sources in the searches list. Hiring-manager surveys flag generic AI language, and a Stanford study (via a secondary source) named "realm, intricate, showcasing, pivotal". The LLM-tell list is a heuristic, not an absolute ban on the words.
[4][36]

**N-7. Never leave spelling or grammar errors.**
Run a spell check pass and flag any non-dictionary token that isn't in the profile's skills or keywords lists.
*Why:* Spelling and grammar errors are Harvard's #1 mistake. A CareerBuilder figure (via secondary sources) says 59–77% of recruiters reject for typos.
[12][22][36]

**N-8. Never mix date formats, tenses, or person** (A-7, R-14). [20][36]

**N-9. Never include personal data or references** (R-22). [12][15]

**N-10. Never copy the job description's sentences** into the résumé or letter. [2][30]

**N-11. Never put key information in headers, footers, tables, or text boxes** (A-1, A-2). [21][22]

**N-12. Never send the same generic cover letter,** and never paste another company's name by mistake.
*Code check:* the letter contains the target company name and no other company name from the jobs database.
[28][33]

---

## Disagreements between sources and the position the generator should take

| Topic | Sources say | Generator position |
|---|---|---|
| **Length: 1 page vs 2** | MIT [13][15] and Tech Interview Handbook [24]: 1 page (MIT allows more for extensive experience or an advanced degree). ResumeGo's simulation [29] found recruiters prefer 2 pages (2.6× at mid level, 2.9× at managerial), but it is a simulation, not real outcomes. Wikipedia and SHRM: 1–2 pages [1][5]. | 1 page if under about 10 years of experience. Up to 2 pages if 10+ years or an advanced degree. Never 3. Fill page 2 only with relevant material. |
| **.docx vs PDF** | Indeed [2]: .docx may be best for ATS. Jobscan [19]: .docx or text-based PDF. SCU/Jobscan [22]: PDF preferred. Tech Interview Handbook [24]: submit PDF. Greenhouse [17] accepts both. | Generate .docx (required by the app), built to parse cleanly. Offer "Export PDF" from the same file. Tell the user to follow the posting's instructions. |
| **Do ATSs auto-reject?** | Jobscan [19]: 99.7% of recruiters use keyword filters. Enhancv [16]: 92% of 25 interviewed recruiters say the ATS does not auto-reject on content; only knockout questions filter. The "75% rejected by bots" claim traces to an unsourced 2012 sales pitch [16]. | Optimize for a human skimmer first and for keyword search second. Never trade readability for keyword density. Don't show users "75% get rejected"-style claims. |
| **The 6/7.4-second scan** | Ladders [10]: 7.4 s average initial scan. Spectacle Talent Partners [11] criticizes the method and its lack of context. ResumeGo's simulation [29] measured minutes of review time. | Don't cite a number to users. Do follow the practical implications: headline, titles, and dates must be scannable at a glance. |
| **"References available on request"** | Tailero (UK) [7] says the line is sufficient. Harvard [12]: "Don't list references." MIT [15]: employers assume it. | Omit it (US default). |
| **Year-only dates** | Jobscan's date article [20] and formatting article [21] say avoid year-only. Some secondary testers say it parses. | Use month and year. Year-only is allowed only for education older than about 10 years if the user opts in. |
| **Photo / personal data** | US, UK, and Harvard: never [6][12]. Parts of the EU, Middle East, and Asia: common [1][6]. | Never add these automatically. Flag non-US postings to the user (R-24). |
| **Summary vs no summary** | Tech Interview Handbook [24], SHRM [5], and Indeed [2] favor a headline and short summary. Harvard's undergraduate template has none (student context) [12]. | Include a headline plus a summary of 50 words or fewer for experienced applicants. For students with little experience, the summary is optional (headline only). |
| **Match-score targets** | Jobscan [19]: aim for 75%+. Enhancv [16]: most recruiters distrust fit scores. | Show coverage as information only. Never inflate it with unsupported terms (T-10). |
| **Acronyms: spell out everything?** | Harvard [12]: "Don't abbreviate." Tech Interview Handbook [24]: use "Amazon Web Services", not "AWS". Indeed and SCU [2][22]: use both forms. | Use both forms on first use for key terms (R-18), and short forms after that. Universally known terms (SQL, API, HTML) stay short. |
| **Quantify everything vs never fabricate** | Every source pushes metrics [4][5][14][19]. The app forbids invention. | Use profile numbers only (R-13). If a bullet has no number, describe scope in words. Prompt the user in the UI to add metrics to the profile; never make them up. |

---

## Sources

### URLs read (fully or in substantial part)

1. https://en.wikipedia.org/wiki/R%C3%A9sum%C3%A9 — Formats (chronological, functional, hybrid), 1–2 page norm, action verbs, spelling out acronyms, ATS font list via Indeed, country differences on personal data.
2. https://www.indeed.com/career-advice/resumes-cover-letters/ats-resume-template — Standard fonts at 10–12 pt, .docx may be best, standard headings, avoid headers/tables/graphics/text boxes, both acronym forms, no keyword stuffing.
3. https://www.linkedin.com/pulse/death-functional-resume-why-longer-works-faraz-riaz/ — Why functional résumés fail parsers and recruiters. Recommends a combination/hybrid format.
4. https://thenextweb.com/news/ai-robots-reviewed-my-resume-unimpressed-work — AI reviewer flagged missing metrics, "team player"/"results-driven" buzzwords, and excess length.
5. https://www.shrm.org/resourcesandtools/hr-topics/organizational-and-employee-development/pages/how-to-create-an-hr-resume.aspx — 1–2 pages, branded headline, accomplishments over duties, keywords throughout, spell out credential acronyms.
6. https://www.linkedin.com/pulse/customizing-resumes-different-countries-cultures-fbpsc/ — Country norms: the US omits photo, age, and marital status; the EU and Asia often include them.
7. https://www.tailero.com/en/guides/applications/how-to-write-cv — UK CV: reverse-chronological, 1–2 pages, 3–5 bullets per role, Month YYYY dates, omit DOB/photo/salary, file naming.
8. https://hbr.org/2023/06/when-blind-hiring-advances-dei-and-when-it-doesnt — Only partly accessible. Blind hiring background and adoption figures.
9. https://www.shrm.org/topics-tools/news/hr-magazine/can-blind-hiring-improve-workplace-diversity — Name, address, graduation dates, college, and hobbies as bias triggers.
10. https://www.hrdive.com/news/eye-tracking-study-shows-recruiters-look-at-resumes-for-7-seconds/541582/ — Ladders 2018 eye-tracking: 7.4 s scan. Simple layouts, clear headers, and bold titles win; columns, clutter, and long sentences lose.
11. https://spectacletalentpartners.com/is-the-6-second-resume-scan-a-myth/ — Methodological critique of the 6/7.4-second claim.
12. https://careerservices.fas.harvard.edu/resources/create-a-strong-resume/ — Reverse-chronological, action verbs, no pronouns, no photo/age/gender/references, don't start lines with dates, top-five mistakes, cover letter at most one page.
13. https://capd.mit.edu/resources/resume-checklist/ — Margins, 10–12 pt fonts, one page unless 10+ years, name on page 2, dates consistent, tense rules, quantify, no pronouns, avoid underlining.
14. https://capd.mit.edu/resources/resumes-writing-about-your-skills/ — PAR bullet formula, metric types, 1–2 line bullets, no pronouns.
15. https://capd.mit.edu/resources/resumes/ — Tailor each résumé; exclude personal info, photos, salary, and references.
16. https://enhancv.com/blog/does-ats-reject-resumes/ — 25 recruiter interviews (Sep–Oct 2025). 92% say the ATS doesn't auto-reject on content, 100% use knockouts, volume is the real filter, and what recruiters prioritize.
17. https://support.greenhouse.io/hc/en-us/articles/360052218132-Supported-formats-for-resumes-cover-letters-and-other-candidate-uploads — Greenhouse (ATS vendor) accepted file types and the 100 MB limit; externally linked images aren't loaded.
18. https://www.textkernel.com/learn-support/blog/improving-extraction-from-column-resumes/ — Textkernel (parser vendor): at least 15% of CVs use columns, and column layouts mix sections during extraction.
19. https://www.jobscan.co/blog/ats-resume/ — .docx or text PDF, single column, standard headings, job-title match (10.6×, vendor data), 99.7% of recruiters use keyword filters, 75% match target, no stuffing.
20. https://www.jobscan.co/blog/resume-dates/ — Month YYYY or MM/YYYY consistently, "Present", avoid year-only and DMY, old graduation dates may be omitted.
21. https://www.jobscan.co/blog/ats-formatting-mistakes/ — Apostrophe dates, icons/emoji, headers/footers/text boxes, creative headings, columns and skill bars, safe font list.
22. https://www.scu.edu/careercenter/toolkit/job-scan-common-ats-resume-formatting-mistakes/ — Santa Clara University career center summary: dates, both acronym forms, no tables/graphics, no headers/footers, margins, fonts, standard headings.
23. https://www.jobscan.co/blog/writing-a-resume-pay-attention-to-the-file-name/ — File name `FirstName-LastName-JobTitle`, no version numbers or special characters, keep it short.
24. https://www.techinterviewhandbook.org/resume/ — Software-engineer specifics: 1 page, section order, summary under 50 words, skills-line format, MM/YYYY, no header/footer, 0.5" margins, GPA > 3.5 only, keyword approach.
25. https://blog.pragmaticengineer.com/the-pragmatic-engineers-resume-template/ — Content matters more than the template. People forgot to replace placeholder contact info.
26. https://thetechresume.com/ — Gergely Orosz: real people read résumés, show impact and context, tailor, prioritize relevant experience.
27. https://blog.pragmaticengineer.com/cv-reviews/ — Limited content. Confirms that most engineer CVs Orosz reviewed were "dull" and points to the book.
28. https://www.resumego.net/research/cover-letters/ — Field experiment with 7,287 applications and a survey of 236 hiring professionals. Tailored letters gave a 53% higher callback rate than no letter; generic letters hurt.
29. https://www.resumego.net/research/one-or-two-page-resumes/ — Simulation with 482 recruiters and 7,712 résumés: 2-page preference by career level, with the authors' caveat that it is only a simulation.
30. https://careerservices.upenn.edu/cover-letter-writing-guide/ — Three-part letter, one page, specific and credible, use the employer's keywords, no stock letters, don't repeat the résumé.
31. https://careers.usc.edu/resources/cover-letter-overview/ — One page, 3–4 paragraphs, avoid "To Whom It May Concern", highlight impact rather than restating the résumé.
32. https://career.ucsb.edu/get-hired/cover-letters/build-paragraphs — Thesis-statement intro, one story per body paragraph tied back to the role, closing with thanks and a request for an interview.
33. https://careercenter.georgetown.edu/major-career-guides/resumes-cover-letters/cover-letters/ — Four-paragraph structure, "Dear Hiring Manager:", templates are easy to spot, focus on contribution, left-aligned with no indents.
34. https://www.indeed.com/career-advice/resumes-cover-letters/whats-the-ideal-cover-letter-length — 250–400 words, 3–4 paragraphs, 12 pt, 1" margins, a topic sentence per paragraph.
35. https://insight.ieeeusa.org/?p=5955 — IEEE-USA: tech hiring managers accept AI-assisted writing if it is accurate. About 5–10% of offers are rescinded for misrepresentation.
36. https://topresume.com/career-advice/avoid-these-resume-mistakes-part-one — Lack of focus, repetitive language, information overload, mixed person, tasks instead of achievements, positions older than 15 years, passive voice.
37. https://hbr.org/2014/02/how-to-write-a-cover-letter — Only the page shell and summary were accessible: send a letter, research first, strong opening, mention connections early, show you can solve the company's problems (partly via search summary).

Also consulted: https://en.wikipedia.org/wiki/Laszlo_Bock, only to confirm Bock's Google role. It does not mention the XYZ formula.

### Information taken only from search-result summaries (primary page not read; treat as unverified)

- The XYZ formula's attribution to Laszlo Bock. Tealhq and Inc.com both returned 403; the primary source is a 2014 LinkedIn post by Bock that was not accessed.
- CareerBuilder typo and quantification statistics, and TopResume's "47% generic objective" figure. Reported via secondary statistics roundups; careerbuilder.com returned 403.
- Resume Now AI-applicant survey (62% reject non-personalized AI résumés). The fetch timed out.
- The "Stanford research" list of AI-tell words, and TopResume's "20 seconds to detect AI" claim. Secondary summaries only.
- Greenhouse's "won't parse over 2.5 MB". A secondary claim that is not on Greenhouse's own page.
- Jobscan's "37% of rejections from date misreads". Seen in a search summary only. Not used; it looks implausible and unsourced.

### References from the Wikipedia article that could not be accessed or were skipped

| Reference | Status |
|---|---|
| https://www.forbes.com/sites/dianehamilton/2024/12/01/your-rsum-passed-ai-screening-how-to-also-stand-out-to-recruiters/ | 403 Forbidden |
| https://www.forbes.com/sites/dailymuse/2012/05/18/recruiters-tell-us-do-resume-fads-really-work/ | 403 Forbidden |
| http://www.bbc.com/capital/story/20140620-to-print-or-not-to-print | Blocked for the fetch tool |
| https://www.nytimes.com/2016/02/28/magazine/is-blind-hiring-the-best-hiring.html | Blocked (paywall/fetch restriction) |
| http://www.businessinsider.com/how-resumes-have-evolved-since-their-first-creation-in-1482-2011-2 | Blocked for the fetch tool |
| http://edition.cnn.com/2008/LIVING/worklife/05/07/cb.e.resume/index.html | HTTP 451 |
| http://www.dummies.com/how-to/content/reverse-chronological-resume-format-focusing-on-w0.html | 404 dead link |
| https://www.theladders.com/career-advice/you-only-get-6-seconds-of-fame-make-it-count, the Ladders eye-tracking PDF | 403. HR Dive's coverage [10] was used instead |
| https://finance.yahoo.com/news/11-resumes-that-got-worldwide-attention-194410148.html | Skipped (novelty résumés, not guidance) |
| https://www.linkedin.com/pulse/20141015111116-2417166-3-lessons-every-job-seeker-can-learn-from-the-world-s-oldest-cv | Skipped (history) |
| https://news.lettersofnote.com/p/i-will-make-an-infinite-number-of | Skipped (history) |
| https://www.davron.net/history-of-the-resume/ | Skipped (history) |
| https://thepracticalartworld.com/2011/02/12/how-to-write-an-artists-cv-in-10-steps/ | Skipped (artist CVs, out of scope) |
| https://usm.maine.edu/career-employment-hub/ | Skipped (landing page) |
| Merriam-Webster and Dictionary.com entries | Skipped (definitions only) |
| Sehgal, *Business Communication* (2008) | Book, not accessible |
| Chen (2023), AI and employment (NCBI) | Not accessed |
| Rojas-Galeano et al., *Scientific World Journal* | Not accessed; academic matching research, not practitioner guidance |
| Other items: Google Careers "how we hire" (navigation only, no advice text); Harvard College Resumes & Cover Letter Guide index page (directory only); careerbuilder.com buzzword article (403); tealhq.com XYZ article (403); inc.com Google recruiter tips (403) | Not usable |
