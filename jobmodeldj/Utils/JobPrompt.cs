using System.ComponentModel;
using System.Globalization;
using jobmodeldj.Exceptions;
using jobmodeldj.Model;
using NLog;

namespace jobmodeldj.Utils
{
    /// <summary>
    /// Non-interactive <see cref="IJobPrompt"/>: values come from the command line (--param key=value),
    /// from the "JobParams" section of [JobName].json or from the default value.
    /// jobrundj extends it with a console implementation for -i / --interactive, overriding the Ask* hooks.
    /// </summary>
    public class JobPrompt : IJobPrompt
    {
        public const string PARAM_OPTION = "--param";
        public const string JSON_SECTION = "JobParams";
        private const string SECRET_MASK = "******";

        private readonly JobConfiguration conf;
        private readonly Dictionary<string, string> commandLineValues;

        /// <summary>The logger of the job: the received values are written in the job log.</summary>
        protected Logger Log { get; }

        public JobPrompt(JobConfiguration conf)
        {
            this.conf = conf;
            commandLineValues = ParseCommandLine(conf.JobArgs);
            Log = LogManager.GetLogger($"jobmodeldj.jobs.{conf.JobID}");
        }

        public virtual bool IsInteractive => false;

        #region IJobPrompt

        public string Ask(string key, string question, string? defaultValue = null)
        {
            return Resolve(key, question, defaultValue != null, defaultValue ?? string.Empty, secret: false,
                parse: raw => raw,
                ask: () => AskValue(question, defaultValue != null, defaultValue ?? string.Empty, secret: false),
                format: v => v);
        }

        public T Ask<T>(string key, string question)
        {
            return Resolve(key, question, false, default(T)!, secret: false,
                parse: ConvertValue<T>,
                ask: () => AskValue(question, false, default(T)!, secret: false),
                format: FormatValue);
        }

        public T Ask<T>(string key, string question, T defaultValue)
        {
            return Resolve(key, question, true, defaultValue, secret: false,
                parse: ConvertValue<T>,
                ask: () => AskValue(question, true, defaultValue, secret: false),
                format: FormatValue);
        }

        public string AskSecret(string key, string question)
        {
            return Resolve(key, question, false, string.Empty, secret: true,
                parse: raw => raw,
                ask: () => AskValue(question, false, string.Empty, secret: true),
                format: v => v);
        }

        public bool Confirm(string key, string question, bool defaultValue = false)
        {
            return Resolve(key, question, true, defaultValue, secret: false,
                parse: ConvertValue<bool>,
                ask: () => AskConfirm(question, defaultValue),
                format: v => v ? "yes" : "no");
        }

        public string Choose(string key, string title, IEnumerable<string> choices, string? defaultValue = null)
        {
            var list = CheckChoices(key, choices);
            string? def = null;
            if (defaultValue != null)
            {
                def = MatchChoice(key, list, defaultValue, "default value");
            }

            return Resolve(key, title, def != null, def ?? string.Empty, secret: false,
                parse: raw => MatchChoice(key, list, raw, "value"),
                ask: () => AskChoice(title, list, def),
                format: v => v);
        }

        public IList<string> ChooseMany(string key, string title, IEnumerable<string> choices, IEnumerable<string>? defaultValues = null)
        {
            var list = CheckChoices(key, choices);
            List<string>? defs = null;
            if (defaultValues != null)
            {
                defs = defaultValues.Select(d => MatchChoice(key, list, d, "default value")).Distinct().ToList();
            }

            return Resolve<IList<string>>(key, title, defs != null && defs.Count > 0, defs ?? new List<string>(), secret: false,
                parse: raw =>
                {
                    var selected = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                      .Select(v => MatchChoice(key, list, v, "value"))
                                      .Distinct()
                                      .ToList();
                    if (selected.Count == 0) throw new FormatException("at least one choice is required");
                    return selected;
                },
                ask: () => AskChoices(title, list, defs ?? new List<string>()),
                format: v => string.Join(", ", v));
        }

        #endregion

        #region interactive hooks (overridden by the console implementation)

        /// <summary>Asks a value to the user. Called only when <see cref="IsInteractive"/> is true.</summary>
        protected virtual T AskValue<T>(string question, bool hasDefault, T defaultValue, bool secret) => throw NotInteractive();

        /// <summary>Asks a yes/no confirmation to the user. Called only when <see cref="IsInteractive"/> is true.</summary>
        protected virtual bool AskConfirm(string question, bool defaultValue) => throw NotInteractive();

        /// <summary>Asks to choose one item. Called only when <see cref="IsInteractive"/> is true.</summary>
        protected virtual string AskChoice(string title, IReadOnlyList<string> choices, string? defaultValue) => throw NotInteractive();

        /// <summary>Asks to choose one or more items (at least one). Called only when <see cref="IsInteractive"/> is true.</summary>
        protected virtual IList<string> AskChoices(string title, IReadOnlyList<string> choices, IReadOnlyList<string> defaultValues) => throw NotInteractive();

