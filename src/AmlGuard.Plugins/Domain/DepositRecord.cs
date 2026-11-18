using System;

namespace AmlGuard.Plugins.Domain
{
    // A deposit as the rule sees it: just id, amount and time (UTC), nothing from the Dataverse SDK.
    public sealed class DepositRecord
    {
        public DepositRecord(Guid id, decimal amount, DateTime dateUtc)
        {
            Id = id;
            Amount = amount;
            DateUtc = dateUtc;
        }

        public Guid Id { get; }

        public decimal Amount { get; }

        public DateTime DateUtc { get; }
    }
}
