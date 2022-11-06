using System.Collections.Specialized;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.Loader;
using jobmodeldj.Model;
using jobmodeldj.Utils;
using CommandLine;
using NLog;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.CodeAnalysis.Emit;
using System.Xml.Linq;
using System.Data.Common;
using System.Data;

namespace jobrundj.Console
{
    /// <summary>
    /// Class to execute the job
    /// </summary>
    class Executer
    {
        private static Logger l = LogManager.GetCurrentClassLogger();
        private JobConfiguration conf = null;
        private ReferenceList References { get; } = new ReferenceList();
        public NamespaceList Namespaces { get; } = new NamespaceList();
        public Executer(JobConfiguration config)
        {
            conf= config;
        }
        public void Execute()
        {
            l.Debug("Parsing argument job..");
            Parser.Default.ParseArguments<Options>(conf.JobArgs)
                   .WithParsed<Options>(o =>
                   {
                       l.Info($"Configuring job to run: {o.JobName}");
                       conf.JobID = o.JobName;
                       ExecuteJobName();
                   });
        }

        private void ExecuteJobName()
        {
            IJob job = null;
            bool jobfound = false;

            l.Debug("Loading jobs from .cs file..");
            List<JobFile> externalJobsFiles = GetExternalJobs(conf.JobsDirectoryPath.FullName);
            l.Debug("Looking for cs file to compile..");
            foreach (JobFile jobFile in externalJobsFiles)
            {
                job = null;

                if (conf.JobID == jobFile.JobID)
                {
                    conf.JobFileReference = jobFile;

                    l.Debug("Compiling {0}..", jobFile.JobID);
                    job = CompileJob();

                    if (job != null && job.JobID == conf.JobID)
                    {
                        l.Debug("In file {0} is present JobID '{1}'", jobFile.JobFileInfo.FullName, conf.JobID);
                        jobfound = true;
                        break;
                    }
                }
            }
            
            if (jobfound)
            {
                l.Debug("Start loading other assemblies..");
                foreach (string assem in conf.JobFileReference.OtherAssemblies)
                {
                    if (!string.IsNullOrEmpty(assem))
                    {
                        string externalAssemblyFullName = GetAssemblyFullPath(assem, conf.ExternalDLLDirectoryPath.FullName, conf.DotNetDirectoryPath.FullName);
                        Assembly assembly = Assembly.LoadFrom(externalAssemblyFullName);
                        AppDomain.CurrentDomain.Load(assembly.GetName());
                    }
                }
                l.Debug("End loading other assemblies");
            }

            if (jobfound)
            {
                l.Debug("Start reading Configuration json..");
                try
                {
                    var jobConfJson = new ConfigurationBuilder()
                        .AddJsonFile(Path.Combine(conf.JobsDirectoryPath.FullName, conf.JobConfigurationJsonName), true)
                        .Build();
                    conf.Json = jobConfJson;
                }
                catch (Exception exconf)
                {
                    l.Debug("Configuration{0}",exconf.Message);
                }
            }

            if (jobfound)
            {
                job.MainExecute(conf);
            }
            else
            {
                throw new Exception("Job not found");
            }
        }

        private static List<JobFile> GetExternalJobs(string jobPath)
        {
            List<JobFile> list = new List<JobFile>();
            DirectoryInfo dllDir = new DirectoryInfo(jobPath);
            FileInfo[] files = dllDir.GetFiles("*.cs");
            
            foreach (FileInfo f in files)
            {
                JobFile jf = new JobFile(f);
                list.Add(jf);
            }

            return list;
        }

        private IJob CompileJob()
        {
            IJob oJob = null;
            string codeSourceString = conf.JobFileReference.JobSourceCode;
            l.Trace("source={0}", codeSourceString);

            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeSourceString, options);

            StringCollection refAssemblies = conf.JobFileReference.OtherAssemblies;
            foreach (string assem in refAssemblies)
            {
                if (!string.IsNullOrEmpty(assem))
                {
                    FileInfo dllinExternalDllDir = new FileInfo(Path.Combine(conf.ExternalDLLDirectoryPath.FullName, assem));
                    string assemblyFullPath = GetAssemblyFullPath(assem, conf.ExternalDLLDirectoryPath.FullName, conf.DotNetDirectoryPath.FullName);
                    l.Debug("adding {0}..", assemblyFullPath);
                    AddAssembly(assemblyFullPath);
                }
            }

            AddLoadedReferences();

