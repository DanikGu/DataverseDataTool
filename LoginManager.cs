
using DataverseDataTool;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Query;
using Spectre.Console;
using static ConnectionHelper;
using ValidationResult = Spectre.Console.ValidationResult;

public class LoginManager
{
    public static ServiceClient GoToLoginLoop(string url, ILogger loggerInstance)
    {
        ServiceClient? client = null;
        while (client is null)
        {
            var isAnySaved = ConnectionHelper.IsAnyAccountForService(url);
            var options = isAnySaved
              ? new[] {
                  "Browser Interactive",
                  "Client Secret",
                  "Select Saved Credentials"
                }
              : new[] {
                  "Browser Interactive",
                  "Client Secret",
                };
            var loginType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Choose Login Type[/]")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more login options)[/]")
                    .AddChoices(options));
            if (loginType == "Browser Interactive")
            {
                client = LoginWithBrowser(url, loggerInstance);
            }
            else if (loginType == "Client Secret")
            {
                client = LoginWithSecret(url, loggerInstance);
            }
            else if (loginType == "Select Saved Credentials")
            {
                client = LoginWithExistingConnection(url, loggerInstance);
            }
        }
        return client;
    }
    private static ServiceClient? LoginWithExistingConnection(string url, ILogger loggerInstance)
    {
        var connectionDetail = ConnectionHelper.SelectConnection(url);
        if (connectionDetail == null)
        {
            AnsiConsole.WriteLine("[red]No connection selected or found.[/]");
            return null;
        }
        var clientId = connectionDetail.Id;
        var clientSecret = connectionDetail.Secret;

        var client = AnsiConsoleUtils.RunWithStatus(
          () => new ServiceClient(new Uri(url), clientId, clientSecret, useUniqueInstance: true, loggerInstance),
          "Logging in"
        )!;
        return TestConnection(client) ? client : null;
    }
    private static ServiceClient? LoginWithSecret(string url, ILogger loggerInstance)
    {
        var clientId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Client Id:[/]")
                .PromptStyle("green")
                .ValidationErrorMessage("[red]Client id is empty[/]")
                .Validate(str =>
                  !string.IsNullOrWhiteSpace(str)
                  ? ValidationResult.Success()
                  : ValidationResult.Error()));
        var clientSecret = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Client Secret:[/]")
                .PromptStyle("green")
                .Secret()
                .ValidationErrorMessage("[red]Secret is empty[/]")
                .Validate(str =>
                  !string.IsNullOrWhiteSpace(str)
                  ? ValidationResult.Success()
                  : ValidationResult.Error()));
        var client = AnsiConsoleUtils.RunWithStatus(
          () => new ServiceClient(new Uri(url), clientId, clientSecret, useUniqueInstance: true, loggerInstance),
          "Logging in"
        )!;
        if (TestConnection(client))
        {
            if (AnsiConsole.Confirm("Do you want to save this account?"))
            {
                var accountName = AnsiConsole.Prompt(
                    new TextPrompt<string>("[green]Enter a name for this account:[/]")
                        .PromptStyle("green")
                        .ValidationErrorMessage("[red]Account name cannot be empty[/]")
                        .Validate(str =>
                          !string.IsNullOrWhiteSpace(str)
                          ? ValidationResult.Success()
                          : ValidationResult.Error()));

                var connectionDetail = new ConnectionDetail(url, accountName, clientId, clientSecret);
                ConnectionHelper.SaveConnection(connectionDetail);
            }
            return client;
        }
        return null;
    }
    private static ServiceClient? LoginWithBrowser(string url, ILogger loggerInstance)
    {
        var connStr = $@"
            AuthType = OAuth;
            Url = {url};
            AppId = 51f81489-12ee-4a9e-aaae-a2591f45987d;
            RedirectUri = http://localhost;
            LoginPrompt=Auto;
            RequireNewInstance = True";
        var client = AnsiConsoleUtils.RunWithStatus(
          () => new ServiceClient(connStr, loggerInstance),
          "Logging in"
        )!;
        var str = client.CurrentAccessToken;
        if (TestConnection(client))
        {
            return client;
        }
        return null;
    }
    private static bool TestConnection(ServiceClient client)
    {
        return AnsiConsoleUtils.RunWithStatus(() =>
        {
            try
            {
                var whoAmIRequest = new WhoAmIRequest();
                var whoAmIResponse = (WhoAmIResponse)client.Execute(whoAmIRequest);
                var userName = client
                  .Retrieve("systemuser", whoAmIResponse.UserId, new ColumnSet("fullname"))
                  .GetAttributeValue<string>("fullname");
                AnsiConsole.MarkupLine($"[green]Logged in as {userName}({whoAmIResponse.UserId})[/]");
                return true;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Cannot connect to datverse exception: {ex.Message}[/]");
                return false;
            }
        }, "Testing connection ...");
    }
}
