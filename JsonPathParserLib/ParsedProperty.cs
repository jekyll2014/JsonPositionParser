namespace JsonPathParserLib
{
    public class ParsedProperty
    {
        public int StartPosition = -1;
        public int EndPosition = -1;
        public string Name = string.Empty;
        public string Value = string.Empty;
        public JsonPropertyType JsonPropertyType = JsonPropertyType.Unknown;
        public JsonValueType ValueType;
        public char PathDivider { get; private set; }

        public string Path
        {
            get => _path;
            set
            {
                _parentPath = null;
                _path = value;
            }
        }

        private string _path = string.Empty;

        public string ParentPath => _parentPath ?? (_parentPath = TrimPathEnd(Path, 1, PathDivider));
        private string _parentPath = null;

        public int RawLength
        {
            get
            {
                if (StartPosition == -1 || EndPosition == -1)
                    return -1;

                return EndPosition - StartPosition + 1;
            }
        }

        public int Depth
        {
            get
            {
                if (string.IsNullOrEmpty(_path))
                    return 0;

                return _path.Split(PathDivider).Length;
            }
        }

        public ParsedProperty(char pathDivider = '.')
        {
            PathDivider = pathDivider;
        }

        private static string TrimPathEnd(string originalPath, int levels, char pathDivider)
        {
            for (; levels > 0; levels--)
            {
                var pos = originalPath.LastIndexOf(pathDivider);
                if (pos > 0)
                    originalPath = originalPath.Substring(0, pos);
                else
                    return "";
            }

            return originalPath;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
