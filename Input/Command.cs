using System;
using System.Collections.Generic;

namespace ProjectVagabond
{
    public class Command
    {
        /// <summary>
        /// The name of the command (ex: "rest" or "walk").
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The action to execute when the command is run.
        /// </summary>
        public Action<string[]> Action { get; }

        /// <summary>
        /// A function that returns a list of suggestions for the *next* argument,
        /// based on the arguments already provided.
        /// </summary>
        public Func<string[], List<string>> SuggestArguments { get; }

        /// <summary>
        /// The help text for the command, used by the 'help' command.
        /// </summary>
        public string HelpText { get; }

        public Command(string name, Action<string[]> action, string helpText, Func<string[], List<string>> suggestArguments = null)
        {
            Name = name;
            Action = action;
            HelpText = helpText;
            // Provide a default empty suggestion function if none is given.
            SuggestArguments = suggestArguments ?? ((args) => new List<string>());
        }
    }
}