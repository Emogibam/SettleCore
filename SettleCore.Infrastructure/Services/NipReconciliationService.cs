using MassTransit;
using Microsoft.Extensions.Logging;
using SettleCore.Core.Contracts;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SettleCore.Infrastructure.Services;

/// <summary>
/// Hangfire async polling engine worker that queries the payment switch for transaction status
/// and publishes reconciliation events upon confirmation.
/// </summary>
public class NipReconciliationService : INipReconciliationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<NipReconciliationService> _logger;

    public NipReconciliationService(
        IHttpClientFactory httpClientFactory,
        IPublishEndpoint publishEndpoint,
        ILogger<NipReconciliationService> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task PollTransactionStatusAsync(Guid transferId)
    {
        _logger.LogInformation(
            "[Hangfire Worker] Initiating out-of-process switch reconciliation poll for TransferId: {TransferId}",
            transferId);

        try
        {
            var client = _httpClientFactory.CreateClient("MockNipBridge");
            var requestUri = $"/api/v1/mock/nip/status/{transferId}";

            _logger.LogInformation(
                "[Hangfire Worker] Sending GET request to switch status endpoint: {RequestUri} (BaseAddress: {BaseAddress})",
                requestUri, client.BaseAddress);

            var response = await client.GetAsync(requestUri);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "[Hangfire Worker] Switch status poll returned non-success HTTP status {StatusCode} for TransferId: {TransferId}",
                    response.StatusCode, transferId);
                return;
            }

            var statusResponse = await response.Content.ReadFromJsonAsync<NipStatusResponse>();

            if (statusResponse is null)
            {
                _logger.LogError(
                    "[Hangfire Worker] Failed to deserialize switch status response payload for TransferId: {TransferId}",
                    transferId);
                return;
            }

            if (string.Equals(statusResponse.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "[Hangfire Worker] Switch confirmed SUCCESS for TransferId: {TransferId}. SwitchReference: {SwitchReference}, ReconciledAt: {ReconciledAtUtc}",
                    transferId, statusResponse.SwitchReference, statusResponse.ReconciledAtUtc);

                var eventPayload = new NipReconciliationSuccess(
                    transferId,
                    statusResponse.SwitchReference,
                    statusResponse.ReconciledAtUtc
                );

                // Publish to MassTransit message bus for TransferSagaStateMachine consumption
                await _publishEndpoint.Publish(eventPayload);

                _logger.LogInformation(
                    "[Hangfire Worker] Dispatched NipReconciliationSuccess event to MassTransit for TransferId: {TransferId}",
                    transferId);
            }
            else if (string.Equals(statusResponse.Status, "FAILED", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(statusResponse.Status, "REVERSED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "[Hangfire Worker] Switch confirmed definitive FAILURE for TransferId: {TransferId}. Status: {Status}. Emitting NipReconciliationFailed.",
                    transferId, statusResponse.Status);

                var failurePayload = new NipReconciliationFailed(
                    transferId,
                    $"Payment switch confirmed transaction status: {statusResponse.Status}",
                    DateTime.UtcNow
                );

                await _publishEndpoint.Publish(failurePayload);

                _logger.LogInformation(
                    "[Hangfire Worker] Dispatched NipReconciliationFailed event to MassTransit for TransferId: {TransferId}",
                    transferId);
            }
            else
            {
                _logger.LogWarning(
                    "[Hangfire Worker] TransferId: {TransferId} status returned as '{Status}' (pending further reconciliation)",
                    transferId, statusResponse.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[Hangfire Worker] Unhandled exception while polling switch status for TransferId: {TransferId}",
                transferId);
            throw; // Allow Hangfire retry mechanics to handle transient faults
        }
    }
}
