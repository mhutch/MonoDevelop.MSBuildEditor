// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MonoDevelop.MSBuild.Workspace
{
	/// <summary>
	/// Describes the different kinds of MSBuild files that can be determined by convention from the file extension.
	/// </summary>
	public enum MSBuildFileKind
	{
		/// <summary>
		/// The file kind is unknown.
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// The file is a <c>.targets</c> file and is intended to be loaded by a project file or another <c>.targets</c> file.
		/// </summary>
		Targets = 1,

		/// <summary>
		/// The file is a <c>.props</c> file and is intended to be loaded by a project file or another <c>.props</c> file.
		/// </summary>
		Props = 2,

		/// <summary>
		/// The file is a <c>.tasks</c> file and contains core task definitions.
		/// </summary>
		Tasks = 3,

		/// <summary>
		/// The file is a <c>.overridetasks</c> file and contains overrides for core task definitions.
		/// </summary>
		OverrideTasks = 4,

		/// <summary>
		/// The file is a .NET publish profile
		/// </summary>
		PubXml = 5,

		/// <summary>
		/// The file is a project file of unknown type.
		/// </summary>
		/// <remarks>
		/// This is also used as a flag by the values for more specific project types such as <see cref="CSProj"/>.
		/// Use <see cref="MSBuildFileKindExtensions.IsProject"/> to check if a file is a project file.
		/// </remarks>
		Project = 1024,

		/// <summary>
		/// The file is a C# project file.
		/// </summary>
		CSProj = Project + 1,

		/// <summary>
		/// The file is a Visual Basic project file.
		/// </summary>
		VBProj = Project + 2,

		/// <summary>
		/// The file is a F# project file.
		/// </summary>
		FSProj = Project + 3,

		/// <summary>
		/// The file is a generic MonoDevelop project file.
		/// </summary>
		XProj = Project + 4,

		/// <summary>
		/// The file is a C++ project file.
		/// </summary>
		VcxProj = Project + 5,

		/// <summary>
		/// The file is an Arcade shared framework project
		/// </summary>
		// https://github.com/dotnet/arcade/tree/19969f682c8cb42443ade22a42ffee4da7a8c2ca/src/Microsoft.DotNet.SharedFramework.Sdk
		SfxProj = Project + 6,

		/// <summary>
		/// If this flag is set, the file is a <c>.user</c> file used to store user-specific settings and customizations for a project file.
		/// </summary>
		/// <remarks>
		/// Use <see cref="MSBuildFileKindExtensions.IsUserProj"/> to check if a file is a user project file
		/// and get the <see cref="MSBuildFileKind"/> of the project file that is being customized.
		/// </remarks>
		UserProj = 2048,
	}
}