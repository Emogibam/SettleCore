using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SettleCore.Core.Contracts;
using System;
using System.Threading.Tasks;

namespace SettleCore.Api.Controllers;

[ApiController]
[Route("api/v1/mock/nip")]
public class MockNipBridgeController : ControllerBase
{
    private readonly ILogger<MockNipBridgeController> _logger;

    public MockNipBridgeController(ILogger<MockNipBridgeController> logger)
    {
        _logger = logger;
    }

    [HttpPost("process")]
    public async Task<IActionResult> ProcessTransfer([FromBody] NipTransferRequest request)
    {
        _logger.LogInformation(
            "Received mock NIP transfer request. TransferId: {TransferId}, Destination: {DestinationAccount}, Amount: {Amount}",
            request.TransferId, request.DestinationAccount, request.Amount);

        // 1. Chaos Simulation: Artificial Latency
        // Inject an asynchronous non-blocking delay of 5 to 8 seconds
        int delayMs = Random.Shared.Next(5000, 8001);
        _logger.LogInformation("Injecting artificial switch latency of {DelayMs}ms for TransferId: {TransferId}", delayMs, request.TransferId);
        await Task.Delay(delayMs);

        // 2. Chaos Simulation: Fault Injection (30% failure rate)
        if (Random.Shared.Next(100) < 30)
        {
            _logger.LogWarning("Simulating NIP switch gateway timeout (504) for TransferId: {TransferId}", request.TransferId);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                Error = "Gateway Timeout",
                Message = "NIBSS switch failed to respond within threshold."
            });
        }

        // 3. Successful Resolution (70% success rate)
        var transactionId = Guid.NewGuid();
        var switchReference = $"NIP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        _logger.LogInformation(
            "NIP transfer processed successfully. TransferId: {TransferId}, TransactionId: {TransactionId}, SwitchReference: {SwitchReference}",
            request.TransferId, transactionId, switchReference);

        return Ok(new
        {
            TransactionId = transactionId,
            SwitchReference = switchReference,
            Status = "SUCCESS",
            Message = "Payment processed successfully through NIP switch."
        });
    }

    [HttpGet("status/{transferId}")]
    public IActionResult GetTransferStatus([FromRoute] Guid transferId)
    {
        _logger.LogInformation("Querying mock NIP switch status for TransferId: {TransferId}", transferId);

        var reconGuid = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var switchReference = $"NIP-RECON-{reconGuid}";
        var reconciledAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Mock NIP status resolved to SUCCESS for TransferId: {TransferId}, SwitchReference: {SwitchReference}, ReconciledAtUtc: {ReconciledAtUtc}",
            transferId, switchReference, reconciledAt);

        return Ok(new NipStatusResponse(
            TransferId: transferId,
            Status: "SUCCESS",
            SwitchReference: switchReference,
            ReconciledAtUtc: reconciledAt
        ));
    }
}

public record NipTransferRequest(Guid TransferId, string DestinationAccount, decimal Amount);

