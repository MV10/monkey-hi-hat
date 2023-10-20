
namespace mhh;

public static class Const
{
    /// <summary>
    /// Combines the TrimEntries and RemoveEmptyEntries flags
    /// </summary>
    public static readonly StringSplitOptions SplitOptions = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

    /// <summary>
    /// Shorter reference to StringComparison.InvariantCultureIgnoreCase
    /// </summary>
    public static readonly StringComparison StringComp = StringComparison.InvariantCultureIgnoreCase;
}
