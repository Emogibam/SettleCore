using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SettleCore.Core.Contracts;
public interface ITransferRequestedEvent
{
    Guid TransferId { get; }
    string SenderAccount { get; }
    string RecipientAccount { get; }
    decimal Amount { get; }
    string IdempotencyKey { get; }
    DateTime CreatedAtUtc { get; }
}