        private static NotSupportedException NotInteractive() => new NotSupportedException("The prompt is not interactive");

        #endregion

        #region resolution

        private T Resolve<T>(string key, string question, bool hasDefault, T defaultValue, bool secret,
                             Func<string, T> parse, Func<T> ask, Func<T, string> format)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("The key of an input is required", nameof(key));

            if (TryGetProvidedValue(key, out string raw, out string source))
            {
                T value;
                try
                {
                    value = parse(raw);
                }
                catch (Exception ex) when (ex is not JobInputException)
                {
                    throw new JobInputException(key, $"invalid value '{(secret ? SECRET_MASK : raw)}' from {source} ({ex.Message})");
                }
                LogValue(key, value, format, secret, source);
                return value;
            }

            if (IsInteractive)
            {
                LogManager.Flush();
                T value = ask();
                LogValue(key, value, format, secret, "console");
                return value;
            }

            if (hasDefault)
            {
                LogValue(key, defaultValue, format, secret, "default");
                return defaultValue;
            }

            throw new JobInputException(key,
                $"no value for \"{question}\". Pass {PARAM_OPTION} {key}=<value>, add \"{key}\" to the \"{JSON_SECTION}\" section " +
                $"of {conf.JobConfigurationJsonName} or run with -i (--interactive)");
        }

        private bool TryGetProvidedValue(string key, out string value, out string source)
        {
            if (commandLineValues.TryGetValue(key, out var cmd))
            {
                value = cmd;
                source = "command line";
                return true;
            }

            var section = conf.Json?.GetSection($"{JSON_SECTION}:{key}");
            if (section != null)
            {
                if (section.Value != null)
                {
                    value = section.Value;
                    source = conf.JobConfigurationJsonName;
                    return true;
                }
                // json array (for ChooseMany): ["a", "b"]
                var items = section.GetChildren().Select(c => c.Value).Where(v => v != null).ToList();
                if (items.Count > 0)
                {
                    value = string.Join(",", items);
                    source = conf.JobConfigurationJsonName;
                    return true;
                }
            }

            value = string.Empty;
            source = string.Empty;
            return false;
        }

        private void LogValue<T>(string key, T value, Func<T, string> format, bool secret, string source)
        {
            Log.Info("Input {0} = {1} ({2})", key, secret ? SECRET_MASK : format(value), source);
        }

        /// <summary>
        /// --param key=value [key2=value2 ...] (the values up to the next option), --param=key=value;
        /// repeatable, the last value of a key wins.
        /// </summary>
        private static Dictionary<string, string> ParseCommandLine(string[] args)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == PARAM_OPTION)
                {
                    int count = 0;
                    while (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        AddPair(values, args[++i]);
                        count++;
                    }
                    if (count == 0) throw new ArgumentException($"{PARAM_OPTION}: missing key=value");
                }
                else if (args[i].StartsWith(PARAM_OPTION + "=", StringComparison.Ordinal))
                {
                    AddPair(values, args[i].Substring(PARAM_OPTION.Length + 1));
                }
            }
            return values;
        }

        private static void AddPair(Dictionary<string, string> values, string pair)
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0) throw new ArgumentException($"{PARAM_OPTION} {pair}: expected key=value");
            values[pair.Substring(0, eq).Trim()] = pair.Substring(eq + 1);
        }

        #endregion

        #region conversions

        private static readonly string[] TrueValues = { "true", "yes", "y", "1", "si", "sì", "s" };
        private static readonly string[] FalseValues = { "false", "no", "n", "0" };

        /// <summary>Converts a provided value (invariant culture) to T.</summary>
        protected static T ConvertValue<T>(string raw)
        {
            Type type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (type == typeof(string)) return (T)(object)raw;

            string text = raw.Trim();
            if (type == typeof(bool))
            {
                if (TrueValues.Contains(text, StringComparer.OrdinalIgnoreCase)) return (T)(object)true;
                if (FalseValues.Contains(text, StringComparer.OrdinalIgnoreCase)) return (T)(object)false;
                throw new FormatException("expected true/false, yes/no, y/n or 1/0");
            }

            var converter = TypeDescriptor.GetConverter(type);
            if (!converter.CanConvertFrom(typeof(string))) throw new NotSupportedException($"type {type.Name} is not supported");
            return (T)converter.ConvertFromInvariantString(text)!;
        }

        private static string FormatValue<T>(T value) => value switch
        {
            DateTime d => d.ToString(d.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        private static List<string> CheckChoices(string key, IEnumerable<string> choices)
        {
            var list = (choices ?? throw new ArgumentNullException(nameof(choices))).Distinct().ToList();
            if (list.Count == 0) throw new ArgumentException($"Input '{key}': the list of choices is empty", nameof(choices));
            return list;
        }

        private static string MatchChoice(string key, IReadOnlyList<string> choices, string value, string what)
        {
            string text = value.Trim();
            var match = choices.FirstOrDefault(c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new JobInputException(key, $"{what} '{text}' is not one of: {string.Join(", ", choices)}");
            }
            return match;
        }

        #endregion
    }
}
