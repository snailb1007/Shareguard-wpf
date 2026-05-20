using System;
using System.Collections.Generic;
using Flurl;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.Application.Services;

/// <summary>
/// Service implementation to clean URLs by stripping tracking and marketing query parameters.
/// </summary>
public sealed class UrlCleanerService : IUrlCleanerService
{
    private static readonly HashSet<string> TrackingParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source",
        "utm_medium",
        "utm_campaign",
        "utm_term",
        "utm_content",
        "fbclid",
        "gclid",
        "igshid",
        "_gl",
        "tt_medium",
        "twclid"
    };

    /// <summary>
    /// Strips matching tracking parameters from a URL using Flurl.
    /// </summary>
    /// <param name="dirtyUrl">The input URL.</param>
    /// <param name="cleanUrl">The cleaned URL if modified; otherwise, the original URL.</param>
    /// <param name="removedCount">The count of tracking parameters removed.</param>
    /// <returns>True if any tracking parameters were removed; otherwise, false.</returns>
    public bool CleanUrl(string dirtyUrl, out string cleanUrl, out int removedCount)
    {
        if (string.IsNullOrWhiteSpace(dirtyUrl))
        {
            cleanUrl = dirtyUrl;
            removedCount = 0;
            return false;
        }

        try
        {
            var url = new Url(dirtyUrl);

            // Validate scheme: only accept http/https
            if (string.IsNullOrEmpty(url.Scheme) ||
                (!url.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
                 !url.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
            {
                cleanUrl = dirtyUrl;
                removedCount = 0;
                return false;
            }

            // Find all parameters matching our tracking parameters list (case-insensitively)
            var matchingParams = new List<string>();
            foreach (var qp in url.QueryParams)
            {
                if (qp.Name != null && TrackingParams.Contains(qp.Name))
                {
                    matchingParams.Add(qp.Name);
                }
            }

            if (matchingParams.Count > 0)
            {
                // Remove matching parameters using Flurl's RemoveQueryParams
                url.RemoveQueryParams(matchingParams);

                removedCount = matchingParams.Count;
                cleanUrl = url.ToString();
                return true;
            }

            cleanUrl = dirtyUrl;
            removedCount = 0;
            return false;
        }
        catch (Exception)
        {
            cleanUrl = dirtyUrl;
            removedCount = 0;
            return false;
        }
    }
}
