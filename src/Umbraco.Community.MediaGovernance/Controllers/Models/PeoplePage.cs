namespace Umbraco.Community.MediaGovernance.Controllers.Models;

/// <summary>One page of people. <paramref name="Total"/> is how many match, so the screen can offer "load more".</summary>
internal record PeoplePage(IReadOnlyList<PersonDto> Items, int Total);
