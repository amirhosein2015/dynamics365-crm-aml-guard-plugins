using System;
using AmlGuard.Plugins.Schema;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Demo
{
    // Fictional data only. Any resemblance to real people or real sanctions lists is accidental.
    internal static class SampleData
    {
        public static readonly string[] SanctionedNames =
        {
            "Boris Vasiliev",
            "Jose Martinez",
            "Elena Novak"
        };

        public static Entity Settings(decimal threshold, int minDepositCount, int windowHours)
        {
            var settings = new Entity(SettingsFields.LogicalName);
            settings["aml_name"] = "Default";
            settings[SettingsFields.ReportingThreshold] = threshold;
            settings[SettingsFields.MinDepositCount] = minDepositCount;
            settings[SettingsFields.WindowHours] = windowHours;
            return settings;
        }

        public static Entity SanctionedName(string name)
        {
            var entry = new Entity(SanctionedNameFields.LogicalName);
            entry[SanctionedNameFields.Name] = name;
            entry[SanctionedNameFields.Source] = "Fictional demo list";
            return entry;
        }

        public static Entity Contact(string firstName, string lastName)
        {
            var contact = new Entity(ContactFields.LogicalName);
            contact[ContactFields.FirstName] = firstName;
            contact[ContactFields.LastName] = lastName;
            return contact;
        }

        public static Entity Deposit(Guid customerId, decimal amount, DateTime dateUtc)
        {
            var deposit = new Entity(TransactionFields.LogicalName);
            deposit["aml_name"] = "Deposit " + amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            deposit[TransactionFields.Customer] = new EntityReference(ContactFields.LogicalName, customerId);
            deposit[TransactionFields.Amount] = new Money(amount);
            deposit[TransactionFields.TransactionDate] = dateUtc;
            deposit[TransactionFields.Type] = new OptionSetValue((int)TransactionType.Deposit);
            return deposit;
        }
    }
}
