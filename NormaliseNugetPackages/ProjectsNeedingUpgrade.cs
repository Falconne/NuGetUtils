using NuGet.Versioning;
using System.Collections.Generic;

namespace NormaliseNugetPackages
{
    public class ProjectsNeedingUpgrade
    {
        public readonly NuGetVersion ToVersion;

        public readonly List<ProjectUsingPackageVersion> ProjectsAtLowerVersion =
            new List<ProjectUsingPackageVersion>();

        public ProjectsNeedingUpgrade(NuGetVersion toVersion)
        {
            ToVersion = toVersion;
        }
    }
}