using jobmodeldj.Model;
using jobmodeldj.Utils;
using Spectre.Console;

namespace jobrundj.Console
{
    /// <summary>
    /// Interactive <see cref="IJobPrompt"/> (option -i / --interactive): values that are not provided
    /// on the command line or in the json file are asked in the console with the Spectre.Console prompts,
    /// then the job goes on and the log continues as usual.
    /// The job runs on the main thread (no dashboard), so the prompts are plain synchronous calls.
    /// </summary>
    internal sealed class ConsoleJobPrompt : JobPrompt
    {
        private static readonly Style Highlight = new Style(Color.DodgerBlue1);

        public ConsoleJobPrompt(JobConfiguration conf) : base(conf) { }

        /// <summary>True when the console can ask questions (input and output not redirected).</summary>
        public static bool IsSupported()
        {
            if (System.Console.IsInputRedirected || System.Console.IsOutputRedirected) return false;
            return AnsiConsole.Profile.Capabilities.Interactive;
        }

        public override bool IsInteractive => true;

        protected override T AskValue<T>(string question, bool hasDefault, T defaultValue, bool secret)
        {
            var prompt = new TextPrompt<T>(Question(question));
            if (hasDefault) prompt.DefaultValue(defaultValue);
            if (secret) prompt.Secret();
            return AnsiConsole.Prompt(prompt);
        }

        protected override bool AskConfirm(string question, bool defaultValue)
        {
            return AnsiConsole.Prompt(new ConfirmationPrompt(Question(question)) { DefaultValue = defaultValue });
        }

        protected override string AskChoice(string title, IReadOnlyList<string> choices, string? defaultValue)
        {
            var prompt = new SelectionPrompt<string>()
                .Title(Question(title))
                .HighlightStyle(Highlight)
                .PageSize(15)
                .MoreChoicesText("[grey](move up and down to see more choices)[/]")
                .UseConverter(Markup.Escape)
                .AddChoices(choices);
            if (defaultValue != null) prompt.DefaultValue(defaultValue);

            string choice = AnsiConsole.Prompt(prompt);
            // the selection prompt clears itself: echo the answer like the other prompts
            AnsiConsole.MarkupLine($"{Question(title)} [{Highlight.Foreground.ToMarkup()}]{Markup.Escape(choice)}[/]");
            return choice;
        }

        protected override IList<string> AskChoices(string title, IReadOnlyList<string> choices, IReadOnlyList<string> defaultValues)
        {
            var prompt = new MultiSelectionPrompt<string>()
                .Title(Question(title))
                .HighlightStyle(Highlight)
                .PageSize(15)
                .MoreChoicesText("[grey](move up and down to see more choices)[/]")
                .InstructionsText("[grey](press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                .UseConverter(Markup.Escape)
                .Required()
                .AddChoices(choices);
            foreach (var d in defaultValues) prompt.Select(d);

            var selected = AnsiConsole.Prompt(prompt);
            AnsiConsole.MarkupLine($"{Question(title)} [{Highlight.Foreground.ToMarkup()}]{Markup.Escape(string.Join(", ", selected))}[/]");
            return selected;
        }

        private static string Question(string text) => $"[bold]{Markup.Escape(text)}[/]";
    }
}
