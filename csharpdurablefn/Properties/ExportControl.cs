using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace csharpdurablefn.Properties
{
    public class ExportControl
    {
        public ExportControl(int workspaceId, string repository, string branch, int connectionId, string runFrequency, string date, string nextRunTime, string connectionType)
        {
            WorkspaceId = workspaceId;
            Repository = repository;
            Branch = branch;
            ConnectionId = connectionId;
            RunFrequency = runFrequency;
            Date = date;
            NextRunTime = nextRunTime;
            ConnectionType = connectionType;
        }

        [JsonProperty("WorkspaceId")]
        public int WorkspaceId { get; set; }

        [JsonProperty("Name")]
        public string ConnectionName { get; set; }

        [JsonProperty("Repository")]
        public string Repository { get; set; }
        
        [JsonProperty("Branch")]
        public string Branch { get; set; }
        
        [JsonProperty("ConnectionId")]
        public int ConnectionId { get; set; }
        
        [JsonProperty("RunFrequency")]
        public string RunFrequency { get; set; }
        
        [JsonProperty("Date")]
        public string Date { get; set; }
        
        [JsonProperty("NextRunTime")]
        public string NextRunTime { get; set; }
        
        [JsonProperty("ConnectionType")]
        public string ConnectionType { get; set; }
    }
}
