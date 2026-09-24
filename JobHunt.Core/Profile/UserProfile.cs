namespace JobHunt.Core.Profile;

/// <summary>
/// One JobHunt user (an applicant): everything a job application could ever ask for, entered once.
/// A database can hold several users — each has their own searches, jobs, application log, job-site
/// sign-ins and browser profile, and the app works as whichever one is active. This is the source
/// every search, score, tailored document and filled-in form for that user draws from.
/// <para>
/// Fixed fields cover what applications ask in common. Three open-ended parts cover everything
/// else: <see cref="KeywordLists"/> (any named list of keywords the user wants — industries,
/// methodologies, domains), <see cref="TextBlocks"/> (titled free text — an elevator pitch, a
/// leadership story, why you're looking), and <see cref="CustomFields"/> (any label/value pair a
/// form might ask that nothing else holds).
/// </para>
/// <para>
/// Bullets, skills and accomplishments are the "facts" a tailored resume may cite by
/// <c>FactRef</c> — what lets a validator reject any claim the profile does not back.
/// </para>
/// </summary>
public sealed class UserProfile
{
    public int Id { get; set; }

    public ContactInfo Contact { get; set; } = new();
    public WorkAuthorization Authorization { get; set; } = new();
    public Compensation Compensation { get; set; } = new();
    public WorkPreferences Preferences { get; set; } = new();
    public ProfessionalSummary Summary { get; set; } = new();
    public VoluntaryDisclosures Disclosures { get; set; } = new();

    /// <summary>Sign-in details for each job site (LinkedIn first). Only the email and preferences
    /// live here; the password is kept encrypted outside the database by
    /// <see cref="JobHunt.Core.Boards.BoardPasswords"/>.</summary>
    public List<BoardAccount> BoardAccounts { get; set; } = [];

