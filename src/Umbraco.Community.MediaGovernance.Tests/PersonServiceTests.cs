using Umbraco.Community.MediaGovernance.Services.Audit;
using Umbraco.Community.MediaGovernance.Services.Evidence;
using Umbraco.Community.MediaGovernance.Services.People;

namespace Umbraco.Community.MediaGovernance.Tests;

public class PersonServiceTests
{
	private readonly FakePersonRepository _people = new();
	private readonly FakeAuditRepository _auditRepo = new();
	private PersonService Sut() => new(_people, new AuditService(_auditRepo), new FakeSettings(), new EvidenceStore(new FakeEvidenceFiles()), new FixedTimeProvider(new DateOnly(2026, 11, 17)));

	[Fact]
	public async Task Create_requires_a_name() =>
		await Assert.ThrowsAsync<ArgumentException>(() =>
			Sut().CreateAsync(new PersonInput("  ", null, null, null, null), "actor"));

	[Fact]
	public async Task Create_and_update_keep_a_trimmed_job_title_and_treat_blank_as_none()
	{
		var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null, "  Lecturer "), "a");
		Assert.Equal("Lecturer", _people.People[p.Id].JobTitle);

		await Sut().UpdateAsync(p.Id, new PersonInput("Tom Reyes", null, null, null, null, "   "), "a");
		Assert.Null(_people.People[p.Id].JobTitle);
	}

	[Fact]
	public async Task Create_saves_person_and_audits()
	{
		var p = await Sut().CreateAsync(new PersonInput("Verna Burke", "verna@example.com", null, Guid.NewGuid(), null), "actor-1");
		Assert.Equal("Verna Burke", _people.People[p.Id].Name);
		Assert.Contains(_auditRepo.Rows, r => r.Action == "person.created" && r.ActorKey == "actor-1" && r.PersonId == p.Id);
	}

	[Fact]
	public async Task Erase_writes_anonymised_audit_entry()
	{
		var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", "tom@example.com", "0123", null, null), "a");
		await Sut().EraseAsync(p.Id, "a");
		var entry = Assert.Single(_auditRepo.Rows, r => r.Action == "person.erased");
		Assert.DoesNotContain("Tom", entry.Detail ?? "");
		Assert.DoesNotContain("tom@example.com", entry.Detail ?? "");
		// earlier audit rows about the person must not retain personal data either
		Assert.All(_auditRepo.Rows, r => Assert.DoesNotContain("tom@example.com", r.Detail ?? ""));
		Assert.All(_auditRepo.Rows, r => Assert.DoesNotContain("Tom Reyes", r.Detail ?? ""));
	}

	[Fact]
	public async Task Erase_unknown_person_throws() =>
		await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().EraseAsync(Guid.NewGuid(), "a"));

	[Fact]
	public async Task Erase_removes_the_person()
	{
		var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
		await Sut().EraseAsync(p.Id, "a");
		Assert.Contains(p.Id, _people.Erased);
	}

	[Fact]
	public async Task A_new_person_has_not_given_consent()
	{
		var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
		Assert.False(p.ConsentGiven);
		Assert.Null(p.ConsentUpdatedUtc);
	}

	[Fact]
	public async Task Giving_consent_is_recorded_and_audited_with_who_and_when()
	{
		var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
		var updated = await Sut().SetConsentAsync(p.Id, true, "editor-1");
		Assert.True(updated.ConsentGiven);
		Assert.NotNull(updated.ConsentUpdatedUtc);
		var entry = Assert.Single(_auditRepo.Rows, r => r.Action == "consent.given");
		Assert.Equal("editor-1", entry.ActorKey);
		Assert.Equal(p.Id, entry.PersonId);
		Assert.Null(entry.MediaKey);
	}

	[Fact]
	public async Task Withdrawing_consent_is_audited()
	{
		var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
		await Sut().SetConsentAsync(p.Id, true, "a");
		var updated = await Sut().SetConsentAsync(p.Id, false, "b");
		Assert.False(updated.ConsentGiven);
		Assert.Contains(_auditRepo.Rows, r => r.Action == "consent.withdrawn" && r.ActorKey == "b");
	}

	[Fact]
	public async Task Setting_consent_to_what_it_already_is_changes_and_audits_nothing()
	{
		var p = await Sut().CreateAsync(new PersonInput("Tom Reyes", null, null, null, null), "a");
		await Sut().SetConsentAsync(p.Id, false, "a");
		Assert.DoesNotContain(_auditRepo.Rows, r => r.Action.StartsWith("consent."));
	}

	[Fact]
	public async Task Consent_for_an_unknown_person_throws() =>
		await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().SetConsentAsync(Guid.NewGuid(), true, "a"));
}
