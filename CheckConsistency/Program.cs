using CommandLine;
using log4net;
using log4net.Config;
using System;
using System.IO;
using System.Reflection;

namespace CheckConsistency
{
    internal class Options
    {
        [Option('p', "path",
            HelpText = "Root to search for projects under.", Required = true)]
        public string Path { get; set; }

        [Option('r', "report",
            HelpText = "Path to a file to write a report of all used NuGet packages and their versions to (optional).", Required = false)]
        public string Report { get; set; }

        [Option('f', "fix",
            HelpText = "Fix inconsistencies by upgrading highest package version used", Required = false)]
        public bool Fix { get; set; }
    }

    public struct PackageVersionDefinition
    {
        public string Name;
        public string Version;
    }

    internal class Program
    {
        public static bool ShouldProcessDirectory(string directoryToCheck)
        {
            while (true)
            {
                var skipMarker = Path.Combine(directoryToCheck, "SkipNuGetValidation");
                if (File.Exists(skipMarker))
                    return false;

                directoryToCheck = Directory.GetParent(directoryToCheck)?.FullName;
                if (directoryToCheck == null)
                    return true;
            }
        }


        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            Parser.Default.ParseArguments<Options>(args)
                .WithNotParsed(errors => Environment.Exit(1))
                .WithParsed(Execute);
        }

        private static void Execute(Options options)
        {
            if (!Directory.Exists(options.Path))
            {
                ExitWithError($"{options.Path} does not exist");
            }

            try
            {
                if (options.Fix)
                {
                    ConsistencyFixer.FixVersionsUnder(options.Path);
                    return;
                }

                var packageVersions = ConsistentVersionsValidator.Validate(options.Path);
                if (packageVersions == null)
                {
                    Logger.Info("NOTE: Non production directories can be excluded from this check by creating a file called 'SkipNuGetValidation' at the root of a tree to exclude.");
                    ExitWithError("One or more conflicting package versions found");
                }

                if (!string.IsNullOrWhiteSpace(options.Report))
                {
                    WriteVersionReport(options, packageVersions);
                }
            }
            catch (ValidationException e)
            {
                ExitWithError(e.Message);
            }

            if (!ProjectIntegrityValidator.Validate(options.Path))
            {
                ExitWithError("One or more .csproj files failed integrity check.");
            }
        }

        private static void WriteVersionReport(Options options, PackageCollection packageVersions)
        {
            var json = packageVersions.GetPrintableReport();

            Logger.Info($"Writing report to {options.Report}");
            File.WriteAllText(options.Report, json);
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
