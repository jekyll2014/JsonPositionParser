import { JsonPropertyType } from "./JsonPropertyType";
import { JsonValueType } from "./JsonValueTypes";

export default class ParsedProperty {

    public StartPosition: number = -1;
    public EndPosition: number = -1;
    public Name: string = "";
    public Value: string = "";
    public JsonPropertyType: JsonPropertyType = JsonPropertyType.Unknown;
    public ValueType: JsonValueType;
    public PathDivider: string = '.';

    public constructor(pathDivider: string) {
        this.PathDivider = pathDivider;
    }

    private _path: string = "";
    public set Path(value: string) {
        this._parentPath == null;
        this._path = value;
    }

    public get Path(): string {

        return this._path;
    }

    private _parentPath: string | null;
    public get ParentPath(): string {
        if ((this._parentPath == null)) {
            this._parentPath = ParsedProperty.TrimPathEnd(this._path, 1, this.PathDivider);
        }

        return this._parentPath;
    }

    public get RawLength(): number {
        if (((this.StartPosition == -1)
            || (this.EndPosition == -1))) {
            return -1;
        }

        return ((this.EndPosition - this.StartPosition)
            + 1);
    }

    private static TrimPathEnd(originalPath: string, levels: number, pathDivider: string): string {
        for (
            ; levels > 0; levels--) {
            let pos = originalPath.lastIndexOf(pathDivider);
            if ((pos >= 0)) {
                originalPath = originalPath.substring(0, pos);
            }
            else {
                break;
            }

        }

        return originalPath;
    }

    public ToString(): string {
        return this._path;
    }
}