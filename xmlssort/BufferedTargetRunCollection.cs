using System.Xml;
using System.Xml.Linq;
internal sealed class BufferedTargetRunCollection
{
    private const int RunSize = 2048;

    private readonly IReadOnlyList<SortKey> keys;
    private readonly List<BufferedTargetFragment[]> completedRuns = [];
    private List<BufferedTargetFragment> currentRun = [];
    private int nextOrdinal;

    public BufferedTargetRunCollection(IReadOnlyList<SortKey> keys)
    {
        this.keys = keys;
    }

    public void Add(XNode node, XmlSorter.SortValue[] values)
    {
        currentRun.Add(new BufferedTargetFragment(node, values, nextOrdinal));
        nextOrdinal++;
        FlushCurrentRunIfNeeded();
    }

    public void AddRaw(string rawXml, XmlSorter.SortValue[] values)
    {
        currentRun.Add(new BufferedTargetFragment(rawXml, values, nextOrdinal));
        nextOrdinal++;
        FlushCurrentRunIfNeeded();
    }

    public IEnumerator<BufferedTargetFragment> GetMergedEnumerator()
    {
        FlushCurrentRun();
        return EnumerateMerged().GetEnumerator();
    }

    private IEnumerable<BufferedTargetFragment> EnumerateMerged()
    {
        if (completedRuns.Count == 0)
        {
            yield break;
        }

        if (completedRuns.Count == 1)
        {
            foreach (var target in completedRuns[0])
            {
                yield return target;
            }

            yield break;
        }

        var comparer = new BufferedTargetFragmentComparer(keys);
        var queue = new PriorityQueue<RunCursor, BufferedTargetFragment>(comparer);

        foreach (var run in completedRuns)
        {
            if (run.Length == 0)
            {
                continue;
            }

            var cursor = new RunCursor(run);
            queue.Enqueue(cursor, cursor.Current);
        }

        while (queue.TryDequeue(out var cursor, out var target))
        {
            yield return target;

            if (cursor.MoveNext())
            {
                queue.Enqueue(cursor, cursor.Current);
            }
        }
    }

    private void FlushCurrentRunIfNeeded()
    {
        if (currentRun.Count >= RunSize)
        {
            FlushCurrentRun();
        }
    }

    private void FlushCurrentRun()
    {
        if (currentRun.Count == 0)
        {
            return;
        }

        if (currentRun.Count > 1)
        {
            currentRun.Sort(new BufferedTargetFragmentComparer(keys));
        }

        completedRuns.Add([.. currentRun]);
        currentRun = [];
    }

    private sealed class RunCursor
    {
        private readonly BufferedTargetFragment[] run;
        private int index;

        public RunCursor(BufferedTargetFragment[] run)
        {
            this.run = run;
        }

        public BufferedTargetFragment Current => run[index];

        public bool MoveNext()
        {
            index++;
            return index < run.Length;
        }
    }
}

internal sealed class BufferedTargetFragment
{
    public BufferedTargetFragment(XNode node, XmlSorter.SortValue[] values, int ordinal)
    {
        Node = node;
        Values = values;
        Ordinal = ordinal;
    }

    public BufferedTargetFragment(string rawXml, XmlSorter.SortValue[] values, int ordinal)
    {
        RawXml = rawXml;
        Values = values;
        Ordinal = ordinal;
    }

    public XNode? Node { get; }

    public int Ordinal { get; }

    public string? RawXml { get; }

    public XmlSorter.SortValue[] Values { get; }

    public void WriteTo(XmlWriter writer)
    {
        if (RawXml is not null)
        {
            writer.WriteRaw(RawXml);
            return;
        }

        Node!.WriteTo(writer);
    }
}

internal sealed class BufferedTargetFragmentComparer(IReadOnlyList<SortKey> keys) : IComparer<BufferedTargetFragment>
{
    public int Compare(BufferedTargetFragment? left, BufferedTargetFragment? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var result = XmlSorter.CompareSortValues(left.Values, right.Values, keys);
        return result != 0 ? result : left.Ordinal.CompareTo(right.Ordinal);
    }
}
