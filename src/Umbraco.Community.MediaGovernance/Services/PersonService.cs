using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services;

public class PersonService(
    IPersonRepository people,
    IAuditService audit)
{
    public Task<IReadOnlyList<PersonRow>> ListAsync(string? query) => people.ListAsync(query);

    public Task<PersonRow?> GetAsync(Guid id) => people.GetAsync(id);

    public async Task<PersonRow> CreateAsync(PersonInput input, string actor)
    {
        var row = new PersonRow { Id = Guid.NewGuid(), CreatedUtc = DateTime.UtcNow };
        Apply(row, input);
        await people.SaveAsync(row);
        await audit.WriteAsync(actor, "person.created", personId: row.Id);
        return row;
    }

    public async Task<PersonRow> UpdateAsync(Guid id, PersonInput input, string actor)
    {
        var row = await people.GetAsync(id) ?? throw new KeyNotFoundException($"Person {id} not found.");
        Apply(row, input);
        await people.SaveAsync(row);
        await audit.WriteAsync(actor, "person.updated", personId: id);
        return row;
    }

    /// <summary>Records whether the person has given consent to appear in images. Every change is audited with who made it.</summary>
    public async Task<PersonRow> SetConsentAsync(Guid id, bool given, string actor)
    {
        var row = await people.GetAsync(id) ?? throw new KeyNotFoundException($"Person {id} not found.");
        if (row.ConsentGiven == given) return row;

        row.ConsentGiven = given;
        row.ConsentUpdatedUtc = DateTime.UtcNow;
        await people.SaveAsync(row);
        await audit.WriteAsync(actor, given ? "consent.given" : "consent.withdrawn", personId: id);
        return row;
    }

    /// <summary>Permanently removes the person and their image tags. The audit entry records only that an erasure happened.</summary>
    public async Task EraseAsync(Guid id, string actor)
    {
        if (await people.GetAsync(id) is null)
            throw new KeyNotFoundException($"Person {id} not found.");

        await people.EraseAsync(id);
        await audit.WriteAsync(actor, "person.erased", personId: id, detail: "Person and their image tags erased on request.");
    }

    private static void Apply(PersonRow row, PersonInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("A person needs a name.", nameof(input));

        row.Name = input.Name.Trim();
        row.Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();
        row.Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim();
        row.JobTitle = string.IsNullOrWhiteSpace(input.JobTitle) ? null : input.JobTitle.Trim();
        row.LinkedMemberKey = input.LinkedMemberKey;
        row.LinkedUserKey = input.LinkedUserKey;
    }
}
