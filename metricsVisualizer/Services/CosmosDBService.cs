using Microsoft.Extensions.Options;
using System.Net.NetworkInformation;
using System.Runtime;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using System.Configuration;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Net;
using System.ComponentModel;
using Container = Microsoft.Azure.Cosmos.Container;
using System.Drawing;

namespace CDBInstantMetricsVisualizer.Data
{
    public class CosmosDBService
    {
        Database? database;
        Container? container;
        Container? leaseContainer;

        string DatabaseName;
        string ContainerName;
        CosmosClient cosmosClient;

        string pkToMonitor;

        bool changeFeedInitialized = false; 

        public Dictionary<string, List<CDBMetric>> DashboardMetrics=new Dictionary<string, List<CDBMetric>>();
        public List<string> UniqueFunctionNames=new List<string>();
        public List<string> UniqueStatusCode = new List<string>();

        public CosmosDBService(string Endpoint, string Key, string Database, string Container,string preferredregions)
        {
            // Configure CosmosClientOptions
            var clientOptions = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    IgnoreNullValues = true,
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                ApplicationName = "ChatFrontEnd",
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = preferredregions.Split(','),



            };

            // Create a CosmosClient using the endpoint and key
            cosmosClient = new CosmosClient(Endpoint, Key, clientOptions);
            database = cosmosClient?.GetDatabase(Database);
            container = database?.GetContainer(Container);
            leaseContainer = database?.GetContainer("leases");

            DatabaseName = Database;
            ContainerName= Container;

        }

        public void initChnageFeed(string pk)
        {
            if(changeFeedInitialized) return;

            changeFeedInitialized = true;

            pkToMonitor = pk;

            ChangeFeedProcessor changeFeedProcessor = cosmosClient.GetContainer(DatabaseName, ContainerName)
             .GetChangeFeedProcessorBuilder<CDBMetric>("MetricsDashboard", HandleChangesAsync)
                 .WithInstanceName("MetricsDashboard")
                 .WithLeaseContainer(leaseContainer) 
                 .WithStartTime(DateTime.Now)
                 .Build();

            DashboardMetrics = new Dictionary<string, List<CDBMetric>>();
            changeFeedProcessor.StartAsync();
        }

        async Task HandleChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<CDBMetric> changes, CancellationToken cancellationToken)
        {
            foreach (var change in changes)
            {
                if (pkToMonitor == change.pk)
                {
                    string  TimeInSec=change.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss");
                    if (!DashboardMetrics.ContainsKey(TimeInSec))                   
                    {
                        DashboardMetrics.Add(TimeInSec, new List<CDBMetric>());
                    }
                    DashboardMetrics[TimeInSec].Add(change);

                    if(!UniqueFunctionNames.Contains(change.metricName))
                        UniqueFunctionNames.Add(change.metricName);

                    if (!UniqueStatusCode.Contains(change.StatusCode))
                        UniqueStatusCode.Add(change.StatusCode);
                }
            }
        }
       
      

    }


    public record CDBMetric(
            string id,
            string pk,
            string metricName,
            double RU,
            string StatusCode,
            double Latency,
            DateTime TimeStamp
        );


}