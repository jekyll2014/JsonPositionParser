# JsonPositionParser
JSON parser with objects position output feature

Making my JSON validators I've found out that there is no way to link a json parsed property to original json text position.
Had to make my own.

It is now possible to show property position in the text by it's json path.
See test project as an example.

As a side effect it is possible to run simple validation like finding duplicate property names within object (which is not reported by MS or NewtonSoft parsers but simply last entry taken as final value).
