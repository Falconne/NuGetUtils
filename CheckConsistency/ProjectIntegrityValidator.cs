﻿using log4net;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CheckConsistency
{
    public class ProjectIntegrityValidator
    {
        public static bool Validate(string repoRoot)
        {
            Logger.Info("Validating all csproj integrity");
            var projects = Directory
                .GetFiles(repoRoot, "*.csproj", SearchOption.AllDirectories)
                .Where(Program.ShouldProcessDirectory);

            var anyFailure = false;

            foreach (var project in projects)
            {
                if (!ValidateProject(project))
                    anyFailure = true;
            }

            return !anyFailure;
        }

        private static bool ValidateProject(string project)
        {
            var anyFailure = false;
            foreach (var line in File.ReadAllLines(project))
            {
                var lineLower = line.ToLower();
                if (!lineLower.Contains("<hintpath>") && !lineLower.Contains("<projectreference"))
                    continue;

                if (line.Contains(":") || line.Contains("\"/"))
                {
                    Logger.Error($"Absolute path found inside {project}: {line}");
                    anyFailure = true;
                }
            }

            return !anyFailure;
        }


        private static readonly ILog Logger =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    }
}