namespace TIndustry.Shared;

/// <summary>
/// How <c>ui {clic}</c> names a control, shared by the game's bridge (AgentBridge.FindControl) and the server's compact
/// <c>ui</c> answer, so a shortened name is always one the bridge resolves.
/// </summary>
public static class AgentUi
{
    /// <summary>
    /// <paramref name="query"/> names the control: its whole name, an end of it (whole path segments), its shown text
    /// or its tooltip, with or without the key hint (<c>Ricerca (T)</c> → <c>Ricerca</c>); texts ignore case and spaces.
    /// </summary>
    public static bool Matches(string name, string text, string tooltip, string query)
    {
        static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        static string WithoutHotkey(string tip) => tip.LastIndexOfAny(['[', '(']) is var cut and > 0 ? tip[..cut] : tip;
        return name == query || name.EndsWith("/" + query, StringComparison.Ordinal) || Same(text, query)
            || (tooltip.Length > 0 && (Same(tooltip, query) || Same(WithoutHotkey(tooltip), query)));
    }

    /// <summary>
    /// The shortest end of <paramref name="name"/> that <see cref="Matches"/> finds among <paramref name="all"/> for this
    /// control alone; the whole name otherwise (an exact name always wins in the bridge).
    /// </summary>
    public static string ShortName(string name, IReadOnlyList<(string Name, string Text, string Tooltip)> all)
    {
        var parts = name.Split('/');
        for (var take = 1; take < parts.Length; take++)
        {
            var end = string.Join('/', parts[^take..]);
            if (all.Count(c => Matches(c.Name, c.Text, c.Tooltip, end)) == 1)
            {
                return end;
            }
        }

        return name;
    }
}
