using System;
using System.Collections.Generic;
using System.Linq;
using AmlGuard.Plugins.Domain;
using AmlGuard.Plugins.Schema;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace AmlGuard.Plugins.Data
{
    // All Dataverse reads and writes for the AML rules. It only fetches and saves; deciding is the
    // Domain layer's job. QueryExpression is used instead of FetchXML to stay strongly typed.
    internal sealed class AmlRepository
    {
        private readonly IOrganizationService _service;

        public AmlRepository(IOrganizationService service)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }

            _service = service;
        }

        // The settings table is meant to hold one row. If it holds more, the first one returned is used.
        public StoredSettings GetSettings()
        {
            var query = new QueryExpression(SettingsFields.LogicalName)
            {
                ColumnSet = new ColumnSet(
                    SettingsFields.ReportingThreshold,
                    SettingsFields.MinDepositCount,
                    SettingsFields.WindowHours),
                TopCount = 1
            };

            var row = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (row == null)
            {
                return new StoredSettings();
            }

            return new StoredSettings
            {
                RowFound = true,
                ReportingThreshold = AttributeReader.GetDecimal(row, SettingsFields.ReportingThreshold),
                MinDepositCount = AttributeReader.GetInt(row, SettingsFields.MinDepositCount),
                WindowHours = AttributeReader.GetInt(row, SettingsFields.WindowHours)
            };
        }

        // Deposits of one customer inside the window. A single query returns at most 5,000 rows,
        // far more than any realistic window; see docs/limitations in the README.
        public IList<DepositRecord> GetDeposits(Guid customerId, DateTime windowStartUtc, DateTime windowEndUtc)
        {
            var query = new QueryExpression(TransactionFields.LogicalName)
            {
                ColumnSet = new ColumnSet(TransactionFields.Amount, TransactionFields.TransactionDate)
            };
            query.Criteria.AddCondition(TransactionFields.Customer, ConditionOperator.Equal, customerId);
            query.Criteria.AddCondition(TransactionFields.Type, ConditionOperator.Equal, (int)TransactionType.Deposit);
            query.Criteria.AddCondition(TransactionFields.TransactionDate, ConditionOperator.GreaterEqual, windowStartUtc);
            query.Criteria.AddCondition(TransactionFields.TransactionDate, ConditionOperator.LessEqual, windowEndUtc);

            var deposits = new List<DepositRecord>();
            foreach (var row in _service.RetrieveMultiple(query).Entities)
            {
                var amount = AttributeReader.GetDecimal(row, TransactionFields.Amount);
                var date = AttributeReader.GetUtcDate(row, TransactionFields.TransactionDate);
                if (amount.HasValue && date.HasValue)
                {
                    deposits.Add(new DepositRecord(row.Id, amount.Value, date.Value));
                }
            }

            return deposits;
        }

        // Creation time of the customer's most recent alert of this type, whatever its status
        // (a closed alert still counts, so an analyst closing it does not trigger a second one).
        public DateTime? GetLatestAlertCreatedOnUtc(Guid customerId, AlertType alertType)
        {
            var query = new QueryExpression(AlertFields.LogicalName)
            {
                ColumnSet = new ColumnSet(SystemFields.CreatedOn),
                TopCount = 1
            };
            query.Criteria.AddCondition(AlertFields.Customer, ConditionOperator.Equal, customerId);
            query.Criteria.AddCondition(AlertFields.Type, ConditionOperator.Equal, (int)alertType);
            query.AddOrder(SystemFields.CreatedOn, OrderType.Descending);

            var row = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (row == null)
            {
                return null;
            }

            // Dataverse always sets createdon. If it is somehow missing, the alert cannot be placed in
            // time, so it is treated as current: better to skip one alert than to create a duplicate.
            return AttributeReader.GetUtcDate(row, SystemFields.CreatedOn) ?? DateTime.MaxValue;
        }

        // Reads the current name instead of relying on the Target: on Update the Target holds only the
        // changed columns, so one of the two name parts may be missing from it.
        public ContactSnapshot GetContact(Guid contactId)
        {
            var row = _service.Retrieve(
                ContactFields.LogicalName,
                contactId,
                new ColumnSet(ContactFields.FirstName, ContactFields.LastName, ContactFields.ReviewStatus));

            return new ContactSnapshot
            {
                FirstName = row.GetAttributeValue<string>(ContactFields.FirstName),
                LastName = row.GetAttributeValue<string>(ContactFields.LastName),
                ReviewStatus = AttributeReader.GetInt(row, ContactFields.ReviewStatus)
            };
        }

        private const int SanctionedNamePageSize = 5000;

        // Loads the whole sanctions list, page by page. Matching ignores case and diacritics, which a
        // database filter cannot express, so the comparison happens in memory (see the README limitations).
        public IList<string> GetSanctionedNames()
        {
            var names = new List<string>();
            var query = new QueryExpression(SanctionedNameFields.LogicalName)
            {
                ColumnSet = new ColumnSet(SanctionedNameFields.Name),
                PageInfo = new PagingInfo { Count = SanctionedNamePageSize, PageNumber = 1 }
            };

            while (true)
            {
                var page = _service.RetrieveMultiple(query);
                foreach (var row in page.Entities)
                {
                    var name = row.GetAttributeValue<string>(SanctionedNameFields.Name);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }

                if (!page.MoreRecords)
                {
                    return names;
                }

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = page.PagingCookie;
            }
        }

        public bool HasOpenAlert(Guid customerId, AlertType alertType)
        {
            var query = new QueryExpression(AlertFields.LogicalName)
            {
                ColumnSet = new ColumnSet(false),
                TopCount = 1
            };
            query.Criteria.AddCondition(AlertFields.Customer, ConditionOperator.Equal, customerId);
            query.Criteria.AddCondition(AlertFields.Type, ConditionOperator.Equal, (int)alertType);
            query.Criteria.AddCondition(AlertFields.Status, ConditionOperator.Equal, (int)AlertStatus.Open);

            return _service.RetrieveMultiple(query).Entities.Any();
        }

        // Writes only aml_reviewstatus. That column is not a filtering attribute of the sanctions
        // step, so this update cannot trigger the plug-in again.
        public void SetReviewStatus(Guid contactId, ReviewStatus status)
        {
            var update = new Entity(ContactFields.LogicalName, contactId);
            update[ContactFields.ReviewStatus] = new OptionSetValue((int)status);
            _service.Update(update);
        }

        public Guid CreateAlert(
            Guid customerId,
            AlertType type,
            AlertSeverity severity,
            string title,
            string description)
        {
            var alert = new Entity(AlertFields.LogicalName);
            alert[AlertFields.Title] = title;
            alert[AlertFields.Type] = new OptionSetValue((int)type);
            alert[AlertFields.Severity] = new OptionSetValue((int)severity);
            alert[AlertFields.Customer] = new EntityReference(ContactFields.LogicalName, customerId);
            alert[AlertFields.Description] = description;
            alert[AlertFields.Status] = new OptionSetValue((int)AlertStatus.Open);

            return _service.Create(alert);
        }
    }
}
