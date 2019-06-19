// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Evaluation
{
    struct MSBuildPropertyValue
    {
		readonly IEnumerable<string> multiple;

        public MSBuildPropertyValue(string value)
        {
            Value = value;
            multiple = null;
        }

        public MSBuildPropertyValue(IEnumerable<string> multiple)
        {
            var enumerator = multiple.GetEnumerator();
            Value = null;
            if (enumerator.MoveNext())
            {
                Value = enumerator.Current;
            }
            this.multiple = multiple;
        }

        public static implicit operator MSBuildPropertyValue(string value)
        {
            return new MSBuildPropertyValue(value);
        }

        public string Value { get; }
        public bool HasMultipleValues => multiple != null;
        public IEnumerable<string> GetValues() => multiple ?? throw new InvalidOperationException();
    }
}