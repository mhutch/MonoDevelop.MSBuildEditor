using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.BuildEngine
{
	class InvalidExpressionError
	{
		public int Position { get; set; }
		public string Message { get; set; }

		public InvalidExpressionError (string message, int position)
		{
			Message = message;
			Position = position;
		}
	}
}