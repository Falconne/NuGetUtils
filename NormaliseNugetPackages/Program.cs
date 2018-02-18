using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using log4net;
using log4net.Config;

namespace NormaliseNugetPackages
{
    // Checks all package.config files under given path for conflicting package versions
    internal class Program
    {
        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            if (args.Length != 1)
            {
                ExitWithError($"Usage: {AppDomain.CurrentDomain.FriendlyName} <path>");
            }

            var repoRoot = args[0];

            try
            {
                var packageVersions = ConsistentVersionsValidator.Validate(repoRoot);
                if (packageVersions == null)
                {
                    ExitWithError("One or more conflicting package versions found");
                }
            }
            catch (ValidationException e)
            {
                ExitWithError(e.Message);
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
