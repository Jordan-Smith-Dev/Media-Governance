using Umbraco.Community.MediaGovernance.Core.Models;
using Umbraco.Community.MediaGovernance.Persistence.Schema;

namespace Umbraco.Community.MediaGovernance.Services;

/// <summary>
/// Turns a person's consent into what the status rules need. A plain yes is in force with no limits; the optional detail
/// (given-on date, expiry, permitted uses) only counts while its option is on. Detail saved while an option was on is kept
/// if the option is switched off, and ignored.
/// </summary>
public static class ConsentFactsFactory
{
    /// <summary>The consent facts for a person, or null when they have not given consent.</summary>
    public static ConsentFacts? Create(PersonRow person, bool detailsOn, bool expiryOn, bool usesOn)
    {
        if (!person.ConsentGiven) return null;

        var givenOn = detailsOn && person.ConsentGivenOn is { } given ? DateOnly.FromDateTime(given) : ConsentFacts.Always;
        DateOnly? expires = expiryOn && person.ConsentExpiresOn is { } end ? DateOnly.FromDateTime(end) : null;
        IReadOnlyList<string>? uses = usesOn ? Split(person.ConsentPermittedUses) : null;
        return new ConsentFacts(person.Id, givenOn, expires, null, uses);
    }

    public static ConsentFacts? Create(PersonRow person, GovernanceOptions options) =>
        Create(person, options.ConsentDetails, options.ConsentExpiry, options.UsesOn);

    public static IReadOnlyList<string> Split(string? uses) =>
        (uses ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>True when the person's consent is given and, if expiry is on, has not run out. Used to colour people and pills.</summary>
    public static bool InForce(PersonRow person, bool detailsOn, bool expiryOn, DateOnly today) =>
        Create(person, detailsOn, expiryOn, usesOn: false) is { } c
        && c.GivenOn <= today && (c.ExpiresOn is null || c.ExpiresOn > today);

    /// <summary>True when consent was given but its end date has passed (shown as "Consent expired").</summary>
    public static bool Expired(PersonRow person, bool expiryOn, DateOnly today) =>
        expiryOn && person.ConsentGiven && person.ConsentExpiresOn is { } end && DateOnly.FromDateTime(end) <= today;
}
