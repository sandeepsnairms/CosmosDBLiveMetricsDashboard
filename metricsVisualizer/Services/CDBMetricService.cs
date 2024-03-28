using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;
using static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary;

namespace CDBInstantMetricsVisualizer.Data
{
    public class CDBMetricService
    {
        public Dictionary<string, List<double>> MetricsList;
        public DateTime StartDatetime;
        CosmosDBService cdb;


        public void Init(CosmosDBService cdb)
        {
            ResetData();
            this.cdb = cdb;
        }

        public void ResetData()
        {
            MetricsList = new Dictionary<string, List<double>>();

            if (cdb != null)
                cdb.DashboardMetrics.Clear();

            StartDatetime = DateTime.Now.AddMinutes(-2);
        }

        public async Task UpdateMetricsAsync(MetricType mtype, List<string> xAxislabels)
        {
            try
            {
                //making sure there is 2 minutes data
                DateTime endDt = DateTime.Now;
                if (endDt.Subtract(StartDatetime).TotalSeconds > 120)
                    StartDatetime = endDt.AddSeconds(-120);

                int counter = 0;
                while (StartDatetime.AddSeconds(counter) <= endDt)
                {
                    string timeLookupKey;

                    //look if this metric exists for given time.
                    timeLookupKey = StartDatetime.AddSeconds(counter).ToString("yyyy-MM-dd HH:mm:ss");
                    xAxislabels.Add(StartDatetime.AddSeconds(counter).ToString("HH:mm:ss"));

                    List<string> metricKeys = new List<string>();
                    CDBMetricService.MetricAggregationType aggType = CDBMetricService.MetricAggregationType.Count;
                    switch (mtype)
                    {
                        case MetricType.RU:
                            aggType = CDBMetricService.MetricAggregationType.Sum;
                            metricKeys = cdb.UniqueFunctionNames;
                            break;
                        case MetricType.Latency:
                            aggType = CDBMetricService.MetricAggregationType.Average;
                            metricKeys = cdb.UniqueFunctionNames;
                            break;
                        case MetricType.StatusCode:
                            aggType = CDBMetricService.MetricAggregationType.Count;
                            metricKeys = cdb.UniqueStatusCode;
                            break;
                    }

                    foreach (var metrickey in metricKeys)
                    {
                        //checking if metric name is new entry
                        if (!MetricsList.ContainsKey(metrickey))
                        {
                            MetricsList.Add(metrickey, new List<double>());

                            //add leading 0 to make sure  chart data is consistent
                            int secondsPassed = (int)endDt.Subtract(StartDatetime).TotalSeconds;
                            List<double> blankList = new List<double>(new double[secondsPassed]);
                            MetricsList[metrickey] = blankList;
                        }


                        double calculatedValue = 0;//;
                        if (cdb.DashboardMetrics.ContainsKey(timeLookupKey)) //if exists in metrics
                        {
                            var metrics = cdb.DashboardMetrics[timeLookupKey];
                            calculatedValue = CalcLineMetricValue(metrics, metrickey, aggType);
                        }
                        else
                        {
                            calculatedValue = 0;
                        }
                        //set value for  the given second
                        MetricsList[metrickey].Add(calculatedValue);


                        //ensure only 120 items are stored.
                        int listCount = MetricsList[metrickey].Count;
                        if (listCount > 120)
                        {
                            MetricsList[metrickey].RemoveRange(0, listCount - 120);
                        }
                    }
                    counter++;
                }
            }
            catch (Exception ex) { }
        }

        private double CalcLineMetricValue(List<CDBMetric> metrics, string metrickey, MetricAggregationType aggType )
        {
            double calcValue = 0;
            List<double> latency_values = new List<double>();//;
            switch (aggType)
            {
                case MetricAggregationType.Sum:
                     foreach (CDBMetric m in metrics)
                    {
                        if (m.metricName == metrickey) //only adding for current metricname
                        {
                            calcValue = calcValue + m.RU;
                        }
                    }
                    return calcValue; 
                case MetricAggregationType.Average:
                    foreach (CDBMetric m in metrics)
                    {
                        if (m.metricName == metrickey) //only adding for current metricname
                        {
                            latency_values.Add(m.Latency);
                        }
                        if(latency_values.Count > 0)
                            calcValue = latency_values.Average();
                        else
                            calcValue = 0;

                    }
                    return calcValue;
                case MetricAggregationType.Count:
                    foreach (CDBMetric m in metrics)
                    {
                        if (m.StatusCode == metrickey) //only adding for current metricname
                        {
                            calcValue++;
                        }
                    }
                    return calcValue;
            }

            return 0;
        }


        public enum MetricType {RU,Latency,StatusCode};
        public enum MetricAggregationType { Sum, Average, Count };

    }
}
