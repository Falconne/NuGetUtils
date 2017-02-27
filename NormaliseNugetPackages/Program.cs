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

            // Identify components using older packages
            var invalidPackagesFound = false;
            foreach (var packageFile in packageFiles)
            {
                foreach (var versionedPackage in GetPackagesIn(packageFile))
                {
                    var id = versionedPackage.Key;
                    var version = versionedPackage.Value;
                    if (version >= packageVersions[id]) continue;
                    Logger.Error($"In {packageFile}:");
                    Logger.Error($"\t{id} should be version {packageVersions[id]}");
                    invalidPackagesFound = true;
                }
            }

            if (invalidPackagesFound)
                ExitWithError("One or more conflicting nuget versions found");
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
                    Logger.Warn($"Ignoring unparsable version for {id}: {versionRaw}");
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
