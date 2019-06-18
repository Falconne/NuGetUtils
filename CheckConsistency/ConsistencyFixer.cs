using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace CheckConsistency
{
    public class ConsistencyFixer
    {
        public static void FixVersionsUnder(string path)
        {
            var config = GetNuGetConfigFile(path);

            var usageMatrix = new PackageUsageMatrix();
            usageMatrix.Build(path);
            foreach (var packageUpgradeContext in usageMatrix.PackagesThatNeedUpdating)
            {
                var packageId = packageUpgradeContext.Key;
                var toVersion = packageUpgradeContext.Value.ToVersion;

                Logger.Info($"Upgrading {packageId} to {toVersion}");

                var projectsNeedingUpgrade = packageUpgradeContext.Value;
                foreach (var projectAtLowerVersion in
                    projectsNeedingUpgrade.ProjectsAtLowerVersion)
                {
                    var project = projectAtLowerVersion.Project;

                    Logger.Info($"First restoring packages in project {project}");

                    RunNuGet($"restore \"{project}\" -ConfigFile \"{config}\"");

                    Logger.Info($"Upgrading project {project}");
                    RunNuGet($"update \"{project}\" -Id {packageId} -Version {toVersion} -ConfigFile \"{config}\"");

                    Logger.Info($"Done upgrading {project}");
                }
            }
        }

        private static string GetNuGetConfigFile(string path)
        {
            Logger.Info("Looking for NuGet.config");
            var lookLocation = path;
            do
            {
                var possibleLocation = Path.Combine(lookLocation, "NuGet.config");
                if (File.Exists(possibleLocation))
                    return possibleLocation;

                lookLocation = Directory.GetParent(lookLocation)?.FullName;
            } while (!string.IsNullOrWhiteSpace(lookLocation));

            throw new ValidationException("Auto-fix currently only works with a NuGet.config");
        }

        private static void RunNuGet(string args)
        {
            var nugetExe = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "nuget.exe");

            if (!File.Exists(nugetExe))
            {
                throw new Exception($"nuget.exe not found at {nugetExe}");
            }

            var p = Process.Start(nugetExe, args);
            if (p == null)
                throw new Exception($"Failed to run {nugetExe}");

            p.WaitForExit();
            if (p.ExitCode == 0)
                return;

            var failMessage = $"Command failed: {nugetExe} {args}";
            Logger.Error(failMessage);
            throw new Exception(failMessage);
        }

        protected static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    }
}