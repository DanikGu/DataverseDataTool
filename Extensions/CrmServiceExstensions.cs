using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDataTool.Extensions;

public record BatchOperationProgress(int TotalCount, int SuccessCount, int FailCount);

public static class CrmServiceExtensions
{

    public static IEnumerable<Entity> GetAllByQuery(this ServiceClient service, QueryExpression query)
    {
        var result = new List<Entity>();
        EntityCollection resultCollection;

        if (query == null)
            throw new ArgumentNullException(nameof(query));

        if (query.PageInfo == null)
        {
            query.PageInfo = new PagingInfo
            {
                Count = 5000,
                PageNumber = 1,
                PagingCookie = string.Empty,
            };
        }
        do
        {
            resultCollection = service.RetrieveMultiple(query);

            if (resultCollection.Entities.Count > 0)
            {
                result.AddRange(resultCollection.Entities.ToList());
            }
            if (resultCollection.MoreRecords)
            {
                query.PageInfo.PageNumber += 1;
                query.PageInfo.PagingCookie = resultCollection.PagingCookie;
            }
        } while (resultCollection.MoreRecords);

        return result;
    }
    public static void ExecuteMultipleBatch(this ServiceClient service,
        List<OrganizationRequest> requests,
        int maxServiceCount,
        int batchSize,
        Action<BatchOperationProgress> reportProgress,
        ILogger logger)
    {
        ExecuteBatchOperation(
            service,
            requests,
            maxServiceCount,
            batchSize,
            reportProgress,
            logger);
    }

    public static void CreateMultipleBatch(
        List<Entity> entitiesToCreate,
        ServiceClient service,
        int maxServiceCount,
        int batchSize,
        Action<BatchOperationProgress> reportProgress,
        ILogger logger
    )
    {
        var requests = entitiesToCreate
            .Select(entity => new CreateRequest { Target = entity })
            .Select(req => (OrganizationRequest)req)
            .ToList();
        ExecuteBatchOperation(
            service,
            requests,
            maxServiceCount,
            batchSize,
            reportProgress,
            logger);
    }

    public static void UpdateMultipleBatch(
        this ServiceClient service,
        List<Entity> entitiesToUpdate,
        int maxServiceCount,
        int batchSize,
        Action<BatchOperationProgress> reportProgress,
        ILogger logger
    )
    {
        var requests = entitiesToUpdate
            .Select(entity => new UpdateRequest { Target = entity })
            .Select(req => (OrganizationRequest)req)
            .ToList();
        ExecuteBatchOperation(
            service,
            requests,
            maxServiceCount,
            batchSize,
            reportProgress,
            logger);
    }

    public static void DeleteMultipleBatch(
        this ServiceClient service,
        List<Entity> entitiesToDelete,
        int maxServiceCount,
        int batchSize,
        Action<BatchOperationProgress> reportProgress,
        ILogger logger)
    {
        var requests = entitiesToDelete
            .Select(entity => new DeleteRequest { Target = entity.ToEntityReference() })
            .Select(req => (OrganizationRequest)req)
            .ToList();
        ExecuteBatchOperation(
            service,
            requests,
            maxServiceCount,
            batchSize,
            reportProgress,
            logger
        );
    }

    private static void ExecuteBatchOperation(
        this ServiceClient service,
        List<OrganizationRequest> items,
        int maxServiceCount,
        int batchSize,
        Action<BatchOperationProgress> reportProgress,
        ILogger logger)
    {
        int successCount = 0;
        int failCount = 0;
        int totalCount = items.Count;

        if (totalCount == 0)
        {
            return;
        }

        IEnumerable<OrganizationRequest[]> itemBatches = items.Chunk(batchSize);
        var threadLocalService = new ThreadLocal<ServiceClient>(() => service.Clone());

        Parallel.ForEach(itemBatches,
            new ParallelOptions { MaxDegreeOfParallelism = maxServiceCount },
            (itemBatch) =>
            {
                var localService = threadLocalService.Value;
                var request = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = true,
                        ReturnResponses = true
                    },
                    Requests = new OrganizationRequestCollection()
                };

                foreach (var item in itemBatch)
                {
                    request.Requests.Add(item);
                }

                var response = ExecuteWithRetry(localService!, request);
                if (response is null)
                {
                    Interlocked.Add(ref failCount, itemBatch.Length);
                    reportProgress(new BatchOperationProgress(totalCount, successCount, failCount));
                    return;
                }

                foreach (var responseItem in response.Responses)
                {
                    if (responseItem.Fault is null)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref failCount);
                        logger.LogError(responseItem.Fault.Message);
                    }
                    reportProgress(new BatchOperationProgress(totalCount, successCount, failCount));
                }
            });
    }

    private static ExecuteMultipleResponse? ExecuteWithRetry(
           ServiceClient service,
           ExecuteMultipleRequest request)
    {
        var retryCount = 0;
        while (retryCount++ < 5)
        {
            try
            {
                return (ExecuteMultipleResponse)service.Execute(request);
            }
            catch
            {
                continue;
            }
        }
        return null;
    }
}
