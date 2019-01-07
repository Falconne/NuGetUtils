using Newtonsoft.Json;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NormaliseNugetPackages
{
    public class PackageCollection
    {
        public Dictionary<string, NuGetVersion> Collection =
            new Dictionary<string, NuGetVersion>();

        public void AddOrUpdatePackage(string id, NuGetVersion version)
        {
            if (Collection.ContainsKey(id) && version <= Collection[id])
            {
                return;
            }

            Collection[id] = version;
        }

        public NuGetVersion GetVersionOf(string id)
        {
            return Collection[id];
        }

        public string GetPrintableReport()
        {
            var printableVersions = Collection.Keys
                .OrderBy(name => name)
                .Select(k =>
                    new PackageVersionDefinition
                        { Name = k, Version = Collection[k].ToString() }
                );

            return JsonConvert.SerializeObject(printableVersions, Formatting.Indented);
        }
    }
}