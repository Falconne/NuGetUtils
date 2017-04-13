using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using log4net;

namespace NormaliseNugetPackages
{
    internal static class PackageConfigValidator
    {
        public static Dictionary<string, Version> Validate(string repoRoot)
        {
            if (!Directory.Exists(repoRoot))
            {
                throw new ValidationException($"{repoRoot} does not exist");
            }

            var packageFiles = Directory.GetFiles(repoRoot, "packages.config", SearchOption.AllDirectories);

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
                return packageVersions;
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

            return null;
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
                    throw new ValidationException($"Invalid syntax in {packageFile}");
                }

                if (!Version.TryParse(versionRaw, out Version version))
                {
                    Logger.Warn($"Ignoring unparsable version for {id}: {versionRaw} in {packageFile}");
                    continue;
                }

                yield return new KeyValuePair<string, Version>(id, version);
            }
        }

        private static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    }
}
