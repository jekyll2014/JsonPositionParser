using System;
using System.Collections.Generic;

namespace JsonPathParserLib
{
    public class ParseException : Exception
    {
        public readonly int Position;
        public readonly List<ParsedProperty> PathList;

        public ParseException(string message, int position, List<ParsedProperty> pathList) : base(message)
        {
            PathList = pathList;
            Position = position;
        }
    }
}