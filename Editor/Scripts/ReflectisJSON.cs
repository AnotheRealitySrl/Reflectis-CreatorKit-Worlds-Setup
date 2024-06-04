using System.Collections.Generic;

namespace Reflectis.SetupEditor
{
    [System.Serializable]
    public class ReflectisJSON
    {
        public List<ReflectisVersion> reflectisVersions;

        public void Print()
        {
            foreach (ReflectisVersion r in reflectisVersions)
            {
                r.Print();
            }
        }
    }
}
