namespace Obj2Tiles.Library;

/// <summary>
/// Reads the statements of OBJ and MTL files: a keyword, then its argument.
/// </summary>
public static class ObjStatement
{
    private static readonly char[] Separators = [' ', '\t'];

    /// <summary>
    /// Splits <paramref name="line"/> into its keyword and its argument, which is the rest of the line without the
    /// whitespace around it. The names given to <c>usemtl</c>, <c>newmtl</c>, <c>mtllib</c>, <c>g</c> and
    /// <c>o</c> run to the end of the line, so a name with spaces ("Paint 1 Carmine", as Blender and Assimp write
    /// them) is one name, and every reader of a name must take it from here to find the same one.
    /// </summary>
    public static (string Keyword, string Argument) Split(string line)
    {
        var statement = line.AsSpan().Trim();
        var end = statement.IndexOfAny(Separators);

        return end < 0
            ? (statement.ToString(), string.Empty)
            : (statement[..end].ToString(), statement[(end + 1)..].Trim().ToString());
    }

    /// <summary>
    /// The words of the statement <paramref name="line"/>: OBJ and MTL separate them by spaces or tabs, and
    /// exporters (ODM among them) indent statements with a tab, so a keyword is found only when both separate
    /// words. A blank line has none.
    /// </summary>
    public static string[] Tokens(string line) =>
        line.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The argument of the statement <paramref name="line"/>: see <see cref="Split"/>.
    /// </summary>
    public static string Argument(string line) => Split(line).Argument;
}
