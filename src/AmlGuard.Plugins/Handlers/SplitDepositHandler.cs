using System;
using System.Globalization;
using AmlGuard.Plugins.Data;
using AmlGuard.Plugins.Domain;
using AmlGuard.Plugins.Schema;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Plugins.Handlers
{
    // Glue between Dataverse and the Domain rule: read inputs, ask SplitDepositEvaluator, write the alert.
    // It contains no business decisions of its own, which keeps the rule testable without Dataverse.
    public sealed class SplitDepositHandler
    {
        private readonly AmlRepository _repository;
        private readonly ITracingService _tracing;

        public SplitDepositHandler(IOrganizationService service, ITracingService tracing)
        {
            if (tracing == null)
            {
                throw new ArgumentNullException(nameof(tracing));
            }

            _repository = new AmlRepository(service);
            _tracing = tracing;
        }

        public void Handle(Entity transaction, Guid transactionId)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            if (AttributeReader.GetInt(transaction, TransactionFields.Type) != (int)TransactionType.Deposit)
            {
                _tracing.Trace("Skipped: the transaction is not a deposit.");
                return;
            }

            var customer = transaction.GetAttributeValue<EntityReference>(TransactionFields.Customer);
            var amount = AttributeReader.GetDecimal(transaction, TransactionFields.Amount);
            if (customer == null || !amount.HasValue)
            {
                _tracing.Trace("Skipped: the deposit has no customer or no amount.");
                return;
            }

            var dateUtc = AttributeReader.GetUtcDate(transaction, TransactionFields.TransactionDate);
            if (!dateUtc.HasValue)
            {
                _tracing.Trace("The deposit has no transaction date; using the current time.");
                dateUtc = DateTime.UtcNow;
            }

            var rules = LoadRules();
            var evaluator = new SplitDepositEvaluator(rules);
            var newDeposit = new DepositRecord(transactionId, amount.Value, dateUtc.Value);

            // Cheap exit first: a deposit that cannot take part (for example one above the threshold)
            // needs no database queries. The evaluator stays the single source of truth for that rule.
            if (evaluator.Evaluate(newDeposit, null, null).Outcome == SplitDepositOutcome.NewDepositNotEligible)
            {
                _tracing.Trace("Skipped: the deposit is not below the reporting threshold.");
                return;
            }

            var otherDeposits = _repository.GetDeposits(
                customer.Id, rules.WindowStartFor(dateUtc.Value), dateUtc.Value);
            var latestAlertUtc = _repository.GetLatestAlertCreatedOnUtc(customer.Id, AlertType.SplitDeposit);

            var result = evaluator.Evaluate(newDeposit, otherDeposits, latestAlertUtc);
            _tracing.Trace(
                "Split deposit check: outcome={0}, qualifying deposits={1}, total={2}.",
                result.Outcome,
                result.QualifyingDepositCount,
                result.QualifyingTotal);

            if (!result.ShouldCreateAlert)
            {
                return;
            }

            var title = string.Format(CultureInfo.InvariantCulture, "Split deposit {0:yyyy-MM-dd HH:mm} UTC", dateUtc.Value);
            var alertId = _repository.CreateAlert(
                customer.Id, AlertType.SplitDeposit, AlertSeverity.High, title, result.BuildDescription());
            _tracing.Trace("Created split deposit alert {0}.", alertId);
        }

        // A missing settings row or a missing value means defaults. An invalid value (for example a
        // threshold of 0) also falls back to defaults: a typo in a settings row must not stop deposits.
        private DepositRules LoadRules()
        {
            var settings = _repository.GetSettings();
            if (!settings.RowFound)
            {
                _tracing.Trace("No aml_settings row found; using default values.");
            }

            try
            {
                return DepositRules.FromSettings(
                    settings.ReportingThreshold, settings.MinDepositCount, settings.WindowHours);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                _tracing.Trace("Invalid aml_settings value for {0}; using default values.", exception.ParamName);
                return DepositRules.Defaults();
            }
        }
    }
}
