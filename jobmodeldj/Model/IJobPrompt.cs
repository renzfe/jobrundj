namespace jobmodeldj.Model
{
    /// <summary>
    /// Input requested by a job (available in the job as <c>Prompt</c>, or <c>conf.Prompt</c>).
    /// Every request has a key. The value is taken, in this order, from:
    /// <list type="number">
    /// <item>the command line: <c>--param key=value</c> (repeatable)</item>
    /// <item>the job configuration file: section <c>"JobParams"</c> of [JobName].json</item>
    /// <item>the user, with a prompt in the console, when jobrundj runs with <c>-i</c> / <c>--interactive</c></item>
    /// <item>the default value of the request</item>
    /// </list>
    /// When no value can be found, a <see cref="jobmodeldj.Exceptions.JobInputException"/> is thrown.
    /// Values from the command line and the json file use the invariant culture (e.g. 3.5, 2026-11-16).
    /// </summary>
    public interface IJobPrompt
    {
        /// <summary>True when missing values are asked to the user (-i / --interactive on an interactive console).</summary>
        bool IsInteractive { get; }

        /// <summary>Asks a text. Without a default value the text is required.</summary>
        string Ask(string key, string question, string? defaultValue = null);

        /// <summary>Asks a required value of type T (int, decimal, DateTime, an enum, ...).</summary>
        T Ask<T>(string key, string question);

        /// <summary>Asks a value of type T (int, decimal, DateTime, an enum, ...) with a default value.</summary>
        T Ask<T>(string key, string question, T defaultValue);

        /// <summary>Asks a required secret text (password, token): it is not echoed and never written in the log.</summary>
        string AskSecret(string key, string question);

        /// <summary>Asks a yes/no confirmation. Provided values: true/false, yes/no, y/n, 1/0.</summary>
        bool Confirm(string key, string question, bool defaultValue = false);

        /// <summary>Asks to choose one of the choices. A provided value must match a choice (ignoring case).</summary>
        string Choose(string key, string title, IEnumerable<string> choices, string? defaultValue = null);

        /// <summary>
        /// Asks to choose one or more of the choices. Provided values: comma separated list
        /// (command line, json string) or json array.
        /// </summary>
        IList<string> ChooseMany(string key, string title, IEnumerable<string> choices, IEnumerable<string>? defaultValues = null);
    }
}
