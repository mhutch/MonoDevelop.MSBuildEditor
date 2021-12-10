// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// Implementations for methods made partial in the imported class IntrinsicFunctions

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Evaluation
{
	partial class IntrinsicFunctions
	{
		internal static partial bool DoesTaskHostExist (string runtime, string architecture) => true;
	}
}
