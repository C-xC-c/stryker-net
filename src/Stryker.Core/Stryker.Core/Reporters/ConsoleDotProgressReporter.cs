using Spectre.Console;
using Stryker.Core.Mutants;
using Stryker.Core.ProjectComponents;
using System.Collections.Generic;

namespace Stryker.Core.Reporters
{
    /// <summary>
    /// The default reporter, prints a simple progress by printing dots
    /// </summary>
    public class ConsoleDotProgressReporter : IReporter
    {
        private readonly IAnsiConsole _console;

        public ConsoleDotProgressReporter(IAnsiConsole console = null)
        {
            _console = console ?? AnsiConsole.Console;
        }

        public void OnMutantsCreated(IReadOnlyProjectComponent reportComponent) { }

        public void OnStartMutantTestRun(IEnumerable<IReadOnlyMutant> mutantsToBeTested) { }

        public void OnMutantTested(IReadOnlyMutant result)
        {
            switch (result.ResultStatus)
            {
                case MutantStatus.Killed:
                    _console.Write(".");
                    break;
                case MutantStatus.Survived:
                    _console.Markup("[Red]S[/]");
                    break;
                case MutantStatus.Timeout:
                    _console.Write("T");
                    break;
            };
        }

        public void OnAllMutantsTested(IReadOnlyProjectComponent reportComponent)
        {
            _console.WriteLine();
        }
    }
}
