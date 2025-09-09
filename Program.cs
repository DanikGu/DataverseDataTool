using DataverseDataTool;
using DataverseDataTool.Extensions;
using Meziantou.Extensions.Logging.InMemory;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Spectre.Console;


internal class Program
{
    private static InMemoryLoggerProvider LoggerProvider = null!;
    private static void Main(string[] args)
    {
        try
        {
            LoggerProvider = new InMemoryLoggerProvider();
            RunProgram();
        }
        catch (Exception ex)
        {
            AnsiConsole.Write(new Markup("[red]Unexpected error when execution tool[/]"));
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }
        finally
        {
            LoggerProvider.Dispose();
        }
    }
    static void RunProgram()
    {
        var entityLogicalName = "tip_job";
        var url = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Dataverse Url:[/]")
                .PromptStyle("green")
                .ValidationErrorMessage("[red]Thata not a valid url[/]")
                .Validate(url =>
                  !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, new(), out _)
                  ? ValidationResult.Success()
                  : ValidationResult.Error()));
        var loggerInstance = GetLogerInstance("OrgService");
        var service = LoginManager.GoToLoginLoop(url, loggerInstance);

        var entities = AnsiConsoleUtils.RunWithStatus(() =>
            service.GetAllByQuery(new QueryExpression(entityLogicalName)
            {
                ColumnSet = new ColumnSet($"{entityLogicalName}id")
            }
        ), "Retriving Data From Dataverse");
        if (entities is null || !entities.Any())
        {
            AnsiConsole.Write(new Markup("[yellow]No entities meet critieria, exit[/]"));
            return;
        }
        var entitiesToUpdate = entities.Select(x => new Entity(entityLogicalName, x.Id)).Take(200).ToList();



        var startUpdate = AnsiConsole.Confirm("Are you ready to start Update?", defaultValue: false);
        if (!startUpdate)
        {
            return;
        }
        //break some for logs to show
        {
            entitiesToUpdate.ElementAt(10).Id = Guid.NewGuid();
            entitiesToUpdate.ElementAt(12).Id = Guid.NewGuid();
            entitiesToUpdate.ElementAt(33).Id = Guid.NewGuid();
        }
        UpdateBatchWithWatch(service, entitiesToUpdate, loggerInstance);
    }
    static void SaveLogDump()
    {
        //TODO: save data from in memory logger;
    }
    static ILogger GetLogerInstance(string loggerName)
    {
        var logger = LoggerProvider.CreateLogger(loggerName);
        return logger;
    }
    static void UpdateBatchWithWatch(ServiceClient service, List<Entity> entitiesToUpdate, ILogger logger)
    {
        var getProgressPanel = (BatchOperationProgress batchProgress) =>
        {
            var (total, succsess, errors) = batchProgress;
            var panel = new Panel(
                Align.Center(
                  new Markup($"[bold]Updating:[/] Total: {total}, Success: {succsess}, Error: {errors}"),
                  VerticalAlignment.Middle
                )
            ).Expand();
            return panel;
        };
        var getLogsTable = () =>
        {
            var logsTable = new Table().Expand();
            logsTable.AddColumn("Timestamp");
            logsTable.AddColumn("Text");
            var logs = LoggerProvider
              .Logs
              .Errors
              .OrderByDescending(x => x.CreatedAt).Take(10);
            if (!logs.Any())
            {
                logsTable.AddRow(
                    new Markup($"[blue][/]"),
                    new Markup($"[green]Error log table is Empty[/]")
                );
            }
            foreach (var log in logs)
            {
                logsTable.AddRow(
                    new Markup($"[blue]{log.CreatedAt.ToString()}[/]"),
                    new Markup($"[red]{Markup.Escape(log.Message)}[/]")
                );
            }
            return logsTable;
        };
        var layout = new Layout("Root").SplitRows(
            new Layout("Top"),
            new Layout("Bottom")
        );
        layout["Top"].Size(5);
        var currProgress = new BatchOperationProgress(entitiesToUpdate.Count(), 0, 0);
        bool isCompleted = false;
        var liveTask = AnsiConsole.Live(layout)
            .StartAsync(async ctx =>
            {
                while (!isCompleted)
                {
                    await Task.Delay(100);
                    var progress = getProgressPanel(currProgress);
                    var logsTable = getLogsTable();
                    layout["Top"].Update(progress);
                    layout["Bottom"].Update(logsTable);
                    ctx.Refresh();
                }
            });
        var updateTask = Task.Run(() =>
        {
            service.UpdateMultipleBatch(
                entitiesToUpdate,
                10,
                10,
                (batchProgress) =>
                {
                    currProgress = batchProgress;
                },
                logger);
            isCompleted = true;
        });
        Task.WaitAll(liveTask, liveTask);
    }

}

