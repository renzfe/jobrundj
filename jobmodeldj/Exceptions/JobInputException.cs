namespace jobmodeldj.Exceptions
{
    /// <summary>
    /// An input requested by the job (Prompt) is missing or not valid.
    /// </summary>
    [Serializable]
    public class JobInputException : Exception
    {
        public string Key { get; } = string.Empty;

        public JobInputException() { }

        public JobInputException(string key, string message) : base($"Input '{key}': {message}")
        {
            Key = key;
        }
    }
}
