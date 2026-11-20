namespace Umbraco.Community.MediaGovernance.Services.Settings;

internal static class AdminSettingsValidator
{
	public const int MaxPermittedUses = 12;
	public const int MaxUseLength = 30;

	/// <summary>Trims the permitted uses, drops blanks and removes duplicates (ignoring case), keeping the first spelling.</summary>
	public static AdminSettingsUpdate Normalize(AdminSettingsUpdate update) => update with { PermittedUses = CleanUses(update.PermittedUses ?? []) };

	public static List<string> CleanUses(IEnumerable<string> uses) =>
		uses.Select(u => u?.Trim() ?? string.Empty)
			.Where(u => u.Length > 0)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

	/// <summary>Throws <see cref="ArgumentException"/> when the settings cannot be saved. Call after <see cref="Normalize"/>.</summary>
	public static void Validate(AdminSettingsUpdate update)
	{
		if (update.PermittedUses.Count > MaxPermittedUses)
			throw new ArgumentException($"Use at most {MaxPermittedUses} permitted uses.", nameof(update));
		foreach (var use in update.PermittedUses)
		{
			if (use.Length > MaxUseLength)
				throw new ArgumentException($"'{use}' is too long: permitted uses can be at most {MaxUseLength} characters.", nameof(update));
			if (use.IndexOfAny([',', '\r', '\n']) >= 0)
				throw new ArgumentException($"'{use}' cannot contain a comma or a line break.", nameof(update));
		}
	}
}
