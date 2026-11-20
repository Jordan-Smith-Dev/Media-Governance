using System.Text;

namespace Umbraco.Community.MediaGovernance.Services.Overview;

internal static class ExportService
{
	public static string ToCsv(IEnumerable<OverviewItem> items)
	{
		var sb = new StringBuilder();
		sb.Append("Name,Folder,People,Date taken,Expiry date,Consent,Status\r\n");
		foreach (var i in items)
		{
			sb.Append(string.Join(",",
				Cell(i.Name), Cell(i.Folder), Cell(PeopleText(i)),
				i.DateTaken?.ToString("yyyy-MM-dd") ?? "", i.Expiry?.ToString("yyyy-MM-dd") ?? "",
				Cell(i.Consent), i.Status.ToString()));
			sb.Append("\r\n");
		}
		return sb.ToString();
	}

	private static string PeopleText(OverviewItem i) =>
		i.People.Count > 0 ? string.Join(", ", i.People) : i.NoPeople ? "No people" : "Not tagged yet";

	// Prefix formula triggers so spreadsheet apps do not execute exported text, then quote per RFC 4180.
	private static string Cell(string value)
	{
		if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
			value = "'" + value;
		return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
	}
}
