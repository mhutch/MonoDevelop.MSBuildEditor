// Copyright (c).NET Foundation and Contributors
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;

namespace ProjectFileTools.NuGetSearch.Contracts;

public class PackageType : IEquatable<PackageType>
{
    public static IReadOnlyList<PackageType> DefaultList { get; } = new[] { KnownPackageType.Dependency };

    public PackageType(string id, string? version = null)
    {
        Name = id ?? throw new ArgumentNullException(nameof(id));
        Version = version;
    }
    public string Name { get; }
    public string? Version { get; }


    public override bool Equals(object? obj) => Equals(obj as PackageType);

    public bool Equals(PackageType? other) => other is PackageType
        && string.Equals(Name, other.Name, StringComparison.Ordinal)
        && string.Equals(Version, other.Version, StringComparison.Ordinal);

    public override int GetHashCode()
    {
        int hashCode = -612338121;
        hashCode = hashCode * -1521134295 + StringComparer.Ordinal.GetHashCode(Name);
			if (Version is not null) {
				hashCode = hashCode * -1521134295 + StringComparer.Ordinal.GetHashCode(Version);
			}
        return hashCode;
    }
}

public class KnownPackageType
{
    public static PackageType Legacy { get; } = new PackageType("Legacy");
    public static PackageType DotnetCliTool { get; } = new PackageType("DotnetCliTool");
    public static PackageType Dependency { get; } = new PackageType("Dependency");
    public static PackageType DotnetTool { get; } = new PackageType("DotnetTool");
    public static PackageType SymbolsPackage { get; } = new PackageType("SymbolsPackage");
    public static PackageType DotnetPlatform { get; } = new PackageType("DotnetPlatform");
    public static PackageType MSBuildSdk { get; } = new PackageType("MSBuildSdk");
}