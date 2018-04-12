using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;

namespace dequeuer
{
    public static class Functions
    {
        [FunctionName("dequeuer")]
        public static void Run(
        [ServiceBusTrigger("test1", AccessRights.Manage, Connection = "sbConnection")]
            string myQueueItem,
            TraceWriter log)
        {
            log.Info($"C# ServiceBus queue trigger function processed message: {myQueueItem}");
        }
    }
}