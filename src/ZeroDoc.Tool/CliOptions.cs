using System;

namespace ZeroDoc.Tool;

/// <summary>Parsed command-line options for the ZeroDoc tool.</summary>
internal sealed class CliOptions
{
    public string? AssemblyPath { get; private set; }
    public string? OutputPath { get; private set; }
    public string? XmlPath { get; private set; }
    public string? Title { get; private set; }
    public string? ReadmePath { get; private set; }
    public bool PublicOnly { get; private set; }
    public bool ShowHelp { get; private set; }

    /// <summary>Parses arguments into a <see cref="CliOptions"/> instance.</summary>
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        if (args.Length == 0) { options.ShowHelp = true; return options; }

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;

                case "-o":
                case "--output":
                    options.OutputPath = RequireValue(args, ref i, arg);
                    break;

                case "-x":
                case "--xml":
                    options.XmlPath = RequireValue(args, ref i, arg);
                    break;

                case "-t":
                case "--title":
                    options.Title = RequireValue(args, ref i, arg);
                    break;

                case "-r":
                case "--readme":
                    options.ReadmePath = RequireValue(args, ref i, arg);
                    break;

                case "--public-only":
                    options.PublicOnly = true;
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException($"unknown option: {arg}");
                    if (options.AssemblyPath != null)
                        throw new ArgumentException($"unexpected extra argument: {arg}");
                    options.AssemblyPath = arg;
                    break;
            }
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int i, string optionName)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"option {optionName} requires a value.");
        return args[++i];
    }
}
