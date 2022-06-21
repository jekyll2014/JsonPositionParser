import JsonParsedProperty from './JsonParsedProperty';
import { JsonPropertyType } from "./JsonPropertyType";
import { JsonValueType } from "./JsonValueTypes";

export default class JsonPathParser {
    private _jsonText: string;
    private _properties: JsonParsedProperty[];
    private _escapeChars: string[] = ['\"', '\\', '/', 'b', 'f', 'n', 'r', 't', 'u'];
    private _allowedSpacerChars: string[] = [' ', '\t', '\r', '\n'];
    private _endOfLineChars: string[] = ['\r', '\n'];
    private _keywordOrNumberChars: string[] = ["-", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "t", "r", "u", "e", "f", "a", "l", "s", "n", "l"];
    private _keywords: string[] = ["true", "false", "null"];

    private _errorFound: boolean;
    private _searchMode: boolean;
    private _searchPath: string;

    public TrimComplexValues: boolean;
    public SaveComplexValues: boolean;

    public JsonPathDivider: string = '.';
    public RootName: string = "root";
    public SearchStartOnly: boolean;

    public constructor(pathDivider: string) {
        this.JsonPathDivider = pathDivider;
    }

    public ParseJsonToPathList(jsonText: string): [JsonParsedProperty[], number, boolean] {
        this._searchMode = false;
        this._searchPath = "";
        let [result, endPosition, errorFound] = this.StartParser(jsonText);

        if (result.length <= 0) {
            return null;
        }

        return [result, endPosition, errorFound];
    }

    public SearchJsonPath(jsonText: string, path: string): JsonParsedProperty {
        this._searchMode = true;
        this._searchPath = path;
        let [result, endPosition, errorFound] = this.StartParser(jsonText);

        return result.find((n) => n.Path === path);
    }

    private StartParser(jsonText: string): [JsonParsedProperty[], number, boolean] {
        this._jsonText = jsonText;
        let endPosition = 0;
        this._errorFound = false;
        this._properties = [];

        if (jsonText === '') {
            this._errorFound = this._errorFound;
            return [this._properties, endPosition, this._errorFound];
        }

        let currentPath = this.RootName;
        while (!this._errorFound && endPosition < this._jsonText.length) {
            let foundObjectType: JsonPropertyType;
            [endPosition, foundObjectType] = this.FindStartOfNextToken(endPosition);

            if (this._errorFound || endPosition >= this._jsonText.length) {
                break;
            }

            switch (foundObjectType) {
                case JsonPropertyType.Property:
                    endPosition = this.GetPropertyName(endPosition, currentPath);
                    break;
                case JsonPropertyType.Comment:
                    endPosition = this.GetComment(endPosition, currentPath);
                    break;
                case JsonPropertyType.Object:
                    endPosition = this.GetObject(endPosition, currentPath);
                    break;
                case JsonPropertyType.EndOfObject:
                    break;
                case JsonPropertyType.Array:
                    endPosition = this.GetArray(endPosition, currentPath);
                    break;
                case JsonPropertyType.EndOfArray:
                    break;
                default:
                    this._errorFound = true;
                    break;
            }

            endPosition++;
        }

        return [this._properties, endPosition, this._errorFound];
    }

    private FindStartOfNextToken(pos: number): [number, JsonPropertyType] {
        let foundObjectType: JsonPropertyType;

        for (; pos < this._jsonText.length; pos++) {
            let currentChar = this._jsonText[pos];
            switch (currentChar) {
                case '/':
                    foundObjectType = JsonPropertyType.Comment;
                    return [pos, foundObjectType];
                case '\"':
                    foundObjectType = JsonPropertyType.Property;
                    return [pos, foundObjectType];
                case '{':
                    foundObjectType = JsonPropertyType.Object;
                    return [pos, foundObjectType];
                case '}':
                    foundObjectType = JsonPropertyType.EndOfObject;
                    return [pos, foundObjectType];
                case '[':
                    foundObjectType = JsonPropertyType.Array;
                    return [pos, foundObjectType];
                case ']':
                    foundObjectType = JsonPropertyType.EndOfArray;
                    return [pos, foundObjectType];
                default:
                    if (this._keywordOrNumberChars.includes(currentChar)) {
                        foundObjectType = JsonPropertyType.KeywordOrNumberProperty;
                        return [pos, foundObjectType];
                    }

                    const allowedChars = [' ', '\t', '\r', '\n', ','];
                    if (!allowedChars.includes(currentChar)) {
                        foundObjectType = JsonPropertyType.Error;
                        this._errorFound = true;
                        return [pos, foundObjectType];
                    }

                    break;
            }
        }

        return [pos, foundObjectType];
    }

