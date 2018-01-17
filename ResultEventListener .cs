using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.InteropExtensions;
using Microsoft.Azure.ServiceBus.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;

namespace UniversalTennis.Algorithm
{
    public class ResultEventListener
    {
        private static ILogger _logger;
        private static DbContextOptionsBuilder<UniversalTennisContext> _options;
        private static IQueueClient _queueClient;
        private const string ServiceBusConnectionString =
                "Endpoint=sb://ut-us-east.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=V7S5r2obOvmtHllt3fjs5x03qZNKcbRbQQvKfKunuBU=";

        public ResultEventListener(
            ILoggerFactory loggerFactory, 
            DbContextOptionsBuilder<UniversalTennisContext> options, 
            IOptions<Config> config)
        {
            var queueName = config.Value.ResultEventQueueName;
            _options = options;
            _logger = loggerFactory.CreateLogger("Algorithm.ResultEventListener");
            _queueClient = new QueueClient(ServiceBusConnectionString, queueName, ReceiveMode.PeekLock);
            _logger.LogInformation("Result event listener initialized...");        
            ReceiveMessages();
        }

        // Receives messages from the queue in a loop
        private static void ReceiveMessages()
        {
            // Register a OnMessage callback
            _queueClient.RegisterMessageHandler(
            async (message, token) =>
            {
                // need to manually instantiate new context because DI in singleton will always resolve same context
                var ctx = new UniversalTennisContext(_options.Options);
                try
                {                 
                    var eventRepo = new EventRepository(ctx);
                    var resultEvent = JsonConvert.DeserializeObject<ResultEvent>(message.GetBody<string>());
                    resultEvent.InfoDoc = JsonConvert.SerializeObject(resultEvent.Info);
                    await eventRepo.InsertResultEvent(resultEvent);
                    _logger.LogInformation(
                        $"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");
                    await _queueClient.CompleteAsync(message.SystemProperties.LockToken);
                }
                catch (Exception e)
                {
                    await _queueClient.DeadLetterAsync(message.SystemProperties.LockToken);
                    _logger.LogError(e, "Failed to process result event");
                }
                finally
                {
                    ctx.Dispose();
                }
            },
            new MessageHandlerOptions(ExceptionHandler)
            {
                MaxConcurrentCalls = 1,
                AutoComplete = false
            });
        }

        private static Task ExceptionHandler(ExceptionReceivedEventArgs args)
        {
            if (args.Exception is MessagingEntityNotFoundException)
            {
                // queue doesn't exist, close the connection
                _queueClient.CloseAsync();
            }
            else
            {
                _logger.LogError(args.Exception, "Failed to recieve result event");
            }
            return Task.FromResult(true);
        }
    }
}
