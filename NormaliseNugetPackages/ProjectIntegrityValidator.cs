using log4net;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace NormaliseNugetPackages
{
    public class ProjectIntegrityValidator
    {
        public static bool Validate(string repoRoot)
        {
            Logger.Info("Validating all csproj integrity");
            var projects = Directory.GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories);
            var anyFailure = false;

            foreach (var project in projects)
            {
                if (!ValidateProject(project))
                    anyFailure = true;
            }

            return anyFailure;
        }

        private static bool ValidateProject(string project)
        {
            XDocument xelement;
            try
            {
                xelement = XDocument.Load(new StreamReader(project, true));

            }
            catch (XmlException e)
            {
                var msg = $"XML Exception while loading {project}: {e.Message}";
                throw new ValidationException(msg);
            }

            var anyFailure = false;
            foreach (var element in xelement.Descendants("HintPath"))
            {
                if (element.Value.Contains(":") || element.Value.StartsWith("/"))
                {
                    Logger.Error($"Absolute path found in HintPath inside {project}: {element.Value}");
                    anyFailure = true;
                }
            }

            return anyFailure;
        }


        private static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    }
}