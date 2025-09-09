using Microsoft.Xrm.Sdk;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace DataverseDataTool;

public static class AnsiConsoleUtils
{

    public static void WriteListOfEntitis(IEnumerable<Entity> etns)
    {
        if (!etns.Any())
        {
            return;
        }
        var table = new Table();
        var allKeys = etns.SelectMany(e => e.Attributes.Keys).Distinct().ToList();

        var idKey = allKeys.FirstOrDefault(k => k.Equals(etns.First().LogicalName + "id", StringComparison.OrdinalIgnoreCase));

        var dataAttrs = allKeys;
        if (idKey != null)
        {
            dataAttrs.Remove(idKey);
        }

        table.AddColumns("[blue]Id[/]");
        table.AddColumns(dataAttrs.ToArray());

        foreach (var etn in etns)
        {
            var row = new List<IRenderable>();

            row.Add(new Markup($"[blue]{Markup.Escape(etn.Id.ToString())}[/]"));

            foreach (var attr in dataAttrs)
            {
                IRenderable cell;
                if (etn.FormattedValues.TryGetValue(attr, out var formattedValue))
                {
                    var rawValue = etn.Contains(attr) ? FormatValue(etn[attr]) : string.Empty;

                    var escapedFormatted = Markup.Escape(formattedValue ?? string.Empty);
                    var escapedRaw = Markup.Escape(rawValue ?? string.Empty);

                    cell = new Markup($"[green]{escapedFormatted}[/]\n[white]{escapedRaw}[/]");
                }
                else if (etn.Contains(attr))
                {
                    var value = FormatValue(etn[attr]) ?? string.Empty;
                    cell = new Markup(Markup.Escape(value));
                }
                else
                {
                    cell = new Markup(string.Empty);
                }
                row.Add(cell);
            }
            table.AddRow(row);
        }
        table.Border = TableBorder.Square;
        table.ShowRowSeparators();
        AnsiConsole.Write(table);
    }

    public static T? RunWithStatus<T>(Func<T> action, string title)
    {
        T? result = default;
        AnsiConsole.Status()
            .Start(title, ctx =>
            {
                ctx.Spinner(Spinner.Known.Star);
                ctx.SpinnerStyle(Style.Parse("green"));
                result = action();
            });
        return result;
    }

    static string FormatValue(object value)
    {
        if (value is null)
        {
            return "";
        }
        if (value is AliasedValue alValue)
        {
            return FormatValue(alValue.Value);
        }
        if (value is EntityReference erValue)
        {
            return $"{erValue.Id} - {erValue.LogicalName} ({erValue?.Name})";
        }
        if (value is Money mnValue)
        {
            return mnValue.Value.ToString() ?? "";
        }
        if (value is OptionSetValue opValue)
        {
            return opValue.Value.ToString();
        }
        return value.ToString()!;
    }
}
