using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using log4net.Config;
using CommandLine;
using Newtonsoft.Json;

namespace NormaliseNugetPackages
{
    internal class Options
    {
        [Option('p', "path",
            HelpText = "Root to search for projects under.", Required = true)]
        public string Path { get; set; }

        [Option('r', "report",
            HelpText = "Path to a file to write a report of all used NuGet packages and their versions to (optional).", Required = false)]
        public string Report { get; set; }
    }

    internal struct PackageVersionDefinition
    {
        public string Name;
        public string Version;
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            Parser.Default.ParseArguments<Options>(args)
                .WithNotParsed(errors => Environment.Exit(1))
                .WithParsed(Execute);
        }

        private static void Execute(Options options)
        {
            try
            {
                var packageVersions = ConsistentVersionsValidator.Validate(options.Path);
                if (packageVersions == null)
                {
                    ExitWithError("One or more conflicting package versions found");
                }

                if (string.IsNullOrWhiteSpace(options.Report))
                    return;

                var printableVersions = packageVersions.Keys
                    .OrderBy(name => name)
                    .Select(k =>
                        new PackageVersionDefinition
                            { Name = k, Version = packageVersions[k].ToString()}
                        );

                var json = JsonConvert.SerializeObject(printableVersions, Formatting.Indented);

                Logger.Info($"Writing report to {options.Report}");
                File.WriteAllText(options.Report, json);
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
