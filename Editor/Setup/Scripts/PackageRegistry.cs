using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Reflectis.CreatorKit.Worlds.Setup.Editor
{
    [Serializable]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.Fields)]
    public class PackageRegistry
    {
        [SerializeField] private string reflectisVersion;
        [SerializeField] private string requiredUnityVersion;
        [SerializeField] private PackageDefinition[] packages;
        [SerializeField] private Dictionary<string, string[]> dependencies;

        public string ReflectisVersion => reflectisVersion;
        public string RequiredUnityVersion => requiredUnityVersion;
        public PackageDefinition[] Packages => packages;
        public Dictionary<string, string[]> Dependencies => dependencies;

        public Dictionary<string, PackageDefinition> PackageDictionary => Packages.ToDictionary(x => x.Name);

        public Dictionary<string, string[]> FullDependencies => dependencies.ToDictionary(
                kvp => kvp.Key,
                kvp => FindAllDependencies(PackageDictionary[kvp.Key], new List<string>()).ToArray()
            );

        public List<string> FindAllDependencies(PackageDefinition package)
        {
            return FindAllDependencies(package, new List<string>());
        }

        private List<string> FindAllDependencies(PackageDefinition package, List<string> dependencies)
        {
            if (Dependencies.TryGetValue(package.Name, out string[] packageDependencies))
            {
                foreach (string dependency in packageDependencies)
                {
                    FindAllDependencies(PackageDictionary[dependency], dependencies);
                }
                dependencies.AddRange(packageDependencies);
            }

            return dependencies;
        }


        public Dictionary<string, List<string>> ReverseDependencies => InvertDictionary(FullDependencies);



        private Dictionary<string, List<string>> InvertDictionary(Dictionary<string, string[]> dictionary)
        {
            var invertedDictionary = new Dictionary<string, List<string>>();

            foreach (var kvp in dictionary)
            {
                foreach (var value in kvp.Value)
                {
                    if (!invertedDictionary.ContainsKey(value))
                    {
                        invertedDictionary[value] = new List<string>();
                    }
                    invertedDictionary[value].Add(kvp.Key);
                }
            }

            return invertedDictionary;
        }
    }
}
