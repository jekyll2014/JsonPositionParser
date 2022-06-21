import { readFileSync } from 'fs';
import JsonPathParser from './JsonPathParserLib/JsonPathParserLib';

let parser = new JsonPathParser('/');
// parser.TrimComplexValues = true;
// parser.SaveComplexValues = true;
const fileText = readFileSync('./sample.json', 'utf-8');
let [jsonProperties, endPosition, errorFound] = parser.ParseJsonToPathList(fileText);

// let convertedProperties = parser.ConvertForTreeProcessing(jsonProperties);

console.log(`File length: ${fileText.length}`);
console.log(`Last checked position: ${endPosition}`);
console.log(`Errors ${errorFound ? '' : 'not '}found`);
console.log(`Properties found: ${jsonProperties.length}:`);

let propNumber = jsonProperties.length > 10 ? 10 : jsonProperties.length;
for (let i = 0; i < propNumber; i++) {
    console.log(`${jsonProperties[i].Path}: ${jsonProperties[i].Value}`);
}

propNumber = jsonProperties.length - 10;
if (propNumber < 10) {
    propNumber = jsonProperties.length = propNumber;
}
else {
    console.log('...');
}

for (let i = propNumber - 1; i < jsonProperties.length; i++) {
    console.log(`${jsonProperties[i].Path}: ${jsonProperties[i].Value}`);
}