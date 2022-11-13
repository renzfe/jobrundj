//<jobrundjReferencedAssemblies></jobrundjReferencedAssemblies>
using System;
using NLog;
using jobmodeldj.Model;
using CommandLine;
using System.IO;

namespace jobmodeldj.jobs
{
    class JobDelete : Job
    {
        private int FilesDeleted = 0;
        private int FilesParsed = 0;
		
        public override int JobRuntimeVersion { get { return 2; } }
        
        public override void Execute(JobConfiguration conf) 
        {
            try
            {
				string folderToDelete = string.Empty;	
				string ext = string.Empty;	
				bool simulate = true;
				bool recursive = false;
				int age = 100000;
				bool deleteEmptyFolders = false;
				
				Parser.Default.ParseArguments<OptionsJob>(conf.JobArgs)
                   .WithParsed<OptionsJob>(o =>
                   {
                       l.Info($"Folder to delete: {o.FolderFullPath}");
					   folderToDelete = o.FolderFullPath;
                       l.Info($"Simulate: {o.Simulate}");
					   simulate = o.Simulate;
                       l.Info($"FileExtension: {o.FileExtension}");
					   ext = o.FileExtension;
                       l.Info($"Recursive: {o.Recursive}");
					   recursive = o.Recursive;
                       l.Info($"FileAge: {o.FileAge}");
					   age = o.FileAge;
                       l.Info($"FileAge: {o.CleanEmptyFolder}");
					   deleteEmptyFolders = o.CleanEmptyFolder;
                   });
				
				age = age * -1;
				DirectoryInfo folder = new DirectoryInfo(folderToDelete);
				if (!folder.Exists) throw new Exception($"Folder {folderToDelete} does not exist");
				
				DateTime today = DateTime.Now.Date;
				DateTime cutOffDate = today.AddDays(age);
				l.Info("Cut-Off Date={0}", cutOffDate.ToString("yyyy-MM-dd"));
				
				DeleteFiles(folder, folder, ext, simulate, recursive, deleteEmptyFolders, cutOffDate);
				l.Info("File Parsed/Deleted: {0}/{1}{2}", FilesParsed, FilesDeleted, simulate?" (simulate)":"");
            }
            catch (Exception ex)
            {
                l.Error("Error {0} - {1}", JobID, ex.Message);
				throw ex;
            }
            finally
            {
                l.Info("end {0}", JobID);
            }
        }
		
		private void DeleteFiles(DirectoryInfo rootFolder, DirectoryInfo folder, string ext, bool simulate, bool recursive, bool deleteEmptyFolders, DateTime cutOffDate)
		{
			if (!folder.Exists) throw new Exception($"Folder {folder.FullName} does not exist");
			
			string filter = string.Format("*.{0}", ext);
			foreach(FileInfo file in folder.GetFiles(filter))
			{
				FilesParsed++;  
				bool toDelete = file.LastWriteTime.Date < cutOffDate;
				l.Debug("checking [{0}][{1}]<[{2}]={3}", file.FullName, file.LastWriteTime.Date.ToString("yyyy-MM-dd"), cutOffDate.ToString("yyyy-MM-dd"), toDelete);
				if (toDelete)
				{
					try
					{
						l.Info("Deleting [{0}]", file.FullName);
						FilesDeleted++;
						if (!simulate)
						{
							file.Delete();
							l.Info("DELETED [{0}]", file.FullName);
						}
					}
					catch (Exception ex)
					{
						l.Error("Error deleting [{0}]={1}", file.FullName, ex.Message);
					}
				}
			}

			if (recursive)
			{
				foreach(DirectoryInfo subFolder in folder.GetDirectories())
				{
					DeleteFiles(rootFolder, subFolder, ext, simulate, recursive, deleteEmptyFolders, cutOffDate);
				}
			}
			
			if(deleteEmptyFolders)
			{
				if (rootFolder.FullName == folder.FullName)
				{
					l.Debug("Preserving root folder {0}", folder.FullName);
				}
				else if (folder.GetFiles().Length == 0 && folder.GetDirectories().Length == 0)
				{
					l.Info("Deleting empty folder {0}", folder.FullName);

					try
					{
						if (!simulate)
						{
							folder.Delete(true);
							l.Info("DELETED [{0}]", folder.FullName);
						}					
					}
					catch (Exception ex)
					{
						l.Error("Error deleting [{0}]={1}", folder.FullName, ex.Message);
					}
				}
			}
		}
    }
	
    internal class OptionsJob : jobrundj.Console.Options
    {
        [Option('p', "path", Required = true, HelpText = "Folder path where delete files")]
        public string FolderFullPath { get; set; }
		
        [Option('a', "age", Required = true, HelpText = "Age in days")]
        public int FileAge { get; set; }

        [Option('e', "ext", Default = "*", HelpText = "File Extension")]
        public string FileExtension { get; set; }
		
		[Option('s', "simulate", Default = false, HelpText = "Simulate Deletion")]
		public bool Simulate { get; set; }
		
		[Option('r', "recursive", Default = false, HelpText = "Recursive")]
		public bool Recursive { get; set; }
		
		[Option('c', "clean", Default = false, HelpText = "Delete empty folder")]
		public bool CleanEmptyFolder { get; set; }
    }	
}
