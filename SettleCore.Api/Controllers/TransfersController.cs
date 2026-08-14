using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SettleCore.Api.Controllers;
[ApiController]
[Route("api/v1/[controller]")]
public class TransfersController : ControllerBase
{
    [HttpPost]
    public IActionResult InitiateTransfer([FromBody] TransferRequest request)
    {
        // Returns immediate non-blocking 202 Accepted
        return Accepted(new
        {
            TransferId = Guid.NewGuid(),
            Status = "PENDING_SETTLEMENT",
            Message = "Transfer accepted for processing."
        });
    }
}

public record TransferRequest(string SenderAccount, string RecipientAccount, decimal Amount);