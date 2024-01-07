namespace jobmodeldj.Model
{
    /// <summary>
    /// Interface for jobs
    /// </summary>
    public interface IJob
    {
        /// <summary>
        /// job id
        /// </summary>
        string JobID { get; }

        /// <summary>
        /// job runtime version
        /// </summary>
        int JobRuntimeVersion { get; }

        /// <summary>
        /// specific job version
        /// </summary>
        int Version { get; }

        /// <summary>
        /// job execute method
        /// </summary>
        /// <param name="conf">Configuration object</param>
        void MainExecute(JobConfiguration conf);
    }
}