    private GetComment(pos: number, currentPath: string): number {
        if (this._searchMode) {
            let lastItem = this._properties[this._properties.length - 1];
            if (lastItem.Path === this._searchPath) {
                if (this.SearchStartOnly
                    || (lastItem.JsonPropertyType !== JsonPropertyType.Array
                        && lastItem.JsonPropertyType !== JsonPropertyType.Object)) {
                    this._errorFound = true;
                    return pos;
                }
            }
            else {
                this._properties.splice(this._properties.length - 1, 1);
            }
        }

        let newElement = new JsonParsedProperty(this.JsonPathDivider);
        newElement.JsonPropertyType = JsonPropertyType.Comment;
        newElement.StartPosition = pos;
        newElement.Path = currentPath;
        newElement.ValueType = JsonValueType.Unknown;
        this._properties.push(newElement);

        pos++;
        if (pos >= this._jsonText.length) {
            this._errorFound = true;
            return pos;
        }

        switch (this._jsonText[pos]) {
            // single line comment
            case '/':
                pos++;
                if (pos >= this._jsonText.length) {
                    this._errorFound = true;
                    return pos;
                }

                for (; (pos < this._jsonText.length); pos++) {
                    if (this._endOfLineChars.includes(this._jsonText[pos])) { // end of comment
                        pos--;
                        newElement.EndPosition = pos;
                        newElement.Value = this._jsonText.substring(newElement.StartPosition,
                            newElement.EndPosition + 1);

                        return pos;
                    }
                }

                pos--;
                newElement.EndPosition = pos;
                newElement.Value = this._jsonText.substring(newElement.StartPosition);

                return pos;

            // multi line comment
            case '*':
                pos++;
                if (pos >= this._jsonText.length) {
                    this._errorFound = true;
                    return pos;
                }

                for (; pos < this._jsonText.length; pos++) {
                    if (this._jsonText[pos] === '*') { // possible end of comment
                        pos++;
                        if (pos >= this._jsonText.length) {
                            this._errorFound = true;
                            return pos;
                        }

                        if (this._jsonText[pos] === '/') {
                            newElement.EndPosition = pos;
                            newElement.Value = this._jsonText.substring(newElement.StartPosition,
                                newElement.EndPosition + 1);

                            return pos;
                        }

                        pos--;
                    }

                }

                break;
        }

        this._errorFound = true;
        return pos;
    }

