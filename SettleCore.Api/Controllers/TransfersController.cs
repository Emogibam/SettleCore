using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SettleCore.Core.Contracts;

namespace SettleCore.Api.Controllers;
[ApiController]
[Route("api/v1/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;

    public TransfersController(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    [HttpPost]
    public async Task<IActionResult> InitiateTransfer([FromBody] TransferRequest request)
    {
        var idempotencyKey = Request.Headers["X-Idempotency-Key"].ToString();
        var transferId = Guid.NewGuid();

        // Publish event to RabbitMQ (Non-blocking!)
        await _publishEndpoint.Publish<ITransferRequestedEvent>(new
        {
            TransferId = transferId,
            SenderAccount = request.SenderAccount,
            RecipientAccount = request.RecipientAccount,
            Amount = request.Amount,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = DateTime.UtcNow
        });

        return Accepted(new
        {
            TransferId = transferId,
            Status = "PENDING_SETTLEMENT",
            Message = "Transfer queued successfully for settlement processing."
        });
    }
}

public record TransferRequest(string SenderAccount, string RecipientAccount, decimal Amount);