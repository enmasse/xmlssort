using System.Collections.Concurrent;
using System.Text.RegularExpressions;

internal static class XmlPathPatternMatcher
{
    public const string RecursiveWildcard = "**";
    private static readonly ConcurrentDictionary<string, SegmentMatcher> SegmentMatcherCache = new(StringComparer.Ordinal);

    public static bool IsPathMatch(IReadOnlyList<string> pathSegments, IReadOnlyList<string> patternSegments)
    {
        return CreatePathMatcher(patternSegments).IsMatch(pathSegments);
    }

    public static bool IsSegmentMatch(string elementName, string pattern)
    {
        return CreateSegmentMatcher(pattern).IsMatch(elementName);
    }

    public static SegmentMatcher CreateSegmentMatcher(string pattern)
    {
        if (pattern == RecursiveWildcard)
        {
            throw new ArgumentException("The recursive wildcard '**' cannot be used as a segment name pattern.");
        }

        return SegmentMatcherCache.GetOrAdd(pattern, static patternValue => new SegmentMatcher(patternValue));
    }

    public static PathMatcher CreatePathMatcher(IReadOnlyList<string> patternSegments)
    {
        var compiledSegments = new PathPatternSegment[patternSegments.Count];

        for (var index = 0; index < patternSegments.Count; index++)
        {
            compiledSegments[index] = patternSegments[index] == RecursiveWildcard
                ? PathPatternSegment.RecursiveWildcard
                : new PathPatternSegment(CreateSegmentMatcher(patternSegments[index]));
        }

        return new PathMatcher(compiledSegments);
    }

    public sealed class SegmentMatcher
    {
        private readonly string? exactPattern;
        private readonly Regex? wildcardPattern;

        internal SegmentMatcher(string pattern)
        {
            if (!pattern.Contains('*', StringComparison.Ordinal))
            {
                exactPattern = pattern;
                return;
            }

            wildcardPattern = new Regex(
                "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }

        public bool IsMatch(string elementName)
        {
            return exactPattern is not null
                ? string.Equals(elementName, exactPattern, StringComparison.Ordinal)
                : wildcardPattern!.IsMatch(elementName);
        }
    }

    public sealed class PathMatcher
    {
        private readonly PathPatternSegment[] patternSegments;

        internal PathMatcher(PathPatternSegment[] patternSegments)
        {
            this.patternSegments = patternSegments;
        }

        public bool IsMatch(IReadOnlyList<string> pathSegments)
        {
            var memo = new sbyte[(pathSegments.Count + 1) * (patternSegments.Length + 1)];
            return IsMatch(pathSegments, 0, 0, memo);
        }

        public bool CanMatchDescendant(IReadOnlyList<string> pathSegments)
        {
            var memo = new sbyte[(pathSegments.Count + 1) * (patternSegments.Length + 1)];
            return CanMatchDescendant(pathSegments, 0, 0, memo);
        }

        private bool IsMatch(IReadOnlyList<string> pathSegments, int pathIndex, int patternIndex, sbyte[] memo)
        {
            var stateIndex = (pathIndex * (patternSegments.Length + 1)) + patternIndex;

            if (memo[stateIndex] != 0)
            {
                return memo[stateIndex] > 0;
            }

            bool isMatch;

            if (patternIndex == patternSegments.Length)
            {
                isMatch = pathIndex == pathSegments.Count;
            }
            else if (patternSegments[patternIndex].IsRecursiveWildcard)
            {
                isMatch = IsMatch(pathSegments, pathIndex, patternIndex + 1, memo)
                    || (pathIndex < pathSegments.Count && IsMatch(pathSegments, pathIndex + 1, patternIndex, memo));
            }
            else
            {
                isMatch = pathIndex < pathSegments.Count
                    && patternSegments[patternIndex].Matcher!.IsMatch(pathSegments[pathIndex])
                    && IsMatch(pathSegments, pathIndex + 1, patternIndex + 1, memo);
            }

            memo[stateIndex] = isMatch ? (sbyte)1 : (sbyte)-1;
            return isMatch;
        }

        private bool CanMatchDescendant(IReadOnlyList<string> pathSegments, int pathIndex, int patternIndex, sbyte[] memo)
        {
            var stateIndex = (pathIndex * (patternSegments.Length + 1)) + patternIndex;

            if (memo[stateIndex] != 0)
            {
                return memo[stateIndex] > 0;
            }

            bool isMatch;

            if (pathIndex == pathSegments.Count)
            {
                isMatch = true;
            }
            else if (patternIndex == patternSegments.Length)
            {
                isMatch = false;
            }
            else if (patternSegments[patternIndex].IsRecursiveWildcard)
            {
                isMatch = CanMatchDescendant(pathSegments, pathIndex, patternIndex + 1, memo)
                    || CanMatchDescendant(pathSegments, pathIndex + 1, patternIndex, memo);
            }
            else
            {
                isMatch = patternSegments[patternIndex].Matcher!.IsMatch(pathSegments[pathIndex])
                    && CanMatchDescendant(pathSegments, pathIndex + 1, patternIndex + 1, memo);
            }

            memo[stateIndex] = isMatch ? (sbyte)1 : (sbyte)-1;
            return isMatch;
        }
    }

    internal readonly record struct PathPatternSegment(SegmentMatcher? Matcher, bool IsRecursiveWildcard = false)
    {
        public static PathPatternSegment RecursiveWildcard { get; } = new(null, true);
    }
}