    private GetPropertyName(pos: number, currentPath: string): number {
        if (this._searchMode) {
            let lastItem = this._properties[this._properties.length - 1];
            if (lastItem.Path === this._searchPath) {
                if (this.SearchStartOnly
                    || (lastItem.JsonPropertyType !== JsonPropertyType.Array
                        && lastItem.JsonPropertyType !== JsonPropertyType.Object)) {
                    this._errorFound = true;

                    return pos;
                }
            }
            else {
                this._properties.splice(this._properties.length - 1, 1);
            }
        }

        let newElement = new JsonParsedProperty(this.JsonPathDivider);
        newElement.StartPosition = pos;
        this._properties.push(newElement);
        pos++;
        for (; pos < this._jsonText.length; pos++) { // searching for property name end
            let currentChar = this._jsonText[pos];
            if (currentChar === '\\') { // skip escape chars
                pos++;
                if (pos >= this._jsonText.length) {
                    this._errorFound = true;

                    return pos;
                }

                if (this._escapeChars.includes(this._jsonText[pos])) {
                    if (this._jsonText[pos] === 'u') { // if \u0000
                        pos += 4;
                    }
                }
                else {
                    this._errorFound = true;

                    return pos;
                }
            }
            else if (currentChar === '\"') { // end of property name found
                let newName = this._jsonText.substring(newElement.StartPosition,
                    pos + 1);
                pos++;
                if (pos >= this._jsonText.length) {
                    this._errorFound = true;

                    return pos;
                }

                pos = this.GetPropertyDivider(pos, currentPath);
                if (this._errorFound) {
                    return pos;
                }

                if (this._jsonText[pos] === ',' || this._jsonText[pos] === ']') { // it's an array of values
                    pos--;
                    newElement.JsonPropertyType = JsonPropertyType.ArrayValue;
                    newElement.EndPosition = pos;
                    newElement.Path = currentPath;
                    newElement.ValueType = this.GetVariableType(newName);
                    if (newElement.ValueType === JsonValueType.String) {
                        newElement.Value = this.TrimChar(newName, '\"')
                    }
                    else {
                        newElement.Value = newName;
                    }

                    return pos;
                }

                newElement.Name = this.TrimChar(newName, '\"');
                pos++;
                if (pos >= this._jsonText.length) {
                    this._errorFound = true;

                    return pos;
                }

                let valueStartPosition = pos;
                [pos, valueStartPosition] = this.GetPropertyValue(pos, currentPath);
                if (this._errorFound) {
                    return pos;
                }

                currentPath = currentPath + this.JsonPathDivider + newElement.Name;
                newElement.Path = currentPath;
                switch (this._jsonText[pos]) {
                    // it's an object
                    case '{':
                        newElement.JsonPropertyType = JsonPropertyType.Object;
                        pos = this.GetObject(pos, currentPath, false);
                        newElement.EndPosition = pos;
                        newElement.ValueType = JsonValueType.Object;
                        if (this.SaveComplexValues) {
                            newElement.Value = this._jsonText.substring(newElement.StartPosition,
                                newElement.EndPosition + 1);
                            if (this.TrimComplexValues) {
                                newElement.Value = this.TrimObjectValue(newElement.Value);
                            }
                        }

                        return pos;
                    // it's an array
                    case '[':
                        newElement.JsonPropertyType = JsonPropertyType.Array;
                        pos = this.GetArray(pos, currentPath);
                        newElement.EndPosition = pos;
                        newElement.ValueType = JsonValueType.Array;
                        if (this.SaveComplexValues) {
                            newElement.Value = this._jsonText.substring(newElement.StartPosition,
                                newElement.EndPosition + 1);
                            if (this.TrimComplexValues) {
                                newElement.Value = this.TrimArrayValue(newElement.Value);
                            }
                        }

                        return pos;
                    //  it's a property
                    default:
                        newElement.JsonPropertyType = JsonPropertyType.Property;
                        newElement.EndPosition = pos;
                        let newValue = this._jsonText
                            .substring(valueStartPosition, pos + 1)
                            .trim();
                        newElement.ValueType = this.GetVariableType(newValue);

                        if (newElement.ValueType === JsonValueType.String) {
                            newElement.Value = this.TrimChar(newValue, '\"');
                        }
                        else {
                            newElement.Value = newValue;
                        }
                        return pos;
                }
            }
            else if (this._endOfLineChars.includes(currentChar)) {
                this._errorFound = true;

                return pos;
            }
        }

        this._errorFound = true;
        return pos;
    }

    private GetPropertyDivider(pos: number, currentPath: string): number {
        for (; pos < this._jsonText.length; pos++) {
            switch (this._jsonText[pos]) {
                case ':':
                case ']':
                case ',':
                    return pos;
                case '/':
                    pos = this.GetComment(pos, currentPath);
                    break;
                default:
                    if (!this._allowedSpacerChars.includes(this._jsonText[pos])) {
                        this._errorFound = true;

                        return pos;
                    }
                    break;
            }
        }

        this._errorFound = true;
        return pos;
    }

