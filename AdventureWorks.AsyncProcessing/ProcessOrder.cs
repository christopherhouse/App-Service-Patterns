using System;
using System.Threading.Tasks;
using AdventureWorksApi.Data;
using AdventureWorksApi.Data.Dto;
using AdventureWorksApi.Data.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AdventureWorks.AsyncProcessing
{
    public class ProcessOrder
    {
        private readonly ILogger<ProcessOrder> _logger;
        private readonly AdventureWorksContext _db;

        public ProcessOrder(ILogger<ProcessOrder> logger, AdventureWorksContext db)
        {
            _logger = logger;
            _db = db;
        }

        [Function(nameof(ProcessOrder))]
        public async Task Run(
            [ServiceBusTrigger("inbound", Connection = "serviceBusConnectionString")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            var orderDto = message.Body.ToObjectFromJson<OrderDto>();
            var order = orderDto.ToOrder();

            await _db.Orders.AddAsync(order);
            await _db.SaveChangesAsync();

            var orderStatus = await _db.OrderStatuses.Where(o => o.OrderNumber == order.OrderNumber)
                .FirstOrDefaultAsync();

            if (orderStatus != null)
            {
                orderStatus.Status = "Accepted";
                await _db.SaveChangesAsync();
            }
                

            // Complete the message
            await messageActions.CompleteMessageAsync(message);
        }
    }
}
