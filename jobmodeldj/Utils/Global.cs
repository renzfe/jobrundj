namespace jobmodeldj.Utils
{
    /// <summary>
    /// Class for global stuff and const
    /// </summary>
    public static class Global
    {
        public const string JOBS_FOLDER_NAME = "jobs";
        public const string TEMP_FOLDER_NAME = "temp";
        public const string REFERENCE_DLL_FOLDER_NAME = "externaldll";
        public const string DYNAMIC_LINKED_LIBRARY_EXTENSION = "dll";
        public const string JSON_EXTENSION = "json";
        public const string TAG_FOR_EXTERNAL_ASSEMBLY = "jobrundjReferencedAssemblies";
        public const int JOBS_RUNTIME_VERSION = 3;
        public static readonly string[] DefaultNamespaces =
        {
            "System", "System.Text", "System.Reflection", "System.IO", "System.Net", "System.Net.Http",
            "System.Collections", "System.Collections.Generic", "System.Collections.Concurrent",
            "System.Text.RegularExpressions", "System.Threading.Tasks", "System.Linq"
        }; 
    }
}
