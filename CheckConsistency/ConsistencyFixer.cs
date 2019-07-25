using log4net;
using Medallion.Shell;
using System;
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
            if (usageMatrix.PackagesThatNeedUpdating.Count == 0)
            {
                Logger.Info("All package versions are consistent");
                return;
            }

            var nugetExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nuget.exe");
            if (!File.Exists(nugetExe))
            {
                throw new Exception($"nuget.exe not found at {nugetExe}");
            }


            foreach (var packageUpgradeContext in usageMatrix.PackagesThatNeedUpdating)
            {
                var packageId = packageUpgradeContext.Key;
                var toVersion = packageUpgradeContext.Value.ToVersion;

                Logger.Info($"Upgrading {packageId} to {toVersion}");

                var projectsNeedingUpgrade = packageUpgradeContext.Value;
                foreach (var projectAtLowerVersion in projectsNeedingUpgrade.ProjectsAtLowerVersion)
                {
                    var project = projectAtLowerVersion.Project;

                    Logger.Info($"First restoring packages in project {project}");

                    RunCommand(nugetExe,
                        "restore", project, "-ConfigFile", config);

                    Logger.Info($"Upgrading project {project}");

                    if (Program.IsSDKStyleProject(project))
                    {
                        RunCommand("dotnet",
                            "add", project, "package", packageId, "-v", toVersion);
                    }
                    else
                    {
                        RunCommand(nugetExe,
                            "update", project, "-Id", packageId, "-Version", 
                            toVersion, "-ConfigFile", config);
                    }

                    Logger.Info($"Done upgrading {project}");
                }
            }
        }

        private static  void RunCommand(string executable, params object[] arguments)
        {
            var command = Command.Run(executable, arguments);
            Logger.Info($"Running: {command}");
            var result = command.Result;
            if (result.Success) 
                return;

            Logger.Info(result.StandardOutput);
            Logger.Error(result.StandardError);
            throw new Exception($"Command failed: {command}");
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

        protected static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    }
}