using jobmodeldj.Utils;

namespace jobmodeldj.Model
{
    /// <summary>
    /// Configuration stuff
    /// </summary>
    public class JobConfiguration
    {
        public string[] JobArgs { get { return _args; } }
        public string JobID { get { return _id; } set { this._id = value; } }
        public string JobAssembyName 
        {
            get 
            {
                string candidateDLLName = string.Format("{0}.{1}", _id, Global.DYNAMIC_LINKED_LIBRARY_EXTENSION);
                return candidateDLLName;
            }
        }
        public JobFile JobFileReference { get { return _jobFile; } set { this._jobFile = value; } }
        public DirectoryInfo AppDirectoryPath { get { return _appDirectoryPath; } }
        public DirectoryInfo JobsDirectoryPath { get { return _jobsDirectoryPath; } }
        public DirectoryInfo JobsTempDLLDirectoryPath { get { return _jobsTempDLLDirectoryPath; } }
        public DirectoryInfo TempDirectoryPath { get { return _tempDirectoryPath; } }
        public DirectoryInfo ExternalDLLDirectoryPath { get { return _externalDLLDirectoryPath; } }
        public string ExecuterFileName { get { return _executerFileName; } }
        public NLog.Config.LoggingConfiguration? logConfig;

        private readonly string[] _args;
        private string _id = string.Empty;
        private DirectoryInfo _appDirectoryPath;
        private DirectoryInfo _jobsDirectoryPath; 
        private DirectoryInfo _jobsTempDLLDirectoryPath; 
        private DirectoryInfo _tempDirectoryPath;
        private DirectoryInfo _externalDLLDirectoryPath;
        private string _executerFileName;
        private JobFile? _jobFile;

        public JobConfiguration(string[] args, string executerFileName, string appDirectoryPath, string jobsDirectoryPath, string dllTempDirectoryPath, string tempDirectoryPath, string externaldlljobsDirecoryPath)
        {
            _args = args;
            _executerFileName = executerFileName;
            _appDirectoryPath = new DirectoryInfo(appDirectoryPath);
            if (!_appDirectoryPath.Exists) _appDirectoryPath.Create();

            _jobsDirectoryPath = new DirectoryInfo(jobsDirectoryPath);
            if (!_jobsDirectoryPath.Exists) _jobsDirectoryPath.Create();

            _jobsTempDLLDirectoryPath = new DirectoryInfo(dllTempDirectoryPath);
            if (!_jobsTempDLLDirectoryPath.Exists) _jobsTempDLLDirectoryPath.Create();

            _tempDirectoryPath = new DirectoryInfo(tempDirectoryPath);
            if (!_tempDirectoryPath.Exists) _tempDirectoryPath.Create();

            _externalDLLDirectoryPath = new DirectoryInfo(externaldlljobsDirecoryPath);
            if (!_externalDLLDirectoryPath.Exists) _externalDLLDirectoryPath.Create();
        }
    }
}
