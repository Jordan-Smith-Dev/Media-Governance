using Umbraco.Cms.Core.Services;

namespace Umbraco.Community.MediaGovernance.Tests;

internal class FakeKeyValues : IKeyValueService
{
	public Dictionary<string, string> Values { get; } = new();
	public string? GetValue(string key) => Values.GetValueOrDefault(key);
	public IReadOnlyDictionary<string, string?>? FindByKeyPrefix(string keyPrefix) => throw new NotImplementedException();
	public void SetValue(string key, string value) => Values[key] = value;
	public void SetValue(string key, string originValue, string newValue) => Values[key] = newValue;
	public bool TrySetValue(string key, string originValue, string newValue) { Values[key] = newValue; return true; }
}
