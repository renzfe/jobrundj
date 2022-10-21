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

namespace jobrundj.Console
{
    /// <summary>
    /// Main static class to execute jobs
    /// </summary>
    static class Executer
    {
        private static Logger l = LogManager.GetCurrentClassLogger();
        public static void Execute(JobConfiguration config)
        {
            l.Debug("Parsing argument job..");
            Parser.Default.ParseArguments<Options>(config.JobArgs)
                   .WithParsed<Options>(o =>
                   {
                       l.Info($"Configuring job to run: {o.JobName}");
                       config.JobID = o.JobName;
                       ExecuteJobName(config);
                   });
        }

        private static void ExecuteJobName(JobConfiguration conf)
        {
            IJob job = null;
            bool jobfound = false;

            l.Debug("Loading jobs from .cs file..");
            List<JobFile> externalJobsFiles = _GetExternalJobs(conf.JobsDirectoryPath.FullName);
            l.Debug("Looking for cs file to compile..");
            foreach (JobFile jobFile in externalJobsFiles)
            {
                job = null;

                if (conf.JobID == jobFile.JobID)
                {
                    conf.JobFileReference = jobFile;

                    l.Debug("Compiling {0}..", jobFile.JobID);
                    job = _CompileJob(conf);
                }

                if (job != null && job.JobID == conf.JobID)
                {
                    l.Debug("In file {0} is present JobID '{1}'", jobFile.JobFileInfo.FullName, conf.JobID);
                    jobfound = true;
                    break;
                }

            }

            if (jobfound)
            {
                l.Debug("Start loading other assemblies..");
                foreach (string asse in conf.JobFileReference.OtherAssemblies)
                {
                    if (!string.IsNullOrEmpty(asse))
                    {
                        string extarnalAssemblyFullName = Path.Combine(conf.ExternalDLLDirectoryPath.FullName, asse);
                        Assembly assembly = Assembly.LoadFrom(extarnalAssemblyFullName);
                        AppDomain.CurrentDomain.Load(assembly.GetName());
                    }
                }
                l.Debug("End loading other assemblies");
            }
            else
            {
                throw new Exception("Job not found");
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
        }

        private static List<JobFile> _GetExternalJobs(string jobPath)
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

        private static IJob _CompileJob(JobConfiguration conf)
        {
            IJob oJob = null;
            string codeSourceString = conf.JobFileReference.JobSourceCode;
            l.Trace("source={0}", codeSourceString);
            string basePath = Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location);

            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeSourceString, options);

            var references = new List<MetadataReference>
            {
               MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
                ,MetadataReference.CreateFromFile(Path.Combine(basePath, "netstandard.dll"))
                //,MetadataReference.CreateFromFile(Path.Combine(basePath, "System.Core.dll"))
                //,MetadataReference.CreateFromFile(Path.Combine(basePath, "System.Runtime.dll"))
                //,MetadataReference.CreateFromFile(Path.Combine(basePath, "System.Runtime.Extensions.dll"))
                //,MetadataReference.CreateFromFile(typeof(jobmodeldj.Model.IJob).Assembly.Location)
            };

            StringCollection refAssemblies = conf.JobFileReference.OtherAssemblies;
            foreach (string assem in refAssemblies)
            {
                if (!string.IsNullOrEmpty(assem))
                {
                    l.Debug("adding {0}..", Path.Combine(conf.AppDirectoryPath.FullName, assem));
                    references.Add(MetadataReference.CreateFromFile(Path.Combine(conf.ExternalDLLDirectoryPath.FullName, assem)));
                }
            }
            Assembly.GetEntryAssembly()?.GetReferencedAssemblies().ToList()
                .ForEach(a => references.Add(MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

            var csCompilation = CSharpCompilation.Create(conf.JobFileReference.JobID, new[] { parsedSyntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var peStream = new MemoryStream())
            {
                //var result = csCompilation.Emit(peStream); //IN MEMORY OPTION//
                var result = csCompilation.Emit(Path.Combine(conf.JobsTempDLLDirectoryPath.FullName, conf.JobAssembyName));

                if (result.Success)
                {
                    l.Debug("Compiled {0} successfully",conf.JobID);
                    //AssemblyLoadContext assemblyContext = new AssemblyLoadContext(Path.GetRandomFileName(), true); //IN MEMORY OPTION//
                    AssemblyLoadContext assemblyContext = new AssemblyLoadContext(conf.JobFileReference.JobID, true);
                    //Assembly assembly = assemblyContext.LoadFromStream(peStream); //IN MEMORY OPTION//
                    Assembly assembly = assemblyContext.LoadFromAssemblyPath(Path.Combine(conf.JobsTempDLLDirectoryPath.FullName, conf.JobAssembyName));

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

         private static CSharpCompilation CreateCompilation(SyntaxTree tree, string name) =>
                CSharpCompilation
                    .Create(name, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .AddReferences(MetadataReference.CreateFromFile(typeof(string).Assembly.Location))
                    .AddSyntaxTrees(tree);

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
    }
}