    private GetPropertyValue(pos: number, currentPath: string): [number, number] {
        let propertyStartPos = pos;
        for (; pos < this._jsonText.length; pos++) {
            switch (this._jsonText[pos]) {
                case '[':
                //  it's a start of array
                case '{':
                    return [pos, propertyStartPos];
                case '/':
                    // it's a comment
                    pos = this.GetComment(pos, currentPath);
                    propertyStartPos = pos + 1;
                    break;
                case '\"':
                    pos++;
                    for (; pos < this._jsonText.length; pos++) {
                        if (this._jsonText[pos] === '\\') {
                            pos++;
                            if (pos >= this._jsonText.length) {
                                this._errorFound = true;

                                return [pos, propertyStartPos];
                            }

                            if (this._escapeChars.includes(this._jsonText[pos])) {
                                if (this._jsonText[pos] === 'u') { // if \u0000
                                    pos += 4;
                                }

                            }
                            else {
                                this._errorFound = true;

                                return [pos, propertyStartPos];
                            }
                        }
                        else if (this._jsonText[pos] === '\"') {
                            return [pos, propertyStartPos];
                        }
                        else if (this._endOfLineChars.includes(this._jsonText[pos])) { // check restricted chars
                            this._errorFound = true;

                            return [pos, propertyStartPos];
                        }
                    }

                    this._errorFound = true;

                    return [pos, propertyStartPos];
                default:
                    if (!this._allowedSpacerChars.includes(this._jsonText[pos])) // it's a property non-string value
                    {
                        //  ??? check this
                        const endingChars = [',', ']', '}', ' ', '\t', '\r', '\n', '/'];
                        for (; pos < this._jsonText.length; pos++) {
                            // value end found
                            if (endingChars.includes(this._jsonText[pos])) {
                                pos--;

                                return [pos, propertyStartPos];
                            }

                            // non-allowed char found
                            if (!this._keywordOrNumberChars.includes(this._jsonText[pos])) {
                                this._errorFound = true;

                                return [pos, propertyStartPos];
                            }
                        }
                    }
                    break;
            }
        }

        this._errorFound = true;
        return [pos, propertyStartPos];
    }

    private GetArray(pos: number, currentPath: string): number {
        pos++;
        let arrayIndex = 0;
        for (; pos < this._jsonText.length; pos++) {
            let foundObjectType: JsonPropertyType;
            [pos, foundObjectType] = this.FindStartOfNextToken(pos);
            if (this._errorFound) {
                return pos;
            }

            switch (foundObjectType) {
                case JsonPropertyType.Comment:
                    pos = this.GetComment(pos, currentPath + "[" + arrayIndex + "]");
                    arrayIndex++;
                    break;
                case JsonPropertyType.Property:
                    pos = this.GetPropertyName(pos, currentPath + "[" + arrayIndex + "]");
                    arrayIndex++;
                    break;
                case JsonPropertyType.Object:
                    pos = this.GetObject(pos, currentPath + "[" + arrayIndex + "]");
                    arrayIndex++;
                    break;
                case JsonPropertyType.KeywordOrNumberProperty:
                    pos = this.GetKeywordOrNumber(pos, currentPath + "[" + arrayIndex + "]", true);
                    arrayIndex++;
                    break;
                case JsonPropertyType.Array:
                    pos = this.GetArray(pos, currentPath);
                    break;
                case JsonPropertyType.EndOfArray:
                    if (this._searchMode && currentPath === this._searchPath) {
                        this._errorFound = true;
                    }

                    return pos;
                default:
                    this._errorFound = true;

                    return pos;
            }

            if (this._errorFound) {
                return pos;
            }
        }

        this._errorFound = true;
        return pos;
    }

