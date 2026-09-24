using JobHunt.Core.Documents;
using JobHunt.Core.Jobs;
using JobHunt.Core.Llm;
using JobHunt.Core.Profile;

namespace JobHunt.Tests;

[TestFixture]
public class DocumentNamingTests
{
    private static UserProfile Named(string first, string last) =>
        new() { Contact = new ContactInfo { FirstName = first, LastName = last } };

    [Test]
    public void Resume_UsesTheApplicantsName_AndTheAccentedE()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DocumentNaming.ResumeFileName(Named("Ryan", "DeBraal")), Is.EqualTo("Ryan-DeBraal-Résumé.docx"));
            Assert.That(DocumentNaming.CoverLetterFileName(Named("Ryan", "DeBraal")), Is.EqualTo("Ryan-DeBraal-Cover-Letter.docx"));
        });
    }

    [Test]
    public void Names_WithSpacesAccentsAndApostrophes_StayReadable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DocumentNaming.ResumeFileName(Named("Mary Ann", "O'Brien")), Is.EqualTo("Mary-Ann-O'Brien-Résumé.docx"));
            Assert.That(DocumentNaming.ResumeFileName(Named(" Zoë ", "Núñez")), Is.EqualTo("Zoë-Núñez-Résumé.docx"));
            Assert.That(DocumentNaming.ResumeFileName(Named("", "")), Is.EqualTo("Résumé.docx"));
        });
    }

    [Test]
    public void JobFolder_IsReadableUniqueAndWindowsSafe()
    {
        var job = new JobPosting { Company = "Foo: Bar/Baz", Title = "C# Dev?", BoardId = "linkedin", ExternalId = "4012345678" };
        var name = DocumentNaming.JobFolderName(job);
        Assert.That(name, Is.EqualTo("Foo_ Bar_Baz - C# Dev_ (linkedin-4012345678)"));
        Assert.That(name.IndexOfAny(Path.GetInvalidFileNameChars()), Is.EqualTo(-1));
    }

    [Test]
    public void FallbackChain_PutsTheSelectedProviderFirst()
    {
        Assert.That(LlmProviders.FallbackChain("gemini"), Is.EqualTo(new[] { "gemini", "claude", "openai", "kimi" }));
        Assert.That(LlmProviders.FallbackChain("unknown"), Is.EqualTo(new[] { "claude", "openai", "gemini", "kimi" }));
    }
}
