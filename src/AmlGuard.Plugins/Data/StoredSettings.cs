namespace AmlGuard.Plugins.Data
{
    // Raw values of the aml_settings row. A null means "not set"; the Domain layer applies defaults.
    internal sealed class StoredSettings
    {
        public decimal? ReportingThreshold { get; set; }

        public int? MinDepositCount { get; set; }

        public int? WindowHours { get; set; }

        public bool RowFound { get; set; }
    }
}
