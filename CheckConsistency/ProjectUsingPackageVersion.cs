using NuGet.Versioning;

namespace CheckConsistency
{
    public class ProjectUsingPackageVersion
    {
        public readonly string Project;

        public readonly NuGetVersion Version;

        public ProjectUsingPackageVersion(string project, NuGetVersion version)
        {
            Project = project;
            Version = version;
        }
    }
}