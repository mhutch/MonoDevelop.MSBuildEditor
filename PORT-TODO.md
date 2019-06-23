Things that still need to be ported:

MSBuild:

* value completion
* expression completion
* highlight references
* find references command
* go to definition command
* switch between related files command
* fix squiggle tooltips
* ctrl-click to navigate

Xml:

* improve the background parser scheduling
* smart indenter
* formatter
* convert parser errors to squiggles
* polish the completion
* better outliner
* brace matching
* automatic closing element insertion

Longer term:

* switch the XDom to use a red-green tree like Roslyn
* implement some incremental parsing. it should be fairly straightforward to do it for a subset of insertions, especially without < chars