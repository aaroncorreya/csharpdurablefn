using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using csharpdurablefn.Properties;
using System.Threading;
using System.Linq;

namespace csharpdurablefn
{

    //Activity Function 1: Scheduler (Fetch CosmosDB items and return list of valid exportControls to be run)
    //Orchestrator function iterates through exportControls and runs parallel activity tasks
    //Activity Function 2: Consumer -- Make API call to get content,
    //Activity Function 3: read mapping files
    //Activity Function 4: use smart tracking logic
    //Activity Function 5: make PR with content 
    //Activity Function 6: Update next run time after PR completes 

    //TODO: Add cancellation token for all functions, make some values run past ct 
    //      Add http triggered function for on-demand 
    
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context, ILogger log)
        {
            TimeSpan timeout = TimeSpan.FromSeconds(120);
            DateTime deadline = context.CurrentUtcDateTime.Add(timeout);

            //outputs.Add(await context.CallActivityAsync<string>("Function1_Hello", "Tokyo"));
            var cts = new CancellationTokenSource();

            Task<List<ExportControl>> activityTask = context.CallActivityAsync<List<ExportControl>>("FetchCosmosDB", null);
            //activityTask2...
            Task timeoutTask = context.CreateTimer(deadline, cts.Token);

            Task winner = await Task.WhenAny(activityTask, timeoutTask);

            if (winner == activityTask)
            {
                log.LogInformation(activityTask.Result.Count().ToString());
                //success
                cts.Cancel();
            }
            else
            {
                //timeout
                throw new TimeoutException();
            }

            //create an activity function that fans out for each export control in list that we get from cosmosDb activity function
            var parallelTasks = new List<Task<string>>();
            // Get a list of N work items to process in parallel.
            for (int i = 0; i < activityTask.Result.Count(); i++)
            {
                Task<string> task = context.CallActivityAsync<string>("Consumer", activityTask.Result[i]);
                parallelTasks.Add(task);
            }
            await Task.WhenAll(parallelTasks);

            foreach (var control in parallelTasks)
            {
                //Print out connection names after aggregating results
                log.LogInformation(control.Result);
            }
        }

        [FunctionName("Consumer")]
        public static string FetchContentViaAPI([ActivityTrigger] ExportControl exportControl, ILogger log)
        {
            return exportControl.ConnectionName;
        }

        [FunctionName("FetchCosmosDB")]
        public static async Task<List<ExportControl>> FetchDataAsync([ActivityTrigger] string name, ILogger log)
        {
            Container container = await GetContainerAsync();
            log.LogInformation(container.Id);
            var exportControls = await GetAllExportControls(container, log);
            return exportControls;
        }

        //This should be the scheduler 
        [FunctionName("ScheduledStart")]
        public static async Task RunScheduled(
            [TimerTrigger("0 */2 * * * *")] TimerInfo timerInfo,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            if (timerInfo.IsPastDue)
            {
                log.LogInformation("Timer is running late!");
            }

            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            //get all export connections that need to be run in the next 5 minutes 
            //Container container = await GetContainerAsync();
            //List<ExportControl> exportControls = await GetAllExportControls(container, log);

            string instanceId = await starter.StartNewAsync("Function1", null);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }

        public static async Task<List<ExportControl>> GetAllExportControls(Container container, ILogger log)
        {
            List<ExportControl> exportControls = new List<ExportControl>(); ; 
            string sqlQueryText = "SELECT * FROM c";
            QueryDefinition definition = new QueryDefinition(sqlQueryText);
            var iterator = container.GetItemQueryIterator<ExportControl>(definition);
            while (iterator.HasMoreResults)
            {
                FeedResponse<ExportControl> result = await iterator.ReadNextAsync();
                foreach (var item in result)
                {
                    //log.LogInformation(item.ConnectionName);
                    exportControls.Add(item);
                }
            }
            return exportControls;
        }

        public static async Task<Container> GetContainerAsync()
        {
            CosmosClient client = new CosmosClient(
                accountEndpoint: Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")!,
                authKeyOrResourceToken: Environment.GetEnvironmentVariable("COSMOS_KEY")!
            );

            Database database = await client.CreateDatabaseIfNotExistsAsync(id: "BiDirectionalSyncDB");
            
            Container container = await database.CreateContainerIfNotExistsAsync(
                id: "ExportControls",
                partitionKeyPath: "/WorkspaceId",
                throughput: 400
            );

            return container;
        }
    }
}