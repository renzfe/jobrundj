using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jobmodeldj.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class VersionException : Exception
    {
        public VersionException() { }

        public VersionException(int versionCurrentJob, int versionJobEngine): base(String.Format("Invalid Job Version: {0} [Engine runtime version: {1}]", versionCurrentJob, versionJobEngine))
        {
        }
    }
}
