namespace AmlGuard.Plugins.Schema
{
    // Logical names and choice values of the Dataverse data model (see docs/data-model.md).
    // Kept in one place so a rename in the portal is a one-file change, not a search-and-replace.

    public static class ContactFields
    {
        public const string LogicalName = "contact";
        public const string FirstName = "firstname";
        public const string LastName = "lastname";
        public const string ReviewStatus = "aml_reviewstatus";
    }

    public static class TransactionFields
    {
        public const string LogicalName = "aml_transaction";
        public const string Customer = "aml_customer";
        public const string Amount = "aml_amount";
        public const string TransactionDate = "aml_transactiondate";
        public const string Type = "aml_type";
    }

    public static class SanctionedNameFields
    {
        public const string LogicalName = "aml_sanctionedname";
        public const string Name = "aml_name";
        public const string Source = "aml_source";
    }

    public static class AlertFields
    {
        public const string LogicalName = "aml_alert";
        public const string Title = "aml_name";
        public const string Type = "aml_type";
        public const string Severity = "aml_severity";
        public const string Customer = "aml_customer";
        public const string Description = "aml_description";
        public const string Status = "aml_status";
    }

    public static class SettingsFields
    {
        public const string LogicalName = "aml_settings";
        public const string ReportingThreshold = "aml_reportingthreshold";
        public const string MinDepositCount = "aml_mindepositcount";
        public const string WindowHours = "aml_windowhours";
    }

    // Used only when no aml_settings row exists yet; real values always come from the table.
    public static class SettingsDefaults
    {
        public const decimal ReportingThreshold = 10000m;
        public const int MinDepositCount = 3;
        public const int WindowHours = 24;
    }

    // Choice values are fixed in the portal on purpose (docs/data-model.md), because
    // Dataverse would otherwise generate values from the publisher's option-value prefix.
    public enum TransactionType
    {
        Deposit = 100000000,
        Withdrawal = 100000001
    }

    public enum AlertType
    {
        SplitDeposit = 100000000,
        SanctionsMatch = 100000001
    }

    public enum AlertSeverity
    {
        Low = 100000000,
        Medium = 100000001,
        High = 100000002,
        Critical = 100000003
    }

    public enum AlertStatus
    {
        Open = 100000000,
        Closed = 100000001
    }

    public enum ReviewStatus
    {
        Clear = 100000000,
        UnderReview = 100000001
    }
}
