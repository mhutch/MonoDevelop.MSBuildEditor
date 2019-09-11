Things that still need to be ported from the MonoDevelop version:

MSBuild:

* find references command
* switch between related files command
* nuget completion
* polish the completion trigger/commit behavior

Xml:

* improve the background parser scheduling
* formatter
* convert parser errors to squiggles
* polish the completion trigger/commit behavior
* outlining tagger that's better than the one from textmate
* brace matching and auto-insertion
* closing element completion
* closing element matching
* implement overtype on auto-inserted =""
* auto-insertion of closing element and attribute =""
* document outline tool window
* breadcrumbs bar

Although there are hundred of unit tests covering the lower level details, there are few/no tests in the following areas:

* validation
* schema based completion
* completion for various value types
* function completion & resolution
* schema inference

Longer term:

* switch the XDom to use a red-green tree like Roslyn
* implement some incremental parsing. it should be fairly straightforward to do it for a subset of insertions, especially without < chars
