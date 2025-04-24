using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JsonPathParserLib
{
    public class JsonPathParser
    {
        public string RootName { get; set; } = "root";
        public char JsonPathDivider { get; set; } = '.';
        // trim spaces and brackets out of the value content text
        public bool TrimComplexValues { get; set; } = false;
        // Keep complex object (object, array) content to the property
        public bool SaveComplexValues { get; set; } = false;
        public bool SearchStartOnly { get; set; } = false;
        // Keep comments as properties
        public bool KeepComments { get; set; } = false;

        private static readonly char[] _escapeChars = new char[] { '\"', '\\', '/', 'b', 'f', 'n', 'r', 't', 'u' };
        private static readonly char[] _endOfLineChars = new char[] { '\r', '\n' };
        private static readonly char[] _endingChars = new char[] { ' ', ',', '}', ']', '\t', '\r', '\n', '/' };
        private static readonly char[] _keywordOrNumberChars = "-+0123456789E.truefalsnl".ToCharArray();
        private static readonly string[] _keywords = { "true", "false", "null" };

        private string _jsonText;
        private List<ParsedProperty> _properties = new List<ParsedProperty>();

        private bool _searchMode = false;
        private string _searchPath = string.Empty;

        // convert text to the list of JSON properties
        public List<ParsedProperty> ParseJsonToPathList(string jsonText)
        {
            return ParseJsonToPathList(jsonText, out var _, out var _);
        }

        // convert text to the list of JSON properties
        public List<ParsedProperty> ParseJsonToPathList(string jsonText,
            out int endPosition,
            out string errorFound)
        {
            _searchMode = false;
            _searchPath = string.Empty;

            return StartParser(jsonText, out endPosition, out errorFound);
        }

        // look through the text to find certain path
        public ParsedProperty SearchJsonPath(string jsonText, string path)
        {
            _searchMode = true;
            _searchPath = path;
            var items = StartParser(jsonText, out var _, out var _).ToArray();

            if (items.Length == 0)
                return null;

            return items.FirstOrDefault(n => n.Path == path);
        }

        #region Utilities

        // Count the number of line endings betwee start and end positions in the text
        public static bool GetLinesNumber(string jsonText, int startPosition, int endPosition, out int startLine,
            out int endLine)
        {
            startLine = CountLinesFast(jsonText, 0, startPosition) + 1;
            endLine = startLine + CountLinesFast(jsonText, startPosition, endPosition);

            return true;
        }

        // Count the number of line endings (fool-proof)
        public static int CountLines(string jsonText, int startIndex, int endIndex)
        {
            if (startIndex >= jsonText.Length)
                return -1;

            if (startIndex > endIndex)
            {
                var n = startIndex;
                startIndex = endIndex;
                endIndex = n;
            }

            if (endIndex >= jsonText.Length)
                endIndex = jsonText.Length;

            var endOfLineChars = new List<char>();
            endOfLineChars.AddRange(_endOfLineChars);
            endOfLineChars.AddRange(Environment.NewLine.ToCharArray());
            var linesCount = 0;
            char currentChar;
            char nextChar;
            for (; startIndex < endIndex; startIndex++)
            {
                currentChar = jsonText[startIndex];
                if (!endOfLineChars.Contains(currentChar))
                    continue;

                nextChar = jsonText[startIndex + 1];
                linesCount++;
                if (startIndex < endIndex - 1
                    && currentChar != nextChar
                    && endOfLineChars.Contains(nextChar))
                    startIndex++;
            }

            return linesCount;
        }

        // Count the number of line endings (fast)
        public static int CountLinesFast(string jsonText, int startIndex, int endIndex)
        {
            var count = 0;
            while ((startIndex = jsonText.IndexOf('\n', startIndex)) != -1
                   && startIndex < endIndex)
            {
                count++;
                startIndex++;
            }

            return count;
        }

        // split all array paths (root\a[0]) to array body and array members (root\a\[0]) to simplify conversion to the tree structure
        public IEnumerable<ParsedProperty> ConvertForTreeProcessing(IEnumerable<ParsedProperty> schemaProperties)
        {
            var result = new List<ParsedProperty>();
            if (schemaProperties == null || !schemaProperties.Any())
                return result;

            foreach (var property in schemaProperties)
            {
                var tmpStr = new StringBuilder();
                tmpStr.Append(property.Path);
                var pos = tmpStr.ToString().IndexOf('[');
                while (pos >= 0)
                {
                    tmpStr.Insert(pos, JsonPathDivider);
                    pos = tmpStr.ToString().IndexOf('[', pos + 2);
                }

                var newProperty = new ParsedProperty(JsonPathDivider)
                {
                    Name = property.Name,
                    Path = tmpStr.ToString(),
                    JsonPropertyType = property.JsonPropertyType,
                    EndPosition = property.EndPosition,
                    StartPosition = property.StartPosition,
                    Value = property.Value,
                    ValueType = property.ValueType
                };

                result.Add(newProperty);
            }

            return result;
        }

        #endregion

        private List<ParsedProperty> StartParser(string jsonText, out int endPosition, out string errorFound)
        {
            _jsonText = jsonText;
            endPosition = 0;
            _properties = new List<ParsedProperty>();
            errorFound = string.Empty;
            if (string.IsNullOrEmpty(jsonText))
                return _properties;

            var currentPath = RootName;
            try
            {
                while (endPosition < _jsonText.Length)
                {
                    endPosition = FindStartOfNextToken(endPosition, out var foundObjectType);
                    if (endPosition >= _jsonText.Length)
                        break;

                    switch (foundObjectType)
                    {
                        case JsonPropertyType.Property:
                            endPosition = GetPropertyName(endPosition, currentPath);
                            break;
                        case JsonPropertyType.Comment:
                            endPosition = GetComment(endPosition, currentPath);
                            break;
                        case JsonPropertyType.Object:
                            endPosition = GetObject(endPosition, currentPath);
                            break;
                        case JsonPropertyType.EndOfObject:
                            break;
                        case JsonPropertyType.Array:
                            endPosition = GetArray(endPosition, currentPath);
                            break;
                        case JsonPropertyType.EndOfArray:
                            break;
                        default:
                            throw new Exception($"Invalid object found: {foundObjectType}.");
                    }

                    endPosition++;
                }
            }
            catch (Exception ex)
            {
                errorFound = ex.Message;
            }

            if (KeepComments)
                ReArrangeComments(_properties);

            return _properties;
        }

        private int FindStartOfNextToken(int pos, out JsonPropertyType foundObjectType)
        {
            foundObjectType = JsonPropertyType.Unknown;
            for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
            {
                var currentChar = _jsonText[pos];
                switch (currentChar)
                {
                    case '/':
                        foundObjectType = JsonPropertyType.Comment;

                        return pos;
                    case '\"':
                        foundObjectType = JsonPropertyType.Property;

                        return pos;
                    case '{':
                        foundObjectType = JsonPropertyType.Object;

                        return pos;
                    case '}':
                        foundObjectType = JsonPropertyType.EndOfObject;

                        return pos;
                    case '[':
                        foundObjectType = JsonPropertyType.Array;

                        return pos;
                    case ']':
                        foundObjectType = JsonPropertyType.EndOfArray;

                        return pos;
                    default:
                        if (_keywordOrNumberChars.Contains(currentChar)) // keyword or number found
                        {
                            foundObjectType = JsonPropertyType.KeywordOrNumberProperty;

                            return pos;
                        }

                        if (!char.IsWhiteSpace(currentChar) && currentChar != ',') // check for not allowed chars (not white-space or comma)
                        {
                            foundObjectType = JsonPropertyType.Error;
                            throw new Exception($"Incorrect character '{currentChar}' at position {pos}. Not a white-space or ','");
                        }

                        break;
                }
            }

            return pos;
        }

        private int GetComment(int pos, string currentPath)
        {
            if (_searchMode)
            {
                var lastItem = _properties?.LastOrDefault();
                if (lastItem?.Path == _searchPath)
                {
                    if (SearchStartOnly)
                        throw new Exception("Search finished");

                    if (lastItem?.JsonPropertyType != JsonPropertyType.Array
                        && lastItem?.JsonPropertyType != JsonPropertyType.Object)
                        throw new Exception($"Incorrect property type '{lastItem?.JsonPropertyType}' at position {pos}.");
                }
                else
                    _properties?.Remove(_properties.LastOrDefault());
            }

            var newElement = new ParsedProperty(JsonPathDivider)
            {
                JsonPropertyType = JsonPropertyType.Comment,
                StartPosition = pos,
                Path = currentPath,
                ValueType = JsonValueType.Unknown
            };

            if (KeepComments)
                _properties?.Add(newElement);

            pos = IncrementPosition(pos);
            switch (_jsonText[pos])
            {
                //single line comment
                case '/':
                    pos = IncrementPosition(pos);
                    for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
                    {
                        if (_endOfLineChars.Contains(_jsonText[pos])) //end of comment
                        {
                            pos--;
                            newElement.EndPosition = pos;
                            newElement.Value = _jsonText.Substring(newElement.StartPosition,
                                newElement.EndPosition - newElement.StartPosition + 1);

                            return pos;
                        }
                    }

                    pos--;
                    newElement.EndPosition = pos;
                    newElement.Value = _jsonText.Substring(newElement.StartPosition);

                    return pos;
                //multi line comment
                case '*':
                    pos = IncrementPosition(pos);
                    for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
                    {
                        if (_jsonText[pos] == '*') // possible end of comment
                        {
                            pos = IncrementPosition(pos);
                            if (_jsonText[pos] == '/')
                            {
                                newElement.EndPosition = pos;
                                newElement.Value = _jsonText.Substring(
                                    newElement.StartPosition,
                                    newElement.EndPosition - newElement.StartPosition + 1);

                                return pos;
                            }

                            pos--;
                        }
                    }

                    break;
            }

            throw new Exception($"Incorrect comment at position {pos}.");
        }

        private int GetPropertyName(int pos, string currentPath)
        {
            if (_searchMode)
            {
                var lastItem = _properties?.LastOrDefault();
                if (lastItem?.Path == _searchPath)
                {
                    if (SearchStartOnly)
                        throw new Exception($"Search finished");

                    if (lastItem?.JsonPropertyType != JsonPropertyType.Array
                        && lastItem?.JsonPropertyType != JsonPropertyType.Object)
                        throw new Exception("Search finished");
                }
                else
                    _properties?.Remove(_properties.LastOrDefault());
            }

            var newElement = new ParsedProperty(JsonPathDivider)
            {
                StartPosition = pos
            };

            _properties?.Add(newElement);
            pos = IncrementPosition(pos);
            for (; pos < _jsonText.Length; pos = IncrementPosition(pos)) // searching for property name end
            {
                var currentChar = _jsonText[pos];
                if (currentChar == '\\') //skip escape chars
                {
                    pos = IncrementPosition(pos);
                    if (_escapeChars.Contains(_jsonText[pos]))
                    {
                        if (_jsonText[pos] == 'u') // if \u0000
                            pos += 4;
                    }
                    else
                        break;
                }
                else if (currentChar == '\"') // end of property name found
                {
                    var newName = _jsonText.Substring(newElement.StartPosition, pos - newElement.StartPosition + 1);
                    pos = IncrementPosition(pos);
                    pos = GetPropertyDivider(pos, currentPath);
                    if (_jsonText[pos] == ',' || _jsonText[pos] == ']') // it's an array of values
                    {
                        pos--;
                        newElement.JsonPropertyType = JsonPropertyType.ArrayValue;
                        newElement.EndPosition = pos;
                        newElement.Path = currentPath;
                        newElement.ValueType = GetVariableType(newName);
                        newElement.Value = newElement.ValueType == JsonValueType.String ? newName.Trim('\"') : newName;

                        return pos;
                    }

                    newElement.Name = newName.Trim('\"');
                    pos = IncrementPosition(pos);
                    var valueStartPosition = pos;
                    pos = GetPropertyValue(pos, currentPath, ref valueStartPosition);
                    currentPath += JsonPathDivider + newElement.Name;
                    newElement.Path = currentPath;
                    switch (_jsonText[pos])
                    {
                        //it's an object
                        case '{':
                            newElement.JsonPropertyType = JsonPropertyType.Object;
                            newElement.EndPosition = pos = GetObject(pos, currentPath, false);
                            newElement.ValueType = JsonValueType.Object;
                            if (SaveComplexValues)
                            {
                                newElement.Value = _jsonText.Substring(newElement.StartPosition,
                                    newElement.EndPosition - newElement.StartPosition + 1);

                                if (TrimComplexValues)
                                    newElement.Value = TrimObjectValue(newElement.Value);
                            }

                            return pos;
                        //it's an array
                        case '[':
                            newElement.JsonPropertyType = JsonPropertyType.Array;
                            newElement.EndPosition = pos = GetArray(pos, currentPath);
                            newElement.ValueType = JsonValueType.Array;
                            if (SaveComplexValues)
                            {
                                newElement.Value = _jsonText.Substring(newElement.StartPosition,
                                    newElement.EndPosition - newElement.StartPosition + 1);

                                if (TrimComplexValues) newElement.Value = TrimArrayValue(newElement.Value);
                            }

                            return pos;
                        // it's a property
                        default:
                            newElement.JsonPropertyType = JsonPropertyType.Property;
                            newElement.EndPosition = pos;
                            var newValue = _jsonText
                                .Substring(valueStartPosition, pos - valueStartPosition + 1)
                                .Trim();

                            newElement.ValueType = GetVariableType(newValue);
                            newElement.Value = newElement.ValueType == JsonValueType.String
                                ? newValue.Trim('\"')
                                : newValue;

                            return pos;
                    }
                }
                else if (_endOfLineChars.Contains(currentChar)) // check restricted chars
                    break;
            }

            throw new Exception("Can't find end of property name till the end of the file.");
        }

        private int GetPropertyDivider(int pos, string currentPath)
        {
            for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
            {
                switch (_jsonText[pos])
                {
                    case ':':
                    case ']': // ????
                    case ',': // ????
                        return pos;
                    case '/':
                        pos = GetComment(pos, currentPath);
                        break;
                    default:
                        if (!char.IsWhiteSpace(_jsonText[pos]))
                            throw new Exception($"Incorrect character '{_jsonText[pos]}' at position {pos}. Not a white-space.");

                        break;
                }
            }

            throw new Exception("Can't find property divider till the end of the file.");
        }

        private int GetPropertyValue(int pos, string currentPath, ref int propertyStartPos)
        {
            for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
            {
                switch (_jsonText[pos])
                {
                    case '[': // it's a start of array                   
                    case '{': // or object
                        return pos;
                    case '/': //it's a comment                       
                        pos = GetComment(pos, currentPath);
                        propertyStartPos = pos + 1;
                        break;
                    case '\"': //it's a start of value string 
                        {
                            pos = IncrementPosition(pos);
                            for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
                            {
                                if (_jsonText[pos] == '\\') //skip escape chars
                                {
                                    pos = IncrementPosition(pos);
                                    if (_escapeChars.Contains(_jsonText[pos]))
                                    {
                                        if (_jsonText[pos] == 'u') // if \u0000
                                            pos += 4;

                                        continue;
                                    }
                                    else
                                        break;
                                }
                                else if (_jsonText[pos] == '\"')
                                    return pos;
                                else if (_endOfLineChars.Contains(_jsonText[pos])) // check restricted chars
                                    break;
                            }

                            throw new Exception("Can't find end of element till the end of the file.");
                        }
                    default:
                        if (!char.IsWhiteSpace(_jsonText[pos])) // it's a literal property value
                        {
                            for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
                            {
                                if (_endingChars.Contains(_jsonText[pos])) // value end found
                                {
                                    pos--;

                                    return pos;
                                }

                                if (!_keywordOrNumberChars.Contains(_jsonText[pos])) // check restricted chars
                                    throw new Exception($"Incorrect character '{_jsonText[pos]}' at position {pos}. Not a keyword or number.");
                            }
                        }

                        break;
                }
            }

            throw new Exception("Can't find end of property value till the end of the file.");
        }

        private int GetArray(int pos, string currentPath)
        {
            pos = IncrementPosition(pos);
            var arrayIndex = 0;
            for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
            {
                pos = FindStartOfNextToken(pos, out var foundObjectType);
                switch (foundObjectType)
                {
                    case JsonPropertyType.Comment:
                        pos = GetComment(pos, currentPath + "[" + arrayIndex + "]");
                        arrayIndex++;
                        break;
                    case JsonPropertyType.Property:
                        pos = GetPropertyName(pos, currentPath + "[" + arrayIndex + "]");
                        arrayIndex++;
                        break;
                    case JsonPropertyType.Object:
                        pos = GetObject(pos, currentPath + "[" + arrayIndex + "]");
                        arrayIndex++;
                        break;
                    case JsonPropertyType.KeywordOrNumberProperty:
                        pos = GetKeywordOrNumber(pos, currentPath + "[" + arrayIndex + "]", true);
                        arrayIndex++;
                        break;
                    case JsonPropertyType.Array:
                        pos = GetArray(pos, currentPath);
                        break;
                    case JsonPropertyType.EndOfArray:
                        if (_searchMode && currentPath == _searchPath)
                            throw new Exception("Search finished");

                        return pos;
                    default:
                        throw new Exception($"Incorrect element at position {pos}.");
                }
            }

            throw new Exception("Can't find end of array till the end of the file.");
        }

        private int GetObject(int pos, string currentPath, bool save = true)
        {
            if (_searchMode)
            {
                var lastItem = _properties?.LastOrDefault();
                if (lastItem?.Path == _searchPath)
                {
                    if (SearchStartOnly)
                        throw new Exception($"Search finished");

                    if (lastItem?.JsonPropertyType != JsonPropertyType.Array
                        && lastItem?.JsonPropertyType != JsonPropertyType.Object)
                        throw new Exception("Search finished");
                }
                else
                    _properties?.Remove(_properties.LastOrDefault());
            }

            var newElement = new ParsedProperty(JsonPathDivider);
            if (save)
            {
                newElement.StartPosition = pos;
                newElement.JsonPropertyType = JsonPropertyType.Object;
                newElement.Path = currentPath;
                newElement.ValueType = JsonValueType.Object;
                _properties?.Add(newElement);
            }

            pos = IncrementPosition(pos);
            for (; pos < _jsonText.Length; pos = IncrementPosition(pos))
            {
                pos = FindStartOfNextToken(pos, out var foundObjectType);
                switch (foundObjectType)
                {
                    case JsonPropertyType.Comment:
                        pos = GetComment(pos, currentPath);
                        break;
                    case JsonPropertyType.Property:
                        pos = GetPropertyName(pos, currentPath);
                        break;
                    case JsonPropertyType.Array:
                        pos = GetArray(pos, currentPath);
                        break;
                    case JsonPropertyType.Object:
                        pos = GetObject(pos, currentPath);
                        break;
                    case JsonPropertyType.EndOfObject:
                        if (save)
                        {
                            newElement.EndPosition = pos;
                            if (SaveComplexValues)
                            {
                                newElement.Value = _jsonText.Substring(newElement.StartPosition,
                                    newElement.EndPosition - newElement.StartPosition + 1);

                                if (TrimComplexValues)
                                    newElement.Value = TrimObjectValue(newElement.Value);
                            }

                            if (_searchMode && currentPath == _searchPath)
                                throw new Exception("Search finished");
                        }

                        return pos;
                    default:
                        throw new Exception($"Incorrect element at position {pos}.");
                }
            }

            throw new Exception("Can't find end of object till the end of the file.");

            return pos;
        }

        private int GetKeywordOrNumber(int pos, string currentPath, bool isArray)
        {
            if (_searchMode)
            {
                var lastItem = _properties?.LastOrDefault();
                if (lastItem?.Path == _searchPath)
                {
                    if (SearchStartOnly)
                        throw new Exception("Search finished");

                    if (lastItem?.JsonPropertyType != JsonPropertyType.Array
                        && lastItem?.JsonPropertyType != JsonPropertyType.Object)
                        throw new Exception("Search finished.");
                }
                else
                    _properties?.Remove(_properties.LastOrDefault());
            }

            var newElement = new ParsedProperty(JsonPathDivider)
            {
                StartPosition = pos
            };

            _properties?.Add(newElement);
            for (; pos < _jsonText.Length; pos = IncrementPosition(pos)) // searching for token end
            {
                var currentChar = _jsonText[pos];
                if (_endingChars.Contains(currentChar)) // end of token found
                {
                    pos--;
                    var newValue = _jsonText
                        .Substring(newElement.StartPosition, pos - newElement.StartPosition + 1)
                        .Trim();

                    if (!_keywords.Contains(newValue) && !IsNumeric(newValue))
                        throw new Exception($"Invalid value (not key word or number): \"{newValue}\".");

                    newElement.Value = newValue;
                    newElement.JsonPropertyType = isArray ? JsonPropertyType.ArrayValue : JsonPropertyType.Property;
                    newElement.EndPosition = pos;
                    newElement.Path = currentPath;
                    newElement.ValueType = GetVariableType(newValue);

                    return pos;
                }

                if (!_keywordOrNumberChars.Contains(currentChar)) // check restricted chars
                    throw new Exception($"Invalid character: '{currentChar}'.");
            }

            throw new Exception("Can't find end of element till the end of the file.");
        }

        private static JsonValueType GetVariableType(string str)
        {
            var type = JsonValueType.Unknown;
            if (string.IsNullOrEmpty(str))
            {
                type = JsonValueType.Unknown;
            }
            else if (str.Length > 1 && str[0] == '\"' && str[str.Length - 1] == '\"')
            {
                type = JsonValueType.String;
            }
            else if (str == "null")
            {
                type = JsonValueType.Null;
            }
            else if (str == "true" || str == "false")
            {
                type = JsonValueType.Boolean;
            }
            else if (IsNumeric(str))
            {
                type = str.Contains('.') ? JsonValueType.Number : JsonValueType.Integer;
            }

            return type;
        }

        // increment current position and check if the end-of-file reached
        private int IncrementPosition(int pos)
        {
            if (pos > _jsonText.Length)
                throw new Exception("End of file happened.");

            pos++;

            return pos;
        }

        private static bool IsNumeric(string str)
        {
            return double.TryParse(str, out var _);
        }

        private static string TrimObjectValue(string objectText)
        {
            return TrimBracketedValue(objectText, '{', '}');
        }

        private static string TrimArrayValue(string arrayText)
        {
            return TrimBracketedValue(arrayText, '[', ']');
        }

        private static string TrimBracketedValue(string text, char startChar, char endChar)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var startPosition = text.IndexOf(startChar);
            var endPosition = text.LastIndexOf(endChar);
            if (startPosition < 0 || endPosition <= 0 || endPosition <= startPosition)
                return text;

            if (endPosition - startPosition <= 1)
                return string.Empty;

            return text.Substring(startPosition + 1, endPosition - startPosition - 1).Trim();
        }

        // reorder comments to place them inside certain properties
        private static void ReArrangeComments(IEnumerable<ParsedProperty> schemaProperties)
        {
            var commentCount = 0;
            foreach (var comment in schemaProperties.Where(n => n.JsonPropertyType == JsonPropertyType.Comment))
            {
                var prop = schemaProperties
                    .Where(n => n.JsonPropertyType != JsonPropertyType.Comment
                    && n.StartPosition <= comment.StartPosition
                    && n.EndPosition >= comment.EndPosition)?
                    .LastOrDefault();

                comment.Path = prop?.Path + prop?.PathDivider + $"%Comment{commentCount}%";
                commentCount++;
            }
        }
    }
}
