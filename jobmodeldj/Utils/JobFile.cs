using jobmodeldj.Exceptions;
using NLog;
using System.Collections.Specialized;

namespace jobmodeldj.Utils
{
    public class JobFile
    {
        private static Logger l = LogManager.GetCurrentClassLogger();

        private string _id;
        private FileInfo _fileInfo;
        private string _code;
        private StringCollection _otherAssemblies = new StringCollection(); 
        public string JobID
        {
            get { return _id; } 
        }
        public FileInfo JobFileInfo
        {
            get { return _fileInfo; }
        }
        public string JobSourceCode
        {
            get { return _code; }
        }
        public StringCollection OtherAssemblies
        {
            get { return _otherAssemblies; }
        }

        public JobFile(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
            string noext = Path.GetFileNameWithoutExtension(_fileInfo.FullName);
            _id = noext;
            _setSourceCode();
            _setExtractAssemblies();
        }

        private void _setSourceCode()
        {
            try
            {
                l.Debug("Opening source {0} ..", _fileInfo.FullName);
                string code = File.ReadAllText(_fileInfo.FullName);
                _code = code;
            }
            catch (Exception ex)
            {
                l.Warn("JobID:{0}={1}", this._id, ex.Message);
            }

        }

        private void _setExtractAssemblies()
        {
            StringCollection refAssembles = new StringCollection();
            try
            {
                string s = this.JobSourceCode;

                var startTag = string.Format("<{0}>", Global.TAG_FOR_EXTERNAL_ASSEMBLY);
                var endTag = string.Format("</{0}>", Global.TAG_FOR_EXTERNAL_ASSEMBLY);
                int startIndex = s.IndexOf(startTag) + startTag.Length;
                if (startIndex < 32) throw new ExternalAssemblyException("start tag not found");
                int endIndex = s.IndexOf(endTag, startIndex);
                if (endIndex == -1) throw new ExternalAssemblyException("end tag not found");
                string list = s.Substring(startIndex, endIndex - startIndex);
                l.Debug("ExtractAssemblies={0}", list);
                refAssembles.AddRange(list.Split(','));
            }
            catch (ExternalAssemblyException exex)
            {
                l.Debug("JobID:{0}={1}", this._id, exex.Message);
            }
            catch (Exception ex)
            {
                l.Warn("{0}", ex.Message);
                l.Debug("JobID:{0}={1}", this._id, ex.Message);
            }

            this._otherAssemblies = refAssembles;
        }
    }
}
