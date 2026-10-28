// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Preserves the current table filter and sort while generating deterministic numbered navigation.</summary>
internal sealed class TableNavigation(LabelTableQuery query, HttpRequest request)
{
    /// <summary>Gets the validated table query.</summary>
    internal LabelTableQuery Query { get; } = query;

    /// <summary>Gets or sets the provider's clamped current page number.</summary>
    internal long Page { get; set; } = 1;

    /// <summary>Gets or sets the total matching rows in the provider snapshot.</summary>
    internal long Total { get; set; }

    /// <summary>Gets the number of pages, treating an empty result as one empty page.</summary>
    internal long Pages => Math.Max(1, Total / LabelTableQuery.PageSize + (Total % LabelTableQuery.PageSize == 0 ? 0 : 1));

    /// <summary>Gets a validated page and sort from the request while keeping workflow filters typed.</summary>
    internal static TableNavigation? Read(HttpRequest request, LabelTableQuery defaults)
    {
        long page = 1;
        LabelTableSort sort = defaults.Sort;
        bool ascending = defaults.Ascending;
        IQueryCollection values = request.Query;
        if (values.ContainsKey("PageNumber") && !long.TryParse(values["PageNumber"], NumberStyles.None, CultureInfo.InvariantCulture, out page) ||
            values.ContainsKey("Sort") && !Enum.TryParse(values["Sort"], out sort) ||
            values.ContainsKey("Ascending") && !bool.TryParse(values["Ascending"], out ascending))
        {
            return null;
        }

        LabelTableQuery query = defaults with { Page = page, Sort = sort, Ascending = ascending };
        if (query.Kind == LabelTableKind.Audit)
        {
            string actor = values["Actor"].ToString().Trim();
            string outcome = values["Outcome"].ToString();
            string basis = values["TimeBasis"].ToString();
            TimeZoneInfo zone = TimeZoneInfo.Utc;
            if (basis is not ("" or "UTC" or "Local"))
            {
                return null;
            }

            if (basis == "Local")
            {
                if (values["TimeZone"].ToString() is not { Length: > 0 and <= 256 } zoneId ||
                    !TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out TimeZoneInfo? localZone))
                {
                    return null;
                }

                zone = localZone;
            }

            if (actor.Length > 2048 || outcome is not ("" or "Succeeded" or "Failed") ||
                !TryAuditTime(values["From"].ToString(), zone, out DateTimeOffset? from) ||
                !TryAuditTime(values["To"].ToString(), zone, out DateTimeOffset? to) ||
                from is not null && to is not null && from > to)
            {
                return null;
            }

            query = query with
            {
                AuditActor = actor.Length == 0 ? null : actor,
                AuditSucceeded = outcome switch { "Succeeded" => true, "Failed" => false, _ => null },
                AuditFrom = from,
                AuditTo = to
            };
        }

        if (query.Page < 1 || !query.Columns.Contains(query.Sort))
        {
            return null;
        }

        return new(query, request);
    }

    /// <summary>Interprets wall-clock bounds in the selected zone, rejecting daylight-saving gaps and ambiguous times.</summary>
    private static bool TryAuditTime(string value, TimeZoneInfo zone, out DateTimeOffset? timestamp)
    {
        timestamp = null;
        if (value.Length == 0)
        {
            return true;
        }

        if (!DateTime.TryParseExact(
            s: value,
            formats: ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"],
            provider: CultureInfo.InvariantCulture,
            style: DateTimeStyles.None,
            result: out DateTime parsed) ||
            zone.IsInvalidTime(parsed) || zone.IsAmbiguousTime(parsed))
        {
            return false;
        }

        timestamp = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(parsed, zone));
        return true;
    }

    /// <summary>Generates a link for a page without retaining obsolete forward-only cursors or handler names.</summary>
    internal string PageUrl(long page) => Url(page, Query.Sort, Query.Ascending);

    /// <summary>Generates a sort link that resets pagination and toggles only the active column.</summary>
    internal string SortUrl(LabelTableSort sort) => Url(
        page: 1,
        sort: sort,
        ascending: Query.Sort == sort ? !Query.Ascending : sort != LabelTableSort.Received);

    /// <summary>Gets the current accessible sorting state for a table header.</summary>
    internal string SortState(LabelTableSort sort) => (Query.Sort == sort, Query.Ascending) switch
    {
        (false, _) => "none",
        (true, true) => "ascending",
        _ => "descending"
    };

    /// <summary>Gets the directional indicator for the active sort column.</summary>
    internal string SortArrow(LabelTableSort sort) => (Query.Sort == sort, Query.Ascending) switch
    {
        (false, _) => string.Empty,
        (true, true) => "\u2191",
        _ => "\u2193"
    };

    /// <summary>Gets the next direction announced by an interactive column header.</summary>
    internal string NextDirection(LabelTableSort sort)
    {
        bool ascending = Query.Sort == sort ? !Query.Ascending : sort != LabelTableSort.Received;
        return ascending ? "ascending" : "descending";
    }

    /// <summary>Builds canonical, locally scoped URLs using only the table's known filter fields.</summary>
    private string Url(long page, LabelTableSort sort, bool ascending)
    {
        Dictionary<string, string?> values = new()
        {
            ["PageNumber"] = page.ToString(CultureInfo.InvariantCulture),
            ["Sort"] = sort.ToString(),
            ["Ascending"] = ascending.ToString(CultureInfo.InvariantCulture)
        };
        if (Query.Kind == LabelTableKind.Cases)
        {
            values["Queue"] = Query.Queue.ToString();
        }
        else if (Query.Kind == LabelTableKind.Reports)
        {
            values["Status"] = Query.ReportHandled switch { false => "Unresolved", true => "Handled", null => "All" };
        }
        else if (Query.Kind == LabelTableKind.Appeals)
        {
            values["Status"] = Query.AppealStatus?.ToString() ?? string.Empty;
        }
        else if (Query.Kind == LabelTableKind.Audit)
        {
            values["Actor"] = request.Query["Actor"].ToString();
            values["Outcome"] = Query.AuditSucceeded switch { true => "Succeeded", false => "Failed", null => string.Empty };
            values["From"] = request.Query["From"].ToString();
            values["To"] = request.Query["To"].ToString();
            values["TimeBasis"] = request.Query["TimeBasis"].ToString();
            values["TimeZone"] = request.Query["TimeZone"].ToString();
        }
        else if (Query.Kind == LabelTableKind.Team)
        {
            values["Search"] = request.Query["Search"].ToString();
        }

        return QueryHelpers.AddQueryString(request.PathBase + request.Path, values);
    }
}
