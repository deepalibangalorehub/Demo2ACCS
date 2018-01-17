using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.InteropExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using UniversalTennis.Algorithm.Data;
using UniversalTennis.Algorithm.Models;
using UniversalTennis.Algorithm.Repository;

namespace UniversalTennis.Algorithm
{
    public class PlayerEventListener
    {
        private static ILogger _logger;
        private static DbContextOptionsBuilder<UniversalTennisContext> _options;
        private static IQueueClient _queueClient;
        private const string ServiceBusConnectionString =
                "Endpoint=sb://ut-us-east.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=V7S5r2obOvmtHllt3fjs5x03qZNKcbRbQQvKfKunuBU=";

        public PlayerEventListener(
            ILoggerFactory loggerFactory, 
            DbContextOptionsBuilder<UniversalTennisContext> options,
            Microsoft.Extensions.Options.IOptions<Config> config)
        {
            var queueName = config.Value.PlayerEventQueueName;
            _options = options;
            _logger = loggerFactory.CreateLogger("Algorithm.PlayerEventListener");
            _queueClient = new QueueClient(ServiceBusConnectionString, queueName, ReceiveMode.PeekLock);
            _logger.LogInformation("Player event listener initialized...");
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
                    var playerEvent = JsonConvert.DeserializeObject<PlayerEvent>(message.GetBody<string>());
                    playerEvent.InfoDoc = JsonConvert.SerializeObject(playerEvent.Info);
                    await eventRepo.InsertPlayerEvent(playerEvent);
                    
                    // Process the message
                    _logger.LogInformation(
                        $"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{Encoding.UTF8.GetString(message.Body)}");
                    await _queueClient.CompleteAsync(message.SystemProperties.LockToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to process player event");
                    // add to dead letters queue so message isn't lost
                    await _queueClient.DeadLetterAsync(message.SystemProperties.LockToken);
                }
                finally
                {
                    ctx.Dispose();
                    // Complete the message so that it is not received again.
                    // This can be done only if the queueClient is opened in ReceiveMode.PeekLock mode.
                    
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
                _logger.LogError(args.Exception, "Failed to recieve player event");
            }
            return Task.FromResult(true);
        }
    }
}
