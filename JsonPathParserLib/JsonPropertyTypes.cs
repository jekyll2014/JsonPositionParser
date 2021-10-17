namespace JsonPathParserLib
{
    public enum JsonPropertyTypes
    {
        Unknown,
        Comment,
        Property,
        KeywordOrNumberProperty,
        ArrayValue,
        Object,
        EndOfObject,
        Array,
        EndOfArray,
        Error
    }
}
