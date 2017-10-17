using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
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

            var packageFiles = Directory.GetFiles(repoRoot, "packages.config", SearchOption.AllDirectories)
                .Where(ShouldProcessPackage)
                .ToList();

            var newFormatProjects = Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(ShouldProcessPackage);

            packageFiles.AddRange(newFormatProjects);

            var packageVersions = new Dictionary<string, Version>();

            // Find the latest version used for all packages
            foreach (var packageFile in packageFiles)
            {
                var directory = Path.GetDirectoryName(packageFile);
                var skipMarker = Path.Combine(directory, "SkipNuGetValidation");
                if (File.Exists(skipMarker))
                    continue;

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

            var packagesThatNeedUpdating = new Dictionary<string, List<(string, Version)>>();
            var uptoDatePackages = new Dictionary<string, List<string>>();
            // Identify components using older packages
            foreach (var packageFile in packageFiles)
            {
                foreach (var versionedPackage in GetPackagesIn(packageFile))
                {
                    var id = versionedPackage.Key;
                    var version = versionedPackage.Value;
                    if (version >= packageVersions[id])
                    {
                        if (!uptoDatePackages.ContainsKey(id))
                            uptoDatePackages[id] = new List<string>();

                        uptoDatePackages[id].Add(Path.GetDirectoryName(packageFile));

                        continue;
                    }
                    Logger.Debug($"In {packageFile}:");
                    Logger.Debug($"\t{id} should be version {packageVersions[id]}");
                    if (!packagesThatNeedUpdating.ContainsKey(id))
                        packagesThatNeedUpdating[id] = new List<(string, Version)>();

                    packagesThatNeedUpdating[id].Add((Path.GetDirectoryName(packageFile), version));
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
                string downgradeToVersion = null;
                bool canDoGenericDowngrade = true; 
                Logger.Error(" ");
                Logger.Error(" ");
                Logger.Error("=====================================================");
                Logger.Error($"These components need {id} updated to {version}:");
                foreach (var packageConfig in packageThatNeedsUpdating.Value)
                {
                    Logger.Error($"\t[from: {packageConfig.Item2}] {packageConfig.Item1}");
                    if (downgradeToVersion == null)
                    {
                        downgradeToVersion = packageConfig.Item2.ToString();
                    }
                    else if (canDoGenericDowngrade)
                    {
                        if (downgradeToVersion != packageConfig.Item2.ToString())
                        {
                            canDoGenericDowngrade = false;
                        }
                    }
                    
                }

                Logger.Error(" ");
                Logger.Error("These components are forcing this update:");
                var uptoDatePackageList = uptoDatePackages[id];
                foreach (var packageConfig in uptoDatePackageList)
                {
                    Logger.Error($"\t{packageConfig}");
                }

                Logger.Error(" ");
                Logger.Error(
                    "Package Manager Console command to UPGRADE all projects in a solution:");
                Logger.Error($"\tUpdate-Package {id} -Version {version}");
                if (canDoGenericDowngrade)
                {
                    Logger.Error(" ");
                    Logger.Error(
                        "Package Manager Console command to DOWNGRADE all projects in a solution:");
                    Logger.Error($"\tUpdate-Package {id} -Version {downgradeToVersion}");
                }
            }

            return null;
        }

        private static bool ShouldProcessPackage(string packageDefinitionFile)
        {
            var directory = Path.GetDirectoryName(packageDefinitionFile);
            var skipMarker = Path.Combine(directory, "SkipNuGetValidation");
            if (File.Exists(skipMarker))
                return false;

            if (packageDefinitionFile.ToLower().EndsWith("packages.config"))
                return true;

            // Reject any csproj that's not in the new 2017 format
            var csprojContent = File.ReadAllText(packageDefinitionFile);
            const string newFormatMarker = "<Project Sdk=\"Microsoft.NET.Sdk\">";
            return csprojContent.Contains(newFormatMarker);
        }

        private static IEnumerable<KeyValuePair<string, Version>> GetPackagesIn(string packageFile)
        {
            XDocument xelement;
            try
            {
                xelement = XDocument.Load(new StreamReader(packageFile, true));

            }
            catch (XmlException e)
            {
                var msg = $"XML Exception while loading {packageFile}: {e.Message}";
                throw new ValidationException(msg);
            }
            if (packageFile.ToLower().EndsWith(".csproj"))
            {
                var packageReferences = xelement.Descendants("PackageReference");
                foreach (var packageReference in packageReferences)
                {
                    var id = packageReference.Attribute("Include")?.Value;
                    if (id == null)
                    {
                        id = packageReference.Attribute("Update")?.Value;
                    }
                    var versionRaw = packageReference.Attribute("Version")?.Value;
                    if (id == null || versionRaw == null)
                    {
                        throw new ValidationException($"Invalid syntax in {packageFile}");
                    }

                    if (!Version.TryParse(versionRaw, out Version version))
                    {
                        Logger.Warn($"Ignoring unparsable version for {id}: {versionRaw}");
                        continue;
                    }

                    yield return new KeyValuePair<string, Version>(id, version);
                }
            }
            else
            {
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
                        Logger.Warn($"Ignoring unparsable version for {id}: {versionRaw}");
                        continue;
                    }

                    yield return new KeyValuePair<string, Version>(id, version);
                }
            }
        }

        private static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    }
}
