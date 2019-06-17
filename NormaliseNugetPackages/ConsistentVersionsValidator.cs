using log4net;
using System.Reflection;

namespace NormaliseNugetPackages
{
    internal static class ConsistentVersionsValidator
    {
        public static PackageCollection Validate(string repoRoot)
        {
            var usageMatrix = new PackageUsageMatrix();
            usageMatrix.Build(repoRoot);

            if (usageMatrix.PackagesThatNeedUpdating.Count == 0)
            {
                Logger.Info("All packages are upto date");
                return usageMatrix.PackageVersions;
            }

            foreach (var packageThatNeedsUpdating in usageMatrix.PackagesThatNeedUpdating)
            {
                var id = packageThatNeedsUpdating.Key;
                var projectsNeedingUpgrade = packageThatNeedsUpdating.Value;
                var version = projectsNeedingUpgrade.ToVersion;
                Logger.Error(" ");
                Logger.Error(" ");
                Logger.Error("=====================================================");
                Logger.Error($"These components need {id} updated to {version}:");
                foreach (var projectAtLowerVersion in projectsNeedingUpgrade.ProjectsAtLowerVersion)
                {
                    Logger.Error($"\t[from: {projectAtLowerVersion.Version}] {projectAtLowerVersion.Project}");
                }

                Logger.Error(" ");
                Logger.Error("These components are forcing this update:");
                foreach (var componentDir in usageMatrix.PackagesWithConsistentVersions[id])
                {
                    Logger.Error($"\t{componentDir}");
                }
            }

            return null;
        }


        private static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    }
}
