// See https://aka.ms/new-console-template for more information
using Spectre.Console.Cli;
using TakeoutFixer;

await new CommandApp<FixupCommand>().RunAsync(args);