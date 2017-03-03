using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using log4net;
using log4net.Config;

namespace NormaliseNugetPackages
{
    // Checks all package.config files under given path for conflicting versions
    internal class Program
    {
        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            if (args.Length != 1)
            {
                ExitWithError($"Usage: {AppDomain.CurrentDomain.FriendlyName} <path>");
            }

            var root = args[0];
            if (!Directory.Exists(root))
            {
                ExitWithError($"{root} does not exist");
            }

            var packageFiles = Directory.GetFiles(root, "packages.config", SearchOption.AllDirectories);

            var packageVersions = new Dictionary<string, Version>();

            // Find the latest version used for all packages
            foreach (var packageFile in packageFiles)
            {
                foreach (var versionedPackage in GetPackagesIn(packageFile))
                {
                    var id = versionedPackage.Key;
                    var version = versionedPackage.Value;
                    if (packageVersions.ContainsKey(id))
                    {
                        if (version <= packageVersions[id])
                        {
                            continue;
                        }
                        Logger.Debug($"UPDATE {id} to {version} ");
                    }
                    else
                    {
                        Logger.Debug($"Add {id} ({version}) ");
                    }

                    packageVersions[id] = version;
                }
            }

            var packagesThatNeedUpdating = new Dictionary<string, List<string>>();
            // Identify components using older packages
            foreach (var packageFile in packageFiles)
            {
                foreach (var versionedPackage in GetPackagesIn(packageFile))
                {
                    var id = versionedPackage.Key;
                    var version = versionedPackage.Value;
                    if (version >= packageVersions[id]) continue;
                    Logger.Debug($"In {packageFile}:");
                    Logger.Debug($"\t{id} should be version {packageVersions[id]}");
                    if (!packagesThatNeedUpdating.ContainsKey(id))
                        packagesThatNeedUpdating[id] = new List<string>();

                    packagesThatNeedUpdating[id].Add(Path.GetDirectoryName(packageFile));
                }
            }

            if (packagesThatNeedUpdating.Count == 0)
            {
                Logger.Info("All packages are upto date");
                return;
            }

            foreach (var packageThatNeedsUpdating in packagesThatNeedUpdating)
            {
                var id = packageThatNeedsUpdating.Key;
                var version = packageVersions[id];
                Logger.Error($"These components need {id} updated to {version}");
                foreach (var packageConfig in packageThatNeedsUpdating.Value)
                {
                    Logger.Error($"\t{packageConfig}");
                }
            }
            ExitWithError("One or more conflicting package versions found");
        }

        private static IEnumerable<KeyValuePair<string, Version>> GetPackagesIn(string packageFile)
        {
            var xelement = XElement.Load(packageFile);
            var packages = xelement.Descendants("package");
            foreach (var packageElement in packages)
            {
                var id = packageElement.Attribute("id")?.Value;
                var versionRaw = packageElement.Attribute("version")?.Value;
                if (id == null || versionRaw == null)
                {
                    ExitWithError($"Invalid syntax in {packageFile}");
                }
                Version version;
                if (!Version.TryParse(versionRaw, out version))
                {
                    Logger.Warn($"Ignoring unparsable version for {id}: {versionRaw} in {packageFile}");
                    continue;
                }

                yield return new KeyValuePair<string, Version>(id, version);
            }
        }

        private static void ExitWithError(string message)
        {
            Logger.Error(message);
            Environment.Exit(1);
        }

        protected static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    }
}
