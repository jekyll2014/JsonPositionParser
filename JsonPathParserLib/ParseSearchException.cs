using System;

namespace JsonPathParserLib
{
    public class ParseSearchException : Exception
    {
        public readonly int Position;

        public ParseSearchException(string message, int position) : base(message)
        {
            Position = position;
        }
    }
}