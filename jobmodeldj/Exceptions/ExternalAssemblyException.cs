using System;

namespace jobmodeldj.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class ExternalAssemblyException : Exception
    {
        public ExternalAssemblyException() { }

        public ExternalAssemblyException(string noExternalAssemblyMessage) : base(String.Format("External Assembly Exception: {0}", noExternalAssemblyMessage))
        {
        }
    }
}
