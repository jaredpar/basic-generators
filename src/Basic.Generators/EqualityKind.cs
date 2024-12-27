namespace Basic.Generators;

/// <summary>
/// The kind of equality that should be used for a member
/// </summary>
/// <remarks>
/// This must be kept in sync with the <see cref="AutoEqualityKind"/> enum in the generator.
/// </remarks>
internal enum EqualityKind
{
    /// <summary>
    /// This member should have no equality checks.
    /// </summary>
    None = 0,

    /// <summary>
    /// Use simple null checks and .Equals calls
    /// </summary>
    Default,

    /// <summary>
    /// Use the == / != operators
    /// </summary>
    Operator,

    /// <summary>
    /// Use Enumerable.SequenceEqual for the comparison
    /// </summary>
    SequenceEqual,

    /// <summary>
    /// Compare strings with case sensitive equality
    /// </summary>
    Ordinal,

    /// <summary>
    /// Compare the strings with case insensitive equality
    /// </summary>
    OrdinalIgnoreCase,

    /// <summary>
    /// Compare using the current culture
    /// </summary>
    CurrentCulture, 

    /// <summary>
    /// Compare using the current culture with case insensitive equality
    /// </summary>
    CurrentCultureIgnoreCase,

    /// <summary>
    /// Compare using the invariant culture
    /// </summary>
    InvariantCulture,

    /// <summary>
    /// Compare using the invariant culture with case insensitive equality
    /// </summary>
    InvariantCultureIgnoreCase,
}