    public List<ProfileLink> Links { get; set; } = [];
    public List<WorkExperience> Experience { get; set; } = [];
    public List<Education> Education { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
    public List<Certification> Certifications { get; set; } = [];
    public List<LanguageSkill> Languages { get; set; } = [];
    public List<Project> Projects { get; set; } = [];
    public List<Accomplishment> Accomplishments { get; set; } = [];
    public List<Reference> References { get; set; } = [];
    public List<KeywordList> KeywordLists { get; set; } = [];
    public List<TextBlock> TextBlocks { get; set; } = [];
    public List<CustomField> CustomFields { get; set; } = [];

    /// <summary>Answers to recurring screening questions, matched on
    /// <see cref="ScreeningAnswer.NormalizedQuestion"/>. Grows as the user answers questions an
    /// application could not answer on its own.</summary>
    public List<ScreeningAnswer> Answers { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>Set when the user is deleted. Deleting only hides (HOUSE-LAW-2): the profile, jobs and
    /// application history stay, and <see cref="ProfileStore.RestoreAsync"/> brings them back.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>What the user picker shows.</summary>
    public string DisplayName => FullName.Length > 0 ? FullName
        : Contact.Email.Length > 0 ? Contact.Email : "Unnamed user";

    public string FullName => string.Join(' ',
        new[] { Contact.FirstName, Contact.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>The confirmed answer bank entry for <paramref name="question"/>, if any.</summary>
    public ScreeningAnswer? FindAnswer(string question)
    {
        var key = ScreeningAnswer.Normalize(question);
        return Answers.FirstOrDefault(a => a.Confirmed && a.NormalizedQuestion == key);
    }
}

/// <summary>A row of a list the user orders by hand (work history, skills, …). The order is saved.</summary>
public interface IOrdered
{
    int SortOrder { get; set; }
}

public sealed class ContactInfo
{
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Suffix { get; set; } = "";
    public string PreferredName { get; set; } = "";
    public string Pronouns { get; set; } = "";
    public string Email { get; set; } = "";
    public string SecondaryEmail { get; set; } = "";
    public string PhoneCountryCode { get; set; } = "+1";
    public string Phone { get; set; } = "";
    /// <summary>Mobile / Home / Work — some forms ask which.</summary>
    public string PhoneType { get; set; } = "Mobile";
    public string SecondaryPhone { get; set; } = "";
    public Address Address { get; set; } = new();
}

public sealed class Address
{
    public string Street1 { get; set; } = "";
    public string Street2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string County { get; set; } = "";
    public string Country { get; set; } = "United States";
}

public sealed class WorkAuthorization
{
    public List<string> AuthorizedCountries { get; set; } = ["United States"];
    public bool RequiresSponsorship { get; set; }
    public string Citizenship { get; set; } = "";
    public string VisaStatus { get; set; } = "";
    public string WorkPermitExpires { get; set; } = "";
    public string SecurityClearance { get; set; } = "";
    public bool ClearanceActive { get; set; }
    public bool IsOver18 { get; set; } = true;
    public bool HasDriversLicense { get; set; } = true;
    public bool HasReliableTransportation { get; set; } = true;
    public bool ConsentsToBackgroundCheck { get; set; } = true;
    public bool ConsentsToDrugTest { get; set; } = true;
    public bool HasNonCompete { get; set; }
    public string NonCompeteDetails { get; set; } = "";
    public bool PreviouslyEmployedAtTarget { get; set; }
}

public sealed class Compensation
{
    public string Currency { get; set; } = "USD";
    public int? CurrentAnnualSalary { get; set; }
    public int? DesiredAnnualSalary { get; set; }
    public int? MinimumAnnualSalary { get; set; }
    public decimal? DesiredHourlyRate { get; set; }
    public decimal? MinimumHourlyRate { get; set; }
    public bool OpenToEquity { get; set; } = true;
    public string Notes { get; set; } = "";
}

/// <summary>What the user wants next — the defaults every new search starts from.</summary>
public sealed class WorkPreferences
{
    public List<string> DesiredTitles { get; set; } = [];
    public bool WantsRemote { get; set; } = true;
    public bool WantsHybrid { get; set; }
    public bool WantsOnSite { get; set; }
    public bool WillingToRelocate { get; set; }
    public List<string> RelocationLocations { get; set; } = [];
    public int MaxTravelPercent { get; set; }
    public int MaxCommuteMiles { get; set; }
    public List<string> PreferredTimeZones { get; set; } = [];
    public bool WantsFullTime { get; set; } = true;
    public bool WantsContract { get; set; } = true;
    public bool WantsContractToHire { get; set; } = true;
    public bool WantsPartTime { get; set; }
    public int? MinimumContractMonths { get; set; }
    public List<string> PreferredIndustries { get; set; } = [];
    public List<string> AvoidIndustries { get; set; } = [];
    public List<string> PreferredCompanySizes { get; set; } = [];
    public List<string> CompanyBlocklist { get; set; } = [];
    public int NoticePeriodWeeks { get; set; } = 2;
    public string EarliestStartDate { get; set; } = "";
    public bool CanWorkShifts { get; set; }
    public bool CanWorkWeekends { get; set; }
    public bool CanBeOnCall { get; set; }
}

public sealed class ProfessionalSummary
{
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public int? TotalYearsExperience { get; set; }
    public string HighestEducation { get; set; } = "";
}

/// <summary>US EEO self-identification. Every field defaults to declining — the user opts in.</summary>
public sealed class VoluntaryDisclosures
{
    public const string Decline = "I don't wish to answer";
    public string Gender { get; set; } = Decline;
    public string RaceEthnicity { get; set; } = Decline;
    public string VeteranStatus { get; set; } = Decline;
    public string DisabilityStatus { get; set; } = Decline;
    public string SexualOrientation { get; set; } = Decline;
}

/// <summary>
/// The applicant's account on one job site. The password is deliberately not a property: it never
/// enters the database, an export, or a log. See <see cref="JobHunt.Core.Boards.BoardPasswords"/>.
/// </summary>
public sealed class BoardAccount : IOrdered
{
    public int Id { get; set; }
    /// <summary>The <see cref="JobHunt.Core.Boards.IJobBoard.Id"/> this account is for ("linkedin").</summary>
    public string BoardId { get; set; } = "";
    /// <summary>The email or phone number the site signs in with.</summary>
    public string SignInEmail { get; set; } = "";
    /// <summary>Sign in automatically when the site shows its sign-in page. A verification code,
    /// CAPTCHA or security check always stops and hands over to the user.</summary>
    public bool AutoSignIn { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>LinkedIn, GitHub, portfolio, Stack Overflow, a personal site — any labeled URL.</summary>
public sealed class ProfileLink : IOrdered
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public string Url { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class WorkExperience : IOrdered
{
    public int Id { get; set; }
    public string Employer { get; set; } = "";
    public string Title { get; set; } = "";
    public string Location { get; set; } = "";
    public bool WasRemote { get; set; }
    /// <summary>"yyyy-MM".</summary>
    public string StartDate { get; set; } = "";
    /// <summary>"yyyy-MM", or blank for a current position.</summary>
    public string EndDate { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string Industry { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<Bullet> Bullets { get; set; } = [];
    public List<string> Technologies { get; set; } = [];
    public string ReasonForLeaving { get; set; } = "";
    public int? FinalAnnualSalary { get; set; }
    public string SupervisorName { get; set; } = "";
    public string SupervisorTitle { get; set; } = "";
    public string SupervisorPhone { get; set; } = "";
    public string SupervisorEmail { get; set; } = "";
    public bool MayContact { get; set; } = true;
    public int SortOrder { get; set; }

    public bool IsCurrent => string.IsNullOrWhiteSpace(EndDate);
}

/// <summary>One accomplishment line under a job — the unit a tailored resume selects and rewords.</summary>
public sealed class Bullet : IOrdered
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
    /// <summary>User keywords this bullet demonstrates ("migration", "mentoring"), used to match
    /// it to a posting's requirements.</summary>
    public List<string> Keywords { get; set; } = [];
    public int SortOrder { get; set; }

    public string FactRef => $"bullet:{Id}";
}

public sealed class Education : IOrdered
{
    public int Id { get; set; }
    public string School { get; set; } = "";
    public string Degree { get; set; } = "";
    public string FieldOfStudy { get; set; } = "";
    public string Minor { get; set; } = "";
    public string Location { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public bool Graduated { get; set; } = true;
    public string Gpa { get; set; } = "";
    public string Honors { get; set; } = "";
    public string Activities { get; set; } = "";
    public List<string> Coursework { get; set; } = [];
    public int SortOrder { get; set; }
}

public enum SkillKind
{
    /// <summary>Languages, frameworks, platforms — "C#", ".NET", "Azure".</summary>
    Hard,
    /// <summary>"Mentoring", "stakeholder communication".</summary>
    Soft,
    /// <summary>Products and tools — "Jira", "Visual Studio".</summary>
    Tool,
    /// <summary>Business domains — "healthcare billing", "fintech".</summary>
    Domain,
}

public sealed class Skill : IOrdered
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public SkillKind Kind { get; set; }
    public double? Years { get; set; }
    /// <summary>Beginner / Intermediate / Advanced / Expert.</summary>
    public string Level { get; set; } = "";
    public string LastUsed { get; set; } = "";
    /// <summary>Other names the same skill goes by in postings ("dotnet", "ASP.NET Core").</summary>
    public List<string> Aliases { get; set; } = [];
    /// <summary>Put near the top of every resume.</summary>
    public bool Featured { get; set; }

    public string FactRef => $"skill:{Id}";
    public int SortOrder { get; set; }
}

public sealed class Certification : IOrdered
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string IssuedDate { get; set; } = "";
    public string ExpiresDate { get; set; } = "";
    public string CredentialId { get; set; } = "";
    public string CredentialUrl { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class LanguageSkill : IOrdered
{
    public int Id { get; set; }
    public string Language { get; set; } = "";
    /// <summary>Elementary / Limited working / Professional working / Full professional / Native.</summary>
    public string Proficiency { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class Project : IOrdered
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";
    public string StartDate { get; set; } = "";
    public string EndDate { get; set; } = "";
    public List<string> Technologies { get; set; } = [];
    public int SortOrder { get; set; }

    public string FactRef => $"project:{Id}";
}

public enum AccomplishmentKind { Award, Publication, Patent, Volunteer, Speaking, Military, Other }

/// <summary>Awards, publications, patents, volunteer work, talks, military service.</summary>
public sealed class Accomplishment : IOrdered
{
    public int Id { get; set; }
    public AccomplishmentKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Organization { get; set; } = "";
    public string Date { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";

    public string FactRef => $"accomplishment:{Id}";
    public int SortOrder { get; set; }
}

public sealed class Reference : IOrdered
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Relationship { get; set; } = "";
    public string Company { get; set; } = "";
    public string Title { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public int? YearsKnown { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Any named list of keywords the user wants to keep — "Industries I know", "Methodologies",
/// "Keywords recruiters should see". Used for matching and available to resume tailoring.</summary>
public sealed class KeywordList : IOrdered
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<string> Keywords { get; set; } = [];
    public int SortOrder { get; set; }
}

/// <summary>A titled block of free text — an elevator pitch, a leadership story, "why I'm looking".
/// Cover letters and open-ended screening questions draw on these.</summary>
public sealed class TextBlock : IOrdered
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public int SortOrder { get; set; }
}

/// <summary>Anything a form might ask that no other field holds.</summary>
public sealed class CustomField : IOrdered
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class ScreeningAnswer : IOrdered
{
    public int Id { get; set; }
    public string Question { get; set; } = "";
    /// <summary>The key a question matches on across postings; kept in sync with
    /// <see cref="Question"/> by <see cref="SetQuestion"/>.</summary>
    public string NormalizedQuestion { get; set; } = "";
    public string Answer { get; set; } = "";
    /// <summary>False while the answer is only an LLM proposal; Apply never submits an
    /// unconfirmed answer without asking.</summary>
    public bool Confirmed { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void SetQuestion(string question)
    {
        Question = question;
        NormalizedQuestion = Normalize(question);
    }

    /// <summary>Lower-cased, whitespace-collapsed, trailing punctuation and required-field
    /// asterisks removed.</summary>
    public static string Normalize(string? question) =>
        string.Join(' ', (question ?? "").ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .TrimEnd('?', '.', ':', '*', ' ');
    public int SortOrder { get; set; }
}
