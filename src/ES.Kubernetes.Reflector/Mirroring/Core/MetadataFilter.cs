using System.Text.RegularExpressions;

namespace ES.Kubernetes.Reflector.Mirroring.Core;

/// <summary>
///     Shared filtering logic for reflecting labels and annotations from source to target resources.
///     Both use the same full-match regex semantics and support comma-separated pattern lists.
/// </summary>
public static class MetadataFilter
{
    /// <summary>
    ///     Filters a dictionary of key-value pairs by a comma-separated list of regex patterns
    ///     applied to the keys. Each pattern uses full-match semantics (implicitly anchored).
    ///     Returns only entries whose key matches at least one pattern.
    /// </summary>
    /// <param name="source">The source key-value pairs (labels or annotations).</param>
    /// <param name="filterPattern">Comma-separated regex patterns to match against keys. Empty = no matches.</param>
    /// <param name="excludedPrefixes">Key prefixes to always exclude, even if the pattern matches.</param>
    public static Dictionary<string, string> Filter(
        IDictionary<string, string>? source,
        string filterPattern,
        string[]? excludedPrefixes = null)
    {
        var result = new Dictionary<string, string>();

        if (source is null || string.IsNullOrWhiteSpace(filterPattern))
            return result;

        var patterns = filterPattern
            .Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (patterns.Count == 0)
            return result;

        foreach (var kv in source)
        {
            if (excludedPrefixes is not null && IsExcludedByPrefix(kv.Key, excludedPrefixes))
                continue;

            if (MatchesAnyPattern(kv.Key, patterns))
                result[kv.Key] = kv.Value;
        }

        return result;
    }

    /// <summary>
    ///     Merges filtered source entries into an existing dictionary.
    ///     Source entries take precedence on key conflicts.
    /// </summary>
    public static Dictionary<string, string> MergeFiltered(
        IDictionary<string, string>? existing,
        IDictionary<string, string>? source,
        string filterPattern,
        string[]? excludedPrefixes = null)
    {
        var result = existing is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(existing);

        var filtered = Filter(source, filterPattern, excludedPrefixes);
        foreach (var kv in filtered)
            result[kv.Key] = kv.Value;

        return result;
    }

    private static bool MatchesAnyPattern(string key, List<string> patterns)
    {
        foreach (var pattern in patterns)
        {
            try
            {
                var match = Regex.Match(key, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
                if (match.Success && match.Value.Length == key.Length)
                    return true;
            }
            catch (RegexParseException)
            {
                // Invalid pattern — skip silently (fail closed)
            }
        }

        return false;
    }

    private static bool IsExcludedByPrefix(string key, string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
