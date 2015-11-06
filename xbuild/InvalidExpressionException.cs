using System;
using System.Runtime.Serialization;

namespace Microsoft.Build.BuildEngine
{
	[Serializable]
	class InvalidExpressionException : Exception
	{
		public InvalidExpressionException ()
		{
		}

		public InvalidExpressionException (string message) : base (message)
		{
		}

		public InvalidExpressionException (string message, Exception innerException) : base (message, innerException)
		{
		}

		protected InvalidExpressionException (SerializationInfo info, StreamingContext context) : base (info, context)
		{
		}
	}
}