            var csCompilation = CSharpCompilation.Create(conf.JobFileReference.JobID, new[] { parsedSyntaxTree }, References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            //Microsoft.CodeAnalysis.Scripting.ScriptOptions.Default.AddReferences(References);

            using (var peStream = new MemoryStream())
            {
                var debugOptions = DebugInformationFormat.Embedded; // DebugInformationFormat.Pdb;
                var result = csCompilation.Emit(peStream, options: new EmitOptions(debugInformationFormat: debugOptions)); //IN MEMORY OPTION//
                //var result = csCompilation.Emit(Path.Combine(conf.JobsTempDLLDirectoryPath.FullName, conf.JobAssembyName)); //FILE SYSTEM OPTION//

                if (result.Success)
                {
                    l.Debug("Compiled {0} successfully",conf.JobID);
                    AssemblyLoadContext assemblyContext = new AssemblyLoadContext(conf.JobFileReference.JobID, true); //IN MEMORY OPTION//
                    //AssemblyLoadContext assemblyContext = new AssemblyLoadContext(conf.JobFileReference.JobID, true); //FILE SYSTEM OPTION//

                    Assembly assembly = Assembly.Load(peStream.ToArray()); //IN MEMORY OPTION//
                    //Assembly assembly = assemblyContext.LoadFromAssemblyPath(Path.Combine(conf.JobsTempDLLDirectoryPath.FullName, conf.JobAssembyName)); //FILE SYSTEM OPTION//

                    var type = typeof(IJob);
                    Assembly[] assemblies = new Assembly[1];
                    assemblies[0] = assembly;
                    var types = assemblies
                        .SelectMany(s => s.GetTypes())
                        .Where(p => type.IsAssignableFrom(p));

                    oJob = _LookForIJob(types);
                    
                    if (oJob == null)
                    {
                        l.Warn("Not found a class with DFJobModel.IJob interface in {0}", conf.JobFileReference.JobID);
                    }
                }
                else
                {
                    l.Error("Compilation done with error.");
                    var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    foreach (var diagnostic in failures)
                    {
                        l.Error("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                    }
                }
            }

            if (oJob == null)
            {
                l.Warn("Not found a class with DFJobModel.IJob interface in {0}", conf.JobFileReference.JobFileInfo.FullName);
            }

            return oJob;
        }

        private static IJob _LookForIJob(IEnumerable<Type> types)
        {
            //it serach only the first class
            IJob o = null;
            foreach (var it in types)
            {
                o = null;
                l.Debug("check on {0}", it.FullName);
                if (it.IsClass && !it.IsAbstract)
                {
                    l.Debug("found a class with IJob interface");
                    o = (IJob)Activator.CreateInstance(it);
                    l.Debug("JobID={0}", o.JobID);
                    break;
                }
            }
            return o;
        }

        private static string GetAssemblyFullPath(string assemblyName, string externalDLLDirectoryPath, string dotNetDirectoryPath)
        {
            string fullPath = string.Empty;
            FileInfo f = new FileInfo(Path.Combine(externalDLLDirectoryPath, assemblyName));
            if (f.Exists)
            {
                fullPath = f.FullName;
            }
            else
            {
                f = new FileInfo(Path.Combine(dotNetDirectoryPath, assemblyName));
                if (f.Exists)
                {
                    fullPath = f.FullName;
                }
            }

            if (string.IsNullOrEmpty(fullPath))
            {
                throw new Exception($"{assemblyName} not found");
            } 

            return fullPath;
        }

        /// <summary>
        /// Explicitly adds all referenced assemblies of the currently executing
        /// process. Also adds default namespaces.
        ///
        /// Useful in .NET Core to ensure that all those little tiny system assemblies
        /// that comprise NetCoreApp.App etc. dependencies get pulled in.
        ///
        /// For full framework this is less important as the base runtime pulls
        /// in all the system and system.core types.
        ///
        /// Alternative: use LoadDefaultReferencesAndNamespaces() and manually add
        ///               
        /// </summary>
        private void AddLoadedReferences()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                try
                {
                    if (string.IsNullOrEmpty(assembly.Location)) continue;
                    AddAssembly(assembly.Location);
                }
                catch
                {
                }
            }

            AddAssembly("Microsoft.CSharp.dll"); // dynamic

            //#if NETCORE
            AddAssemblies(
                "System.Linq.Expressions.dll", // IMPORTANT!
                "System.Text.RegularExpressions.dll" // IMPORTANT!
            );
            //#endif

            AddNamespaces(Global.DefaultNamespaces);
        }

        /// <summary>
        /// Adds an assembly from disk. Provide a full path if possible
        /// or a path that can resolve as part of the application folder
        /// or the runtime folder.
        /// </summary>
        /// <param name="assemblyDll">assembly DLL name. Path is required if not in startup or .NET assembly folder</param>
        private bool AddAssembly(string assemblyDll)
        {
            if (string.IsNullOrEmpty(assemblyDll)) return false;

            var file = Path.GetFullPath(assemblyDll);

            if (!File.Exists(file))
            {
                // check framework or dedicated runtime app folder
                var path = Path.GetDirectoryName(typeof(object).Assembly.Location);
                file = Path.Combine(path, assemblyDll);
                if (!File.Exists(file))
                    return false;
            }

            if (References.Any(r => r.FilePath == file)) return true;

            try
            {
                var reference = MetadataReference.CreateFromFile(file);
                References.Add(reference);
            }
            catch
            {
                return false;
            }

            return true;
        }

        private bool AddAssembly(PortableExecutableReference reference)
        {
            if (References.Any(r => r.FilePath == reference.FilePath))
                return true;

            References.Add(reference);
            return true;
        }

        /// <summary>
        /// Adds an assembly reference from an existing type
        /// </summary>
        /// <param name="type">any .NET type that can be referenced in the current application</param>
        private bool AddAssembly(Type type)
        {
            try
            {
                if (References.Any(r => r.FilePath == type.Assembly.Location))
                    return true;

                var systemReference = MetadataReference.CreateFromFile(type.Assembly.Location);
                References.Add(systemReference);
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds a list of assemblies to the References
        /// collection.
        /// </summary>
        /// <param name="assemblies"></param>
        private void AddAssemblies(params string[] assemblies)
        {
            foreach (var file in assemblies)
                AddAssembly(file);
        }

        /// <summary>
        /// Adds a namespace to the referenced namespaces
        /// used at compile time.
        /// </summary>
        /// <param name="nameSpace"></param>
        private void AddNamespace(string nameSpace)
        {
            if (string.IsNullOrEmpty(nameSpace))
            {
                Namespaces.Clear();
                return;
            }

            if (!Namespaces.Contains(nameSpace))
                Namespaces.Add(nameSpace);
        }

        /// <summary>
        /// Adds a set of namespace to the referenced namespaces
        /// used at compile time.
        /// </summary>
        private void AddNamespaces(params string[] namespaces)
        {
            foreach (var ns in namespaces)
            {
                if (!string.IsNullOrEmpty(ns))
                    AddNamespace(ns);
            }
        }

    }
}