    private GetObject(pos: number, currentPath: string, save: boolean = true): number {
        if (this._searchMode) {
            let lastItem = this._properties[this._properties.length - 1];
            if (lastItem.Path === this._searchPath) {
                if (this.SearchStartOnly
                    || (lastItem.JsonPropertyType !== JsonPropertyType.Array
                        && lastItem.JsonPropertyType !== JsonPropertyType.Object)) {
                    this._errorFound = true;

                    return pos;
                }

            }
            else {
                this._properties.splice(this._properties.length - 1, 1);
            }
        }

        let newElement = new JsonParsedProperty(this.JsonPathDivider);
        if (save) {
            newElement.StartPosition = pos;
            newElement.JsonPropertyType = JsonPropertyType.Object;
            newElement.Path = currentPath;
            newElement.ValueType = JsonValueType.Object;
            this._properties.push(newElement);
        }

        pos++;
        for (; pos < this._jsonText.length; pos++) {
            let foundObjectType: JsonPropertyType;
            [pos, foundObjectType] = this.FindStartOfNextToken(pos);
            if (this._errorFound) {
                return pos;
            }

            switch (foundObjectType) {
                case JsonPropertyType.Comment:
                    pos = this.GetComment(pos, currentPath);
                    break;
                case JsonPropertyType.Property:
                    pos = this.GetPropertyName(pos, currentPath);
                    break;
                case JsonPropertyType.Array:
                    pos = this.GetArray(pos, currentPath);
                    break;
                case JsonPropertyType.Object:
                    pos = this.GetObject(pos, currentPath);
                    break;
                case JsonPropertyType.EndOfObject:
                    if (save) {
                        newElement.EndPosition = pos;
                        if (this.SaveComplexValues) {
                            newElement.Value = this._jsonText.substring(newElement.StartPosition,
                                newElement.EndPosition + 1);

                            if (this.TrimComplexValues) {
                                newElement.Value = this.TrimObjectValue(newElement.Value);
                            }
                        }

                        if (this._searchMode && currentPath === this._searchPath) {
                            this._errorFound = true;

                            return pos;
                        }
                    }

                    return pos;
                default:
                    this._errorFound = true;

                    return pos;
            }

            if (this._errorFound) {
                return pos;
            }
        }

        this._errorFound = true;
        return pos;
    }

    private GetKeywordOrNumber(pos: number, currentPath: string, isArray: boolean): number {
        if (this._searchMode) {
            let lastItem = this._properties[this._properties.length - 1];
            if (lastItem.Path === this._searchPath) {
                if (this.SearchStartOnly
                    || (lastItem.JsonPropertyType !== JsonPropertyType.Array
                        && lastItem.JsonPropertyType !== JsonPropertyType.Object)) {
                    this._errorFound = true;
                    return pos;
                }
            }
            else {
                this._properties.splice(this._properties.length - 1, 1);
            }

        }

        let newElement = new JsonParsedProperty(this.JsonPathDivider);
        newElement.StartPosition = pos;
        this._properties.push(newElement);
        const endingChars = [',', '}', ']', '\r', '\n', '/'];

        for (; pos < this._jsonText.length; pos++) { // searching for token end
            let currentChar = this._jsonText[pos];
            //  end of token found
            if (endingChars.includes(currentChar)) {
                pos--;
                let newValue = this._jsonText
                    .substring(newElement.StartPosition, pos + 1)
                    .trim();

                if (!this._keywords.includes(newValue) && !this.IsNumeric(newValue)) {
                    this._errorFound = true;

                    return pos;
                }

                newElement.Value = newValue;
                if (isArray) {
                    newElement.JsonPropertyType = JsonPropertyType.ArrayValue;
                }
                else {
                    newElement.JsonPropertyType = JsonPropertyType.Property;
                }
                newElement.EndPosition = pos;
                newElement.Path = currentPath;
                newElement.ValueType = this.GetVariableType(newValue);

                return pos;
            }

            if (!this._keywordOrNumberChars.includes(currentChar)) {
                this._errorFound = true;

                return pos;
            }
        }

        this._errorFound = true;
        return pos;
    }

