using System;
using System.Reflection;
using log4net;
using log4net.Config;
using CommandLine;
using log4net.Util;

namespace NormaliseNugetPackages
{
    internal class Options
    {
        [Option('p', "path", 
            HelpText = "Root to search for projects under", Required = true)]
        public string Path { get; set; }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            XmlConfigurator.Configure();

            Options options = null;
            Parser.Default.ParseArguments<Options>(args)
                .WithNotParsed(errors => Environment.Exit(1))
                .WithParsed(o => options = o);

            try
            {
                var packageVersions = ConsistentVersionsValidator.Validate(options.Path);
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
