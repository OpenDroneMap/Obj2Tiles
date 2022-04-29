namespace Obj2Tiles.Library;

public static class Common
{
    public static double Epsilon = double.Epsilon;
}

public class FormattingStreamWriter : StreamWriter
{
    public FormattingStreamWriter(string path, IFormatProvider formatProvider)
        : base(path)
    {
        FormatProvider = formatProvider;
    }
    public override IFormatProvider FormatProvider { get; }
}