using log4net;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace CheckConsistency
{
    public class PackageUsageMatrix
    {
        public readonly Dictionary<string, List<string>> PackagesWithConsistentVersions =
            new Dictionary<string, List<string>>();

        public readonly Dictionary<string, ProjectsNeedingUpgrade> PackagesThatNeedUpdating =
            new Dictionary<string, ProjectsNeedingUpgrade>();

        public readonly PackageCollection PackageVersions = new PackageCollection();

        public void Build(string path)
        {
            Logger.Info("Building package usage matrix...");

            var packageFiles = Directory.GetFiles(
                    path, "packages.config", SearchOption.AllDirectories)
                .Where(ShouldProcessProject)
                .ToList();

            var newFormatProjects = Directory.GetFiles(
                    path, "*.csproj", SearchOption.AllDirectories)
                .Where(ShouldProcessProject);

            packageFiles.AddRange(newFormatProjects);

            // Find the latest version used for all packages
            foreach (var packageFile in packageFiles)
            {
                foreach (var versionedPackage in GetPackagesIn(packageFile))
                {
                    PackageVersions.AddOrUpdatePackage(
                        versionedPackage.Key, versionedPackage.Value);
                }
            }

            // Identify components using older packages
            foreach (var packageFile in packageFiles)
            {
                foreach (var versionedPackage in GetPackagesIn(packageFile))
                {
                    var id = versionedPackage.Key;
                    var version = versionedPackage.Value;
                    var latestVersion = PackageVersions.GetVersionOf(id);
                    if (version >= latestVersion)
                    {
                        if (!PackagesWithConsistentVersions.ContainsKey(id))
                            PackagesWithConsistentVersions[id] = new List<string>();

                        PackagesWithConsistentVersions[id].Add(
                            Path.GetDirectoryName(packageFile));

                        continue;
                    }

                    Logger.Debug($"In {packageFile}:");
                    Logger.Debug($"\t{id} should be version {PackageVersions.GetVersionOf(id)}");

                    if (!PackagesThatNeedUpdating.ContainsKey(id))
                    {
                        PackagesThatNeedUpdating[id] =
                            new ProjectsNeedingUpgrade(latestVersion);
                    }

                    var project = packageFile;
                    if (project.ToLower().EndsWith("packages.config"))
                    {
                        var projectDir = Path.GetDirectoryName(packageFile);
                        var projects = Directory.GetFiles(
                            projectDir, "*.csproj");
                        if (projects.Length != 1)
                        {
                            throw new ValidationException(
                                $"Multiple projects found in {projectDir}");
                        }

                        project = projects.First();
                    }

                    var projectToAdd = new ProjectUsingPackageVersion(project, version);
                    PackagesThatNeedUpdating[id].ProjectsAtLowerVersion.Add(projectToAdd);
                }
            }

        }

        private static bool ShouldProcessProject(string packageDefinitionFile)
        {
            var directory = Path.GetDirectoryName(packageDefinitionFile);
            if (!Program.ShouldProcessDirectory(directory))
                return false;

            if (packageDefinitionFile.ToLower().EndsWith("packages.config"))
                return true;

            // Reject any csproj that doesn't use PackageReferences
            var csprojContent = File.ReadAllText(packageDefinitionFile);
            return csprojContent.Contains("PackageReference");
        }

        private static IEnumerable<KeyValuePair<string, NuGetVersion>>
            GetPackagesIn(string packageFile)
        {
            XDocument xelement;
            try
            {
                xelement = XDocument.Load(
                    new StreamReader(packageFile, true));
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
                    var id =
                        packageReference.Attribute("Include")?.Value
                        ??
                        packageReference.Attribute("Update")?.Value;

                    if (id == null)
                    {
                        throw new ValidationException(
                            $"Invalid syntax in {packageFile}: PackageReference with no id");
                    }

                    var versionRaw = packageReference.Attribute("Version")?.Value;

                    if (versionRaw == null)
                    {
                        if (id.Equals(
                            "Microsoft.AspNetCore.App",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            // This one is special.
                            // See https://github.com/aspnet/Docs/issues/6430
                            continue;
                        }

                        throw new ValidationException(
                            $"A specific version has not been provided for {id} in {packageFile}");
                    }

                    var result = GetParsedVersion(versionRaw, id);
                    if (result == null)
                        continue;

                    yield return (KeyValuePair<string, NuGetVersion>)result;
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

                    var result = GetParsedVersion(versionRaw, id);
                    if (result == null)
                        continue;

                    yield return (KeyValuePair<string, NuGetVersion>)result;
                }
            }
        }

        private static KeyValuePair<string, NuGetVersion>?
            GetParsedVersion(string versionRaw, string packageId)
        {
            try
            {
                var version = NuGetVersion.Parse(versionRaw);
                return new KeyValuePair<string, NuGetVersion>(packageId, version);
            }
            catch (Exception)
            {
                Logger.Warn($"Ignoring unparsable version for {packageId}: {versionRaw}");
            }

            return null;
        }


        private static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    }
}