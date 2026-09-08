using Hangfire;
using MassTransit;
using Microsoft.Extensions.Logging;
using SettleCore.Core.Contracts;
using SettleCore.Core.Saga;
using SettleCore.Infrastructure.Services;
using System;

namespace SettleCore.Infrastructure.StateMachines;

public class TransferSagaStateMachine : MassTransitStateMachine<TransferState>
{
    private readonly ILogger<TransferSagaStateMachine>? _logger;
    private readonly IBackgroundJobClient? _backgroundJobClient;

    public State Submitted { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State PENDING_SETTLEMENT { get; private set; } = null!;
    public State PendingSettlement => PENDING_SETTLEMENT;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;
    public State FailedAndReversed { get; private set; } = null!;

    public Event<ITransferRequestedEvent> TransferRequested { get; private set; } = null!;
    public Event<NipSwitchTimeoutEvent> SwitchTimedOut { get; private set; } = null!;
    public Event<NipReconciliationSuccess> ReconciliationSuccess { get; private set; } = null!;
    public Event<NipReconciliationFailed> ReconciliationFailed { get; private set; } = null!;

    public TransferSagaStateMachine(
        ILogger<TransferSagaStateMachine>? logger = null,
        IBackgroundJobClient? backgroundJobClient = null)
    {
        _logger = logger;
        _backgroundJobClient = backgroundJobClient;

        InstanceState(x => x.CurrentState);

        // Correlate events by TransferId (CorrelationId in TransferState)
        Event(() => TransferRequested, x => x.CorrelateById(context => context.Message.TransferId));
        Event(() => SwitchTimedOut, x => x.CorrelateById(context => context.Message.TransferId));
        Event(() => ReconciliationSuccess, x => x.CorrelateById(context => context.Message.TransferId));
        Event(() => ReconciliationFailed, x => x.CorrelateById(context => context.Message.TransferId));

        // Initial transfer intake flow
        Initially(
            When(TransferRequested)
                .Then(context =>
                {
                    _logger?.LogInformation(
                        "Saga initialized for TransferId: {TransferId}, Amount: {Amount}, Recipient: {Recipient}",
                        context.Message.TransferId, context.Message.Amount, context.Message.RecipientAccount);

                    context.Saga.SenderAccount = context.Message.SenderAccount;
                    context.Saga.DestinationAccount = context.Message.RecipientAccount;
                    context.Saga.Amount = context.Message.Amount;
                    context.Saga.IdempotencyKey = context.Message.IdempotencyKey;
                    context.Saga.CreatedAtUtc = context.Message.CreatedAtUtc;
                })
                .TransitionTo(Submitted)
                .Then(context =>
                {
                    _logger?.LogInformation(
                        "Transfer {TransferId} transitioned to Submitted, moving to Processing state",
                        context.Saga.CorrelationId);
                })
                .TransitionTo(Processing)
        );

        // Handling switch timeout during processing
        During(Processing,
            When(SwitchTimedOut)
                .Then(context =>
                {
                    _logger?.LogWarning(
                        "NIP Switch timeout occurred for TransferId: {TransferId}. Reason: {Reason}. Transitioning to PENDING_SETTLEMENT.",
                        context.Saga.CorrelationId, context.Message.Reason);

                    context.Saga.FailureReason = context.Message.Reason;
                })
                .TransitionTo(PENDING_SETTLEMENT)
        );

        // State Machine Trigger: entering PENDING_SETTLEMENT schedules Hangfire async polling
        WhenEnter(PENDING_SETTLEMENT, binder => binder
            .Then(context =>
            {
                var transferId = context.Saga.CorrelationId;
                _logger?.LogInformation(
                    "Transfer {TransferId} entered PENDING_SETTLEMENT. Scheduling Hangfire reconciliation polling in 15 seconds.",
                    transferId);

                if (_backgroundJobClient != null)
                {
                    _backgroundJobClient.Schedule<INipReconciliationService>(
                        service => service.PollTransactionStatusAsync(transferId),
                        TimeSpan.FromSeconds(15));
                }
                else
                {
                    BackgroundJob.Schedule<INipReconciliationService>(
                        service => service.PollTransactionStatusAsync(transferId),
                        TimeSpan.FromSeconds(15));
                }

                _logger?.LogInformation(
                    "Hangfire background job scheduled for TransferId: {TransferId} with 15-second delay.",
                    transferId);
            })
        );

        // Handling out-of-process reconciliation success and failure in PENDING_SETTLEMENT
        During(PENDING_SETTLEMENT,
            When(ReconciliationSuccess)
                .ThenAsync(async context =>
                {
                    _logger?.LogInformation(
                        "Reconciliation confirmed SUCCESS for TransferId: {TransferId}. SwitchReference: {SwitchReference}. Transitioning to Completed.",
                        context.Saga.CorrelationId, context.Message.SwitchReference);

                    context.Saga.SwitchReference = context.Message.SwitchReference;
                    context.Saga.CompletedAtUtc = context.Message.ReconciledAtUtc;

                    await context.Publish<INotifyUserEvent>(new NotifyUserEvent(
                        TransferId: context.Saga.CorrelationId,
                        Status: "SUCCESS",
                        Message: "Transfer completed successfully.",
                        TimestampUtc: DateTime.UtcNow
                    ));

                    _logger?.LogInformation(
                        "Published INotifyUserEvent (SUCCESS) for TransferId: {TransferId}",
                        context.Saga.CorrelationId);
                })
                .TransitionTo(Completed),

            When(ReconciliationFailed)
                .ThenAsync(async context =>
                {
                    _logger?.LogWarning(
                        "NIP Reconciliation FAILED for TransferId: {TransferId}. Reason: {Reason}. Triggering automated ledger reversal compensation.",
                        context.Saga.CorrelationId, context.Message.Reason);

                    context.Saga.FailureReason = context.Message.Reason;

                    await context.Publish<IReverseSenderDebitCommand>(new ReverseSenderDebitCommand(
                        TransferId: context.Saga.CorrelationId,
                        SenderAccountId: context.Saga.SenderAccount,
                        Amount: context.Saga.Amount,
                        FailureReason: context.Message.Reason,
                        TimestampUtc: DateTime.UtcNow
                    ));

                    await context.Publish<INotifyUserEvent>(new NotifyUserEvent(
                        TransferId: context.Saga.CorrelationId,
                        Status: "FAILED_AND_REVERSED",
                        Message: "Transfer failed. Your account has been credited.",
                        TimestampUtc: DateTime.UtcNow
                    ));

                    _logger?.LogInformation(
                        "Dispatched IReverseSenderDebitCommand and INotifyUserEvent (FAILED_AND_REVERSED) for TransferId: {TransferId}",
                        context.Saga.CorrelationId);
                })
                .TransitionTo(FailedAndReversed)
        );
    }
}
