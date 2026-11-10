using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Repositories;
using Umbraco.Community.MediaGovernance.Persistence.Schema;
using Umbraco.Community.MediaGovernance.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakePersonRepository : IPersonRepository
{
    public Dictionary<Guid, PersonRow> People { get; } = new();
    public List<string> EvidenceToReturnOnErase { get; } = new();
    public List<Guid> Erased { get; } = new();

    public Task<PersonRow?> GetAsync(Guid id) => Task.FromResult(People.GetValueOrDefault(id));
    public Task<IReadOnlyList<PersonRow>> ListAsync(string? query) =>
        Task.FromResult<IReadOnlyList<PersonRow>>(People.Values.ToList());
    public Task SaveAsync(PersonRow person) { People[person.Id] = person; return Task.CompletedTask; }
    public Task<IReadOnlyList<string>> EraseAsync(Guid personId)
    {
        Erased.Add(personId);
        People.Remove(personId);
        return Task.FromResult<IReadOnlyList<string>>(EvidenceToReturnOnErase.ToList());
    }
}

internal class FakeImageRepository : IImageRepository
{
    public Dictionary<Guid, ImageRow> Images { get; } = new();
    public Dictionary<Guid, List<Guid>> Tags { get; } = new();

    public Task<ImageRow?> GetAsync(Guid mediaKey) => Task.FromResult(Images.GetValueOrDefault(mediaKey));
    public Task<IReadOnlyList<Guid>> GetTaggedPersonIdsAsync(Guid mediaKey) =>
        Task.FromResult<IReadOnlyList<Guid>>(Tags.GetValueOrDefault(mediaKey) ?? new List<Guid>());
    public Task SaveAsync(ImageRow image, IReadOnlyList<Guid> taggedPersonIds)
    {
        Images[image.MediaKey] = image;
        Tags[image.MediaKey] = taggedPersonIds.ToList();
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<string>> DeleteForMediaAsync(Guid mediaKey) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task<IReadOnlyDictionary<Guid, int>> CountTagsByPersonAsync() =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(Tags.Values.SelectMany(t => t).GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count()));

    public Task<IReadOnlyList<Guid>> ListGovernedKeysAsync() => Task.FromResult<IReadOnlyList<Guid>>(Images.Keys.ToList());

    public Dictionary<Guid, ImageGovernanceFacts> Facts { get; } = new();
    public Task<IReadOnlyList<ImageGovernanceFacts>> GetFactsAsync(IReadOnlyCollection<Guid> mediaKeys) =>
        Task.FromResult<IReadOnlyList<ImageGovernanceFacts>>(
            mediaKeys.Select(k => Facts.GetValueOrDefault(k) ?? new ImageGovernanceFacts(k, null, null, null, false, [], [])).ToList());
}

internal class FakeConsentRepository : IConsentRepository
{
    public Dictionary<Guid, ConsentRow> Consents { get; } = new();
    public Task<ConsentRow?> GetAsync(Guid id) => Task.FromResult(Consents.GetValueOrDefault(id));
    public Task SaveAsync(ConsentRow consent) { Consents[consent.Id] = consent; return Task.CompletedTask; }
    public Task<IReadOnlyList<ConsentRow>> ListForImageAsync(Guid mediaKey) =>
        Task.FromResult<IReadOnlyList<ConsentRow>>(Consents.Values.Where(c => c.MediaKey == mediaKey).ToList());
}

internal class FakeAuditRepository : IAuditRepository
{
    public List<AuditRow> Rows { get; } = new();
    public Task WriteAsync(AuditRow row) { Rows.Add(row); return Task.CompletedTask; }
    public Task<IReadOnlyList<AuditRow>> ListAsync(int take) => Task.FromResult<IReadOnlyList<AuditRow>>(Rows);
}

internal class FakeEvidenceStore : IEvidenceStore
{
    public List<string> Deleted { get; } = new();
    public List<Guid> PersonFoldersDeleted { get; } = new();
    public bool FailOnSave { get; set; }
    public bool FailOnDelete { get; set; }
    public string Extension { get; set; } = ".pdf";

    public Task<string> SaveAsync(Guid personId, Guid consentId, string fileName, Stream data) =>
        FailOnSave ? throw new ArgumentException("bad file") : Task.FromResult($"{personId}/{consentId}{Extension}");

    public Task DeleteAsync(IEnumerable<string> paths)
    {
        if (FailOnDelete) throw new IOException("locked");
        Deleted.AddRange(paths);
        return Task.CompletedTask;
    }

    public Task DeleteForPersonAsync(Guid personId)
    {
        if (FailOnDelete) throw new IOException("locked");
        PersonFoldersDeleted.Add(personId);
        return Task.CompletedTask;
    }
}