    public GetLinesNumber(jsonText: string, startPosition: number, endPosition: number): [boolean, number, number] {
        let startLine = this.CountLinesFast(jsonText, 0, startPosition) + 1;
        let endLine = startLine + this.CountLinesFast(jsonText, startPosition, endPosition);

        return [true, startLine, endLine];
    }

    public GetVariableType(str: string): JsonValueType {
        let type = JsonValueType.Unknown;

        if (str !== '') {
            if (str.length > 1 && str.startsWith('\"') && str.endsWith('\"')) {
                type = JsonValueType.String;
            }
            else if (str === "null") {
                type = JsonValueType.Null;
            }
            else if (str === "true" || str === "false") {
                type = JsonValueType.Boolean;
            }
            else if (this.IsNumeric(str)) {
                if (str.includes('.')) {
                    type = JsonValueType.Number;
                }
                else {
                    type = JsonValueType.Integer;
                }
            }
        }

        return type;
    }

    public TrimObjectValue(objectText: string): string {
        return this.TrimBracketedValue(objectText, '{', '}');
    }

    public TrimArrayValue(arrayText: string): string {
        return this.TrimBracketedValue(arrayText, '[', ']');
    }

    public TrimBracketedValue(text: string, startChar: string, endChar: string): string {
        if (text === '') {
            return text;
        }

        let startPosition = text.indexOf(startChar);
        let endPosition = text.lastIndexOf(endChar);

        if (startPosition < 0 || endPosition <= 0 || endPosition <= startPosition) {
            return text;
        }

        if (endPosition - startPosition <= 1) {
            return '';
        }

        return text.substring(startPosition + 1, endPosition - 1).trim();
    }

    //  fool-proof
    public CountLines(jsonText: string, startIndex: number, endIndex: number): number {
        if (startIndex >= jsonText.length) {
            return -1;
        }

        if (startIndex > endIndex) {
            let n = startIndex;
            startIndex = endIndex;
            endIndex = n;
        }

        if (endIndex >= jsonText.length) {
            endIndex = jsonText.length;
        }

        let linesCount = 0;
        for (; startIndex < endIndex; startIndex++) {
            if (!this._endOfLineChars.includes(jsonText[startIndex])) {
                continue;
            }

            linesCount++;
            if (startIndex < endIndex - 1
                && jsonText[startIndex] !== jsonText[startIndex + 1]
                && this._endOfLineChars.includes(jsonText[startIndex + 1])) {
                startIndex++;
            }
        }

        return linesCount;
    }

    public CountLinesFast(jsonText: string, startIndex: number, endIndex: number): number {
        let count = 0;

        while (jsonText.indexOf('\n', startIndex) !== -1 && startIndex < endIndex) {
            count++;
            startIndex++;
        }

        return count;
    }

    public ConvertForTreeProcessing(schemaProperties: JsonParsedProperty[]): JsonParsedProperty[] {
        let result: JsonParsedProperty[] = [];

        for (let property of schemaProperties) {
            let tmpStr = property.Path;
            let pos = tmpStr.indexOf('[');

            while (pos >= 0) {
                tmpStr = tmpStr.substring(0, pos) + this.JsonPathDivider + tmpStr.substring(pos);
                pos = tmpStr.indexOf('[', pos + 2);
            }

            let newProperty = new JsonParsedProperty(this.JsonPathDivider);
            newProperty.Name = property.Name;
            newProperty.Path = tmpStr;
            newProperty.JsonPropertyType = property.JsonPropertyType;
            newProperty.EndPosition = property.EndPosition;
            newProperty.StartPosition = property.StartPosition;
            newProperty.Value = property.Value;
            newProperty.ValueType = property.ValueType;

            result.push(newProperty);
        }

        return result;
    }

    private TrimChar(text: string, charToRemove: string): string {
        while (text.charAt(0) === charToRemove) {
            text = text.substring(1);
        }

        while (text.charAt(text.length - 1) === charToRemove) {
            text = text.substring(0, text.length - 1);
        }

        return text;
    }

    private IsNumeric(str: string): boolean {
        return !isNaN(Number(str));
    }
}