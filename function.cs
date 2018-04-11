using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;

public static void Run(BrokeredMessage message, TraceWriter log)
{
    log.Info($"C# ServiceBus queue trigger function processed message: {message.MessageId}");
    try
    {
        if (message != null)
        {
            //do something with the message here
            log.Info("Completing message.");
            message.Complete();
        }
    }
    catch (Exception ex)
    {
        message.Abandon();
        log.Info($"Exception occurred: {ex.Message}");
    }
}