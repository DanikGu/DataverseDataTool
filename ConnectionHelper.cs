using System.Text.Json;
using GitCredentialManager;
using Spectre.Console;

public class ConnectionHelper
{
    private static readonly string CredsNamespace = "DataverseDataTool";
    public record ConnectionDetail(string ServiceUrl, string Name, string Id, string Secret);
    public static void SaveConnection(ConnectionDetail connectionDetail)
    {
        var serviceName = FormatServiceUrl(connectionDetail.ServiceUrl);
        ICredentialStore store = CredentialManager.Create(CredsNamespace);
        var serilizedDetail = JsonSerializer.Serialize(connectionDetail);
        store.AddOrUpdate(
            serviceName,
            connectionDetail.Name,
            serilizedDetail
        );
    }

    public static bool IsAnyAccountForService(string serviceUrl)
    {
        ICredentialStore store = CredentialManager.Create(CredsNamespace);
        var serviceName = FormatServiceUrl(serviceUrl);
        var accountNames = store.GetAccounts(serviceName);
        return accountNames.Any();
    }
    public static ConnectionDetail? SelectConnection(string serviceUrl)
    {
        ICredentialStore store = CredentialManager.Create(CredsNamespace);
        var serviceName = FormatServiceUrl(serviceUrl);
        var accountNames = store.GetAccounts(serviceName);
        var selectedAccount = AnsiConsole.Prompt(
          new SelectionPrompt<string>()
              .Title("[green]Select existing connection?[/]")
              .PageSize(10)
              .MoreChoicesText("[grey](Move up and down to reveal more connections)[/]")
              .AddChoices(accountNames)
        );
        var credential = store.Get(serviceName, selectedAccount);
        if (credential is null || credential.Password is null)
        {
            return null;
        }
        var details = JsonSerializer.Deserialize<ConnectionDetail>(credential.Password);
        return details;
    }
    private static string FormatServiceUrl(string serviceUrl)
    {
        var uri = new Uri(serviceUrl);
        return uri.ToString();
    }
}
