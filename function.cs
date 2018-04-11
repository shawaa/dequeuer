using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

[FunctionName("dequeuer")]                    
public static void Run(
    [ServiceBusTrigger("test1", Connection = "Endpoint=sb://function-dequeuer-bus.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=tAITq/0PB5gvGS5FYcagimv/DgsKrvvYBqeT2J4DoEk=")] 
    string myQueueItem, 
    TraceWriter log)
{
    log.Info($"C# ServiceBus queue trigger function processed message: {myQueueItem}");
}