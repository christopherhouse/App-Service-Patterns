using Azure.Messaging.ServiceBus;

namespace AdventureWorksApi.Services;

public class MessagingClient
{
    private readonly ServiceBusClient _client;

    public MessagingClient(string connectionString)
    {
        _client = new ServiceBusClient(connectionString);
    }

    public async Task SendMessageAsync(string queueName, string message)
    {
        var sender = _client.CreateSender(queueName);
        var serviceBusMessage = new ServiceBusMessage(message);
        serviceBusMessage.ContentType = "application/json";

        await sender.SendMessageAsync(serviceBusMessage);
    }
}
