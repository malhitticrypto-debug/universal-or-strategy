#pragma warning disable SA1636 // File header copyright text should match
// <copyright file="LintingDummy.cs" company="BMad">
// Copyright (c) BMad. All rights reserved.
// </copyright>
#pragma warning restore SA1636

namespace Linting
{
    /// <summary>
    /// Dummy class to satisfy compilation requirements.
    /// EPIC-6: Minimal file to enable Linting.csproj compilation and StyleCop analyzer execution.
    /// </summary>
    internal static class LintingDummy
    {
        // Empty class - exists only to enable analyzer execution during build
    }
}
