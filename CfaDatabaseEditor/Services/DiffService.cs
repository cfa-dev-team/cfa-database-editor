namespace CfaDatabaseEditor.Services;

public enum DiffOpKind
{
    Equal,
    Removed,
    Added,
    Modified, // paired Removed+Added on same row
    Collapsed // visual placeholder for elided unchanged regions
}

/// <summary>
/// One row in the side-by-side diff. Either side may be null (empty).
/// LineNumber is 1-based and refers to the original file (left) or new file (right).
/// </summary>
public record DiffRow(
    DiffOpKind Kind,
    int? LeftLineNumber,
    string? LeftText,
    int? RightLineNumber,
    string? RightText,
    IReadOnlyList<CharSpan>? LeftSpans = null,
    IReadOnlyList<CharSpan>? RightSpans = null,
    int CollapsedCount = 0);

public record CharSpan(int Start, int Length, bool Changed);

public static class DiffService
{
    /// <summary>
    /// Builds a side-by-side diff between two text snapshots. Lines are paired
    /// row-by-row so deletions sit on the left only and additions on the right
    /// only. Long unchanged stretches collapse to a placeholder row, leaving up
    /// to <paramref name="contextLines"/> lines of headroom around each change.
    /// </summary>
    public static IReadOnlyList<DiffRow> ComputeSideBySide(
        string leftText,
        string rightText,
        int contextLines = 3,
        int maxRows = 5000)
    {
        var leftLines = SplitLines(leftText);
        var rightLines = SplitLines(rightText);
        var ops = LcsLineDiff(leftLines, rightLines);
        var paired = PairAdjacentRemovedAdded(ops);

        // Build rows with line numbers
        var rows = new List<DiffRow>(paired.Count);
        int li = 0, ri = 0;
        foreach (var op in paired)
        {
            switch (op.Kind)
            {
                case DiffOpKind.Equal:
                    rows.Add(new DiffRow(DiffOpKind.Equal, li + 1, leftLines[li], ri + 1, rightLines[ri]));
                    li++; ri++;
                    break;
                case DiffOpKind.Removed:
                    rows.Add(new DiffRow(DiffOpKind.Removed, li + 1, leftLines[li], null, null));
                    li++;
                    break;
                case DiffOpKind.Added:
                    rows.Add(new DiffRow(DiffOpKind.Added, null, null, ri + 1, rightLines[ri]));
                    ri++;
                    break;
                case DiffOpKind.Modified:
                    var (lspans, rspans) = CharDiff(leftLines[li], rightLines[ri]);
                    rows.Add(new DiffRow(DiffOpKind.Modified, li + 1, leftLines[li], ri + 1, rightLines[ri], lspans, rspans));
                    li++; ri++;
                    break;
            }
        }

        var collapsed = CollapseEqualRuns(rows, contextLines);

        // Cap to maxRows of *changed* content. Count non-Equal/Collapsed rows.
        int changedCount = collapsed.Count(r => r.Kind == DiffOpKind.Removed || r.Kind == DiffOpKind.Added || r.Kind == DiffOpKind.Modified);
        if (changedCount > maxRows)
            return Array.Empty<DiffRow>(); // signal "too large" via emptiness; caller checks

        return collapsed;
    }

