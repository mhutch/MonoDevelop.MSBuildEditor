using System.Collections;
using System.Collections.Generic;

namespace MonoDevelop.Xml;

/*
public class OvertypeManager
{
	readonly Stack<Overtype> overtypes = new();

	public bool Insert (string text, int position)
	{
		var top = overtypes.Peek ();

		// if outside the range of the top overtype, discard it

		// if matches the top overtype, consume the characters and remove the overtype

		// update overtype positions
	}

	public bool UpdateCaretPosition (int position)
	{
		// if the caret is inside the range of the top overtype, update the caret position
	}

	public bool AddOvertype (string text, int position, bool isBeforeCaret)
	{
		overtypes.Push (new Overtype (text, position, isBeforeCaret));
	}
}

public class Overtype
{
	public string Text { get; }
	public int Position { get; }
	public bool IsBeforeCaret { get; }
}*/