namespace JsonPathParserLib
{
    public class ParsedProperty
    {
        public int StartPosition = -1;
        public int EndPosition = -1;
        public string Path = "";
        public string Name = "";
        public string Value = "";
        public JsonPropertyTypes JsonPropertyType = JsonPropertyTypes.Unknown;
        public JsonValueTypes ValueType;

        public int RawLength
        {
            get
            {
                if (StartPosition == -1 || EndPosition == -1)
                    return 0;

                return EndPosition - StartPosition + 1;
            }
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