    private static List<string> SplitLines(string text)
    {
        // Preserve all lines including a trailing empty one if the text ends with a newline.
        var list = new List<string>();
        if (text.Length == 0) return list;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                int end = i;
                if (end > start && text[end - 1] == '\r') end--;
                list.Add(text.Substring(start, end - start));
                start = i + 1;
            }
        }
        if (start < text.Length)
            list.Add(text.Substring(start));
        return list;
    }

    // ── LCS-based line diff (Hunt-McIlroy style; suitable for files up to a few thousand lines) ──

    private enum BasicOp { Equal, Removed, Added }
    private record LineOp(BasicOp Kind);

    private static List<LineOp> LcsLineDiff(List<string> a, List<string> b)
    {
        int n = a.Count, m = b.Count;
        // dp[i,j] = length of LCS of a[..i] and b[..j]
        var dp = new int[n + 1, m + 1];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                dp[i + 1, j + 1] = a[i] == b[j] ? dp[i, j] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var ops = new List<LineOp>();
        int x = n, y = m;
        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && a[x - 1] == b[y - 1])
            {
                ops.Add(new LineOp(BasicOp.Equal));
                x--; y--;
            }
            else if (y > 0 && (x == 0 || dp[x, y - 1] >= dp[x - 1, y]))
            {
                ops.Add(new LineOp(BasicOp.Added));
                y--;
            }
            else
            {
                ops.Add(new LineOp(BasicOp.Removed));
                x--;
            }
        }
        ops.Reverse();
        return ops;
    }

    private record PairedOp(DiffOpKind Kind);

    /// <summary>
    /// Pairs adjacent Removed+Added runs into Modified entries so they share a
    /// row. Extra unmatched lines stay on their own side.
    /// </summary>
    private static List<PairedOp> PairAdjacentRemovedAdded(List<LineOp> ops)
    {
        var paired = new List<PairedOp>();
        int i = 0;
        while (i < ops.Count)
        {
            if (ops[i].Kind == BasicOp.Equal)
            {
                paired.Add(new PairedOp(DiffOpKind.Equal));
                i++;
                continue;
            }

            // Collect the run of Removed and Added in any order.
            int removedCount = 0, addedCount = 0;
            int j = i;
            while (j < ops.Count && ops[j].Kind != BasicOp.Equal)
            {
                if (ops[j].Kind == BasicOp.Removed) removedCount++;
                else addedCount++;
                j++;
            }

            int pairs = Math.Min(removedCount, addedCount);
            for (int k = 0; k < pairs; k++) paired.Add(new PairedOp(DiffOpKind.Modified));
            for (int k = 0; k < removedCount - pairs; k++) paired.Add(new PairedOp(DiffOpKind.Removed));
            for (int k = 0; k < addedCount - pairs; k++) paired.Add(new PairedOp(DiffOpKind.Added));

            i = j;
        }
        return paired;
    }

    // ── Character-level diff for paired modified lines ──

    private static (IReadOnlyList<CharSpan> left, IReadOnlyList<CharSpan> right) CharDiff(string a, string b)
    {
        // Bound LCS work — char diff on huge lines is wasteful.
        if (a.Length > 2000 || b.Length > 2000)
        {
            return (
                new[] { new CharSpan(0, a.Length, true) },
                new[] { new CharSpan(0, b.Length, true) }
            );
        }

        int n = a.Length, m = b.Length;
        var dp = new int[n + 1, m + 1];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                dp[i + 1, j + 1] = a[i] == b[j] ? dp[i, j] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var leftSpans = new List<CharSpan>();
        var rightSpans = new List<CharSpan>();
        int x = 0, y = 0;
        while (x < n || y < m)
        {
            if (x < n && y < m && a[x] == b[y])
            {
                AppendSpan(leftSpans, x, 1, false);
                AppendSpan(rightSpans, y, 1, false);
                x++; y++;
            }
            else if (y < m && (x == n || dp[x, y + 1] >= dp[x + 1, y]))
            {
                AppendSpan(rightSpans, y, 1, true);
                y++;
            }
            else
            {
                AppendSpan(leftSpans, x, 1, true);
                x++;
            }
        }

        return (leftSpans, rightSpans);
    }

    private static void AppendSpan(List<CharSpan> list, int start, int length, bool changed)
    {
        if (list.Count > 0)
        {
            var last = list[^1];
            if (last.Changed == changed && last.Start + last.Length == start)
            {
                list[^1] = last with { Length = last.Length + length };
                return;
            }
        }
        list.Add(new CharSpan(start, length, changed));
    }

    // ── Collapse long stretches of equal lines, leaving headroom around changes ──

    private static List<DiffRow> CollapseEqualRuns(List<DiffRow> rows, int context)
    {
        if (context < 0) context = 0;

        // Mark which rows must be kept: any non-Equal row, plus `context` neighbors.
        var keep = new bool[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Kind == DiffOpKind.Equal) continue;
            int lo = Math.Max(0, i - context);
            int hi = Math.Min(rows.Count - 1, i + context);
            for (int k = lo; k <= hi; k++) keep[k] = true;
        }

        var result = new List<DiffRow>(rows.Count);
        int idx = 0;
        while (idx < rows.Count)
        {
            if (keep[idx])
            {
                result.Add(rows[idx]);
                idx++;
            }
            else
            {
                int start = idx;
                while (idx < rows.Count && !keep[idx]) idx++;
                int count = idx - start;
                if (count > 0)
                    result.Add(new DiffRow(DiffOpKind.Collapsed, null, null, null, null, CollapsedCount: count));
            }
        }
        return result;
    }
}
