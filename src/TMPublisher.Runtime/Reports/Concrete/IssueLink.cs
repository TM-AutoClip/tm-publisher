using System.Text;

namespace TMPublisher.Runtime.Reports;

/// <summary>
/// The address of a new GitHub issue with the bug report form (<c>.github/ISSUE_TEMPLATE/bug_report.yml</c>) and some fields filled in
/// through their ids. Opening it sends nothing: the user reads the form, adds what happened and the logs zip, and submits it themselves.
/// Every value is escaped and cut to <see cref="MaxValue"/> characters, so the address stays far below what browsers and GitHub accept.
/// </summary>
public static class IssueLink
{
    /// <summary>Longest value of one field.</summary>
    public const int MaxValue = 300;

    /// <summary>Longest address made (GitHub and browsers accept about 8000 characters).</summary>
    public const int MaxLength = 4000;

    /// <summary>The address for <paramref name="repository"/> (<c>owner/name</c>).</summary>
    public static Uri For(string repository, string template, IEnumerable<KeyValuePair<string, string>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var address = new StringBuilder("https://github.com/").Append(repository).Append("/issues/new?template=").Append(Uri.EscapeDataString(template));
        foreach (var (id, value) in fields)
        {
            var part = "&" + Uri.EscapeDataString(id) + "=" + Uri.EscapeDataString(Cut(value));
            if (address.Length + part.Length > MaxLength)
            {
                break;
            }

            address.Append(part);
        }

        return new Uri(address.ToString());
    }

    private static string Cut(string value)
    {
        if (value.Length <= MaxValue)
        {
            return value;
        }

        // Never half of a surrogate pair (it could not be escaped).
        var length = char.IsHighSurrogate(value[MaxValue - 1]) ? MaxValue - 1 : MaxValue;
        return value[..length];
    }
}
