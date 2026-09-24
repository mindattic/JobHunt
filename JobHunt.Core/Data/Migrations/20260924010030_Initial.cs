using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobHunt.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HuntRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SearchProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    FinishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    JobsSeen = table.Column<int>(type: "INTEGER", nullable: false),
                    JobsNew = table.Column<int>(type: "INTEGER", nullable: false),
                    JobsRecommended = table.Column<int>(type: "INTEGER", nullable: false),
                    Error = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HuntRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Authorization_AuthorizedCountries = table.Column<string>(type: "TEXT", nullable: false),
                    Authorization_Citizenship = table.Column<string>(type: "TEXT", nullable: false),
                    Authorization_ClearanceActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_ConsentsToBackgroundCheck = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_ConsentsToDrugTest = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_HasDriversLicense = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_HasNonCompete = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_HasReliableTransportation = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_IsOver18 = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_NonCompeteDetails = table.Column<string>(type: "TEXT", nullable: false),
                    Authorization_PreviouslyEmployedAtTarget = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_RequiresSponsorship = table.Column<bool>(type: "INTEGER", nullable: false),
                    Authorization_SecurityClearance = table.Column<string>(type: "TEXT", nullable: false),
                    Authorization_VisaStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Authorization_WorkPermitExpires = table.Column<string>(type: "TEXT", nullable: false),
                    Compensation_Currency = table.Column<string>(type: "TEXT", nullable: false),
                    Compensation_CurrentAnnualSalary = table.Column<int>(type: "INTEGER", nullable: true),
                    Compensation_DesiredAnnualSalary = table.Column<int>(type: "INTEGER", nullable: true),
                    Compensation_DesiredHourlyRate = table.Column<string>(type: "TEXT", nullable: true),
                    Compensation_MinimumAnnualSalary = table.Column<int>(type: "INTEGER", nullable: true),
                    Compensation_MinimumHourlyRate = table.Column<string>(type: "TEXT", nullable: true),
                    Compensation_Notes = table.Column<string>(type: "TEXT", nullable: false),
                    Compensation_OpenToEquity = table.Column<bool>(type: "INTEGER", nullable: false),
                    Contact_Email = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_LastName = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_MiddleName = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Phone = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_PhoneCountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_PhoneType = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_PreferredName = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Pronouns = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_SecondaryEmail = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_SecondaryPhone = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Suffix = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Address_City = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Address_Country = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Address_County = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Address_PostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Address_State = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Address_Street1 = table.Column<string>(type: "TEXT", nullable: false),
                    Contact_Address_Street2 = table.Column<string>(type: "TEXT", nullable: false),
                    Disclosures_DisabilityStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Disclosures_Gender = table.Column<string>(type: "TEXT", nullable: false),
                    Disclosures_RaceEthnicity = table.Column<string>(type: "TEXT", nullable: false),
                    Disclosures_SexualOrientation = table.Column<string>(type: "TEXT", nullable: false),
                    Disclosures_VeteranStatus = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_AvoidIndustries = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_CanBeOnCall = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_CanWorkShifts = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_CanWorkWeekends = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_CompanyBlocklist = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_DesiredTitles = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_EarliestStartDate = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_MaxCommuteMiles = table.Column<int>(type: "INTEGER", nullable: false),
                    Preferences_MaxTravelPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    Preferences_MinimumContractMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    Preferences_NoticePeriodWeeks = table.Column<int>(type: "INTEGER", nullable: false),
                    Preferences_PreferredCompanySizes = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_PreferredIndustries = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_PreferredTimeZones = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_RelocationLocations = table.Column<string>(type: "TEXT", nullable: false),
                    Preferences_WantsContract = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_WantsContractToHire = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_WantsFullTime = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_WantsHybrid = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_WantsOnSite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_WantsPartTime = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_WantsRemote = table.Column<bool>(type: "INTEGER", nullable: false),
                    Preferences_WillingToRelocate = table.Column<bool>(type: "INTEGER", nullable: false),
                    Summary_Headline = table.Column<string>(type: "TEXT", nullable: false),
                    Summary_HighestEducation = table.Column<string>(type: "TEXT", nullable: false),
                    Summary_Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Summary_TotalYearsExperience = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ActiveProfileId = table.Column<int>(type: "INTEGER", nullable: true),
                    SelectedLlmProvider = table.Column<string>(type: "TEXT", nullable: false),
                    DryRun = table.Column<bool>(type: "INTEGER", nullable: false),
                    DailyApplyCap = table.Column<int>(type: "INTEGER", nullable: false),
                    PreflightQuestionScan = table.Column<bool>(type: "INTEGER", nullable: false),
                    MinActionDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxActionDelayMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Accomplishment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Organization = table.Column<string>(type: "TEXT", nullable: false),
                    Date = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accomplishment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Accomplishment_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BoardAccount",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BoardId = table.Column<string>(type: "TEXT", nullable: false),
                    SignInEmail = table.Column<string>(type: "TEXT", nullable: false),
                    AutoSignIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardAccount", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardAccount_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Certification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Issuer = table.Column<string>(type: "TEXT", nullable: false),
                    IssuedDate = table.Column<string>(type: "TEXT", nullable: false),
                    ExpiresDate = table.Column<string>(type: "TEXT", nullable: false),
                    CredentialId = table.Column<string>(type: "TEXT", nullable: false),
                    CredentialUrl = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certification_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomField",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomField", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomField_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Education",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    School = table.Column<string>(type: "TEXT", nullable: false),
                    Degree = table.Column<string>(type: "TEXT", nullable: false),
                    FieldOfStudy = table.Column<string>(type: "TEXT", nullable: false),
                    Minor = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<string>(type: "TEXT", nullable: false),
                    EndDate = table.Column<string>(type: "TEXT", nullable: false),
                    Graduated = table.Column<bool>(type: "INTEGER", nullable: false),
                    Gpa = table.Column<string>(type: "TEXT", nullable: false),
                    Honors = table.Column<string>(type: "TEXT", nullable: false),
                    Activities = table.Column<string>(type: "TEXT", nullable: false),
                    Coursework = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Education", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Education_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    BoardId = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalId = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Company = table.Column<string>(type: "TEXT", nullable: false),
                    CompanyUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    PostedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ApplicantCount = table.Column<int>(type: "INTEGER", nullable: true),
                    SupportsQuickApply = table.Column<bool>(type: "INTEGER", nullable: false),
                    BoardShowsApplied = table.Column<bool>(type: "INTEGER", nullable: false),
                    RawDescription = table.Column<string>(type: "TEXT", nullable: false),
                    DetailsReadAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Workplace = table.Column<string>(type: "TEXT", nullable: false),
                    EmploymentType = table.Column<string>(type: "TEXT", nullable: false),
                    ContractTerms = table.Column<string>(type: "TEXT", nullable: false),
                    ContractMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    ContractDurationText = table.Column<string>(type: "TEXT", nullable: false),
                    SalaryMin = table.Column<string>(type: "TEXT", nullable: true),
                    SalaryMax = table.Column<string>(type: "TEXT", nullable: true),
                    SalaryPeriod = table.Column<string>(type: "TEXT", nullable: true),
                    SalaryCurrency = table.Column<string>(type: "TEXT", nullable: false),
                    SalaryText = table.Column<string>(type: "TEXT", nullable: false),
                    SponsorshipAvailable = table.Column<bool>(type: "INTEGER", nullable: true),
                    ClearanceRequired = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsStaffingAgency = table.Column<bool>(type: "INTEGER", nullable: true),
                    RequiredYears = table.Column<int>(type: "INTEGER", nullable: true),
                    PlainDescription = table.Column<string>(type: "TEXT", nullable: false),
                    Highlights = table.Column<string>(type: "TEXT", nullable: false),
                    RedFlags = table.Column<string>(type: "TEXT", nullable: false),
                    TechStack = table.Column<string>(type: "TEXT", nullable: false),
                    Requirements = table.Column<string>(type: "TEXT", nullable: false),
                    Evidence = table.Column<string>(type: "TEXT", nullable: false),
                    UnderstoodAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Score = table.Column<int>(type: "INTEGER", nullable: true),
                    ScoreBreakdown = table.Column<string>(type: "TEXT", nullable: false),
                    FilterReasons = table.Column<string>(type: "TEXT", nullable: false),
                    ResumePath = table.Column<string>(type: "TEXT", nullable: false),
                    ResumeGeneratedHash = table.Column<string>(type: "TEXT", nullable: false),
                    CoverLetterPath = table.Column<string>(type: "TEXT", nullable: false),
                    CoverLetterGeneratedHash = table.Column<string>(type: "TEXT", nullable: false),
                    DocumentsGeneratedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedForApply = table.Column<bool>(type: "INTEGER", nullable: false),
                    FoundBy = table.Column<string>(type: "TEXT", nullable: false),
                    FirstSeenAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KeywordList",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordList", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KeywordList_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LanguageSkill",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Language = table.Column<string>(type: "TEXT", nullable: false),
                    Proficiency = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LanguageSkill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LanguageSkill_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileLink",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileLink_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Project",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    StartDate = table.Column<string>(type: "TEXT", nullable: false),
                    EndDate = table.Column<string>(type: "TEXT", nullable: false),
                    Technologies = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Project", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Project_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Reference",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Relationship = table.Column<string>(type: "TEXT", nullable: false),
                    Company = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", nullable: false),
                    YearsKnown = table.Column<int>(type: "INTEGER", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reference", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reference_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningAnswer",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Question = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedQuestion = table.Column<string>(type: "TEXT", nullable: false),
                    Answer = table.Column<string>(type: "TEXT", nullable: false),
                    Confirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningAnswer", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreeningAnswer_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Searches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BoardId = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SearchTerms = table.Column<string>(type: "TEXT", nullable: false),
                    Filters = table.Column<string>(type: "TEXT", nullable: false),
                    Weights = table.Column<string>(type: "TEXT", nullable: false),
                    ScoreThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxDocumentsPerHunt = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRunAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Searches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Searches_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skill",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", nullable: false),
                    Years = table.Column<double>(type: "REAL", nullable: true),
                    Level = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsed = table.Column<string>(type: "TEXT", nullable: false),
                    Aliases = table.Column<string>(type: "TEXT", nullable: false),
                    Featured = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skill_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TextBlock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TextBlock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TextBlock_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkExperience",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Employer = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", nullable: false),
                    WasRemote = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<string>(type: "TEXT", nullable: false),
                    EndDate = table.Column<string>(type: "TEXT", nullable: false),
                    EmploymentType = table.Column<string>(type: "TEXT", nullable: false),
                    Industry = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Technologies = table.Column<string>(type: "TEXT", nullable: false),
                    ReasonForLeaving = table.Column<string>(type: "TEXT", nullable: false),
                    FinalAnnualSalary = table.Column<int>(type: "INTEGER", nullable: true),
                    SupervisorName = table.Column<string>(type: "TEXT", nullable: false),
                    SupervisorTitle = table.Column<string>(type: "TEXT", nullable: false),
                    SupervisorPhone = table.Column<string>(type: "TEXT", nullable: false),
                    SupervisorEmail = table.Column<string>(type: "TEXT", nullable: false),
                    MayContact = table.Column<bool>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UserProfileId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkExperience", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkExperience_Profiles_UserProfileId",
                        column: x => x.UserProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobPostingId = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDryRun = table.Column<bool>(type: "INTEGER", nullable: false),
                    Method = table.Column<string>(type: "TEXT", nullable: false),
                    ResumePath = table.Column<string>(type: "TEXT", nullable: false),
                    ResumeHash = table.Column<string>(type: "TEXT", nullable: false),
                    CoverLetterPath = table.Column<string>(type: "TEXT", nullable: false),
                    CoverLetterHash = table.Column<string>(type: "TEXT", nullable: false),
                    Answers = table.Column<string>(type: "TEXT", nullable: false),
                    RunLog = table.Column<string>(type: "TEXT", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false),
                    OutcomeUpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Jobs_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bullet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    Keywords = table.Column<string>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkExperienceId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bullet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bullet_WorkExperience_WorkExperienceId",
                        column: x => x.WorkExperienceId,
                        principalTable: "WorkExperience",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobApplicationId = table.Column<int>(type: "INTEGER", nullable: false),
                    At = table.Column<long>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationEvents_Applications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accomplishment_UserProfileId",
                table: "Accomplishment",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationEvents_JobApplicationId",
                table: "ApplicationEvents",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_AppliedAt",
                table: "Applications",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_JobPostingId",
                table: "Applications",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_BoardAccount_UserProfileId_BoardId",
                table: "BoardAccount",
                columns: new[] { "UserProfileId", "BoardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bullet_WorkExperienceId",
                table: "Bullet",
                column: "WorkExperienceId");

            migrationBuilder.CreateIndex(
                name: "IX_Certification_UserProfileId",
                table: "Certification",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomField_UserProfileId",
                table: "CustomField",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Education_UserProfileId",
                table: "Education",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_HuntRuns_SearchProfileId",
                table: "HuntRuns",
                column: "SearchProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_UserProfileId_BoardId_ExternalId",
                table: "Jobs",
                columns: new[] { "UserProfileId", "BoardId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_UserProfileId_Status_Score",
                table: "Jobs",
                columns: new[] { "UserProfileId", "Status", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_KeywordList_UserProfileId",
                table: "KeywordList",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_LanguageSkill_UserProfileId",
                table: "LanguageSkill",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileLink_UserProfileId",
                table: "ProfileLink",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_DeletedAt",
                table: "Profiles",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Project_UserProfileId",
                table: "Project",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Reference_UserProfileId",
                table: "Reference",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningAnswer_NormalizedQuestion",
                table: "ScreeningAnswer",
                column: "NormalizedQuestion");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningAnswer_UserProfileId",
                table: "ScreeningAnswer",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Searches_UserProfileId",
                table: "Searches",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Skill_UserProfileId",
                table: "Skill",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TextBlock_UserProfileId",
                table: "TextBlock",
                column: "UserProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkExperience_UserProfileId",
                table: "WorkExperience",
                column: "UserProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Accomplishment");

            migrationBuilder.DropTable(
                name: "ApplicationEvents");

            migrationBuilder.DropTable(
                name: "BoardAccount");

            migrationBuilder.DropTable(
                name: "Bullet");

            migrationBuilder.DropTable(
                name: "Certification");

            migrationBuilder.DropTable(
                name: "CustomField");

            migrationBuilder.DropTable(
                name: "Education");

            migrationBuilder.DropTable(
                name: "HuntRuns");

            migrationBuilder.DropTable(
                name: "KeywordList");

            migrationBuilder.DropTable(
                name: "LanguageSkill");

            migrationBuilder.DropTable(
                name: "ProfileLink");

            migrationBuilder.DropTable(
                name: "Project");

            migrationBuilder.DropTable(
                name: "Reference");

            migrationBuilder.DropTable(
                name: "ScreeningAnswer");

            migrationBuilder.DropTable(
                name: "Searches");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "Skill");

            migrationBuilder.DropTable(
                name: "TextBlock");

            migrationBuilder.DropTable(
                name: "Applications");

            migrationBuilder.DropTable(
                name: "WorkExperience");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
