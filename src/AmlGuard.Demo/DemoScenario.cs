using System;
using System.Globalization;
using System.Linq;
using AmlGuard.Plugins.Schema;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace AmlGuard.Demo
{
    // The story told by the demo: two customers who structure or do not, and three name checks.
    internal sealed class DemoScenario
    {
        private readonly DemoEnvironment _env;
        private readonly DateTime _now = DateTime.UtcNow;

        public DemoScenario(DemoEnvironment env)
        {
            _env = env;
        }

        public void Run()
        {
            Heading("1. Seed fictional data");
            SeedReferenceData();
            var maria = CreateCustomer("Maria", "Keller");
            var daniel = CreateCustomer("Daniel", "Brooks");

            Heading("2. Split deposits (structuring): Maria Keller, three deposits under 10,000");
            Deposit(maria, 4000m, hoursAgo: 3);
            Deposit(maria, 3500m, hoursAgo: 2);
            Deposit(maria, 3200m, hoursAgo: 0);

            Heading("3. A fourth deposit in the same window must not raise a second alert");
            Deposit(maria, 500m, hoursAgo: 0);

            Heading("4. Daniel Brooks: single deposits, no pattern");
            Deposit(daniel, 9000m, hoursAgo: 1);
            Deposit(daniel, 12000m, hoursAgo: 0);

            Heading("5. Sanctions check on Create: spelling differs, name is the same");
            // Upper case, extra spaces and an accent: still an exact match after normalization.
            CreateCustomer("JOS\u00c9", "  martinez ");

            Heading("6. Sanctions check on Update: a clean contact is renamed to a listed name");
            var elena = CreateCustomer("Elena", "Novakova");
            Step("Rename Elena Novakova to Elena Novak");
            _env.RenameContact(elena, "Elena", "Novak");

            Heading("7. Result");
            PrintAlerts();
            PrintContacts();
        }

        private void SeedReferenceData()
        {
            _env.Create(SampleData.Settings(10000m, 3, 24));
            Step("Settings: threshold 10,000, at least 3 deposits, window 24 hours");

            foreach (var name in SampleData.SanctionedNames)
            {
                _env.Create(SampleData.SanctionedName(name));
            }

            Step("Sanctions list: " + string.Join(", ", SampleData.SanctionedNames));
        }

        private Guid CreateCustomer(string firstName, string lastName)
        {
            Step("Create contact " + firstName.Trim() + " " + lastName.Trim());
            return _env.Create(SampleData.Contact(firstName, lastName));
        }

        private void Deposit(Guid customerId, decimal amount, double hoursAgo)
        {
            var when = _now.AddHours(-hoursAgo);
            Step(string.Format(
                CultureInfo.InvariantCulture, "Deposit {0:N2} ({1:0} h ago)", amount, hoursAgo));
            _env.Create(SampleData.Deposit(customerId, amount, when));
            Console.WriteLine("   alerts so far: " + AlertCount());
        }

        private int AlertCount()
        {
            return RetrieveAll(AlertFields.LogicalName).Count;
        }

        private void PrintAlerts()
        {
            var alerts = RetrieveAll(AlertFields.LogicalName);
            Console.WriteLine("Alerts: " + alerts.Count);

            foreach (var alert in alerts)
            {
                var type = (AlertType)alert.GetAttributeValue<OptionSetValue>(AlertFields.Type).Value;
                var severity = (AlertSeverity)alert.GetAttributeValue<OptionSetValue>(AlertFields.Severity).Value;
                var customer = alert.GetAttributeValue<EntityReference>(AlertFields.Customer);

                Console.WriteLine("  [{0}] {1} for {2}", severity, type, NameOf(customer.Id));
                Console.WriteLine("      " + alert.GetAttributeValue<string>(AlertFields.Description));
            }
        }

        private void PrintContacts()
        {
            Console.WriteLine();
            Console.WriteLine("Contacts and review status:");

            foreach (var contact in RetrieveAll(ContactFields.LogicalName))
            {
                var status = contact.GetAttributeValue<OptionSetValue>(ContactFields.ReviewStatus);
                var label = status != null && status.Value == (int)ReviewStatus.UnderReview
                    ? "UNDER REVIEW"
                    : "clear";
                Console.WriteLine("  {0,-22} {1}", NameOf(contact), label);
            }
        }

        private string NameOf(Guid contactId)
        {
            var contact = _env.Service.Retrieve(
                ContactFields.LogicalName,
                contactId,
                new ColumnSet(ContactFields.FirstName, ContactFields.LastName));
            return NameOf(contact);
        }

        private static string NameOf(Entity contact)
        {
            return (contact.GetAttributeValue<string>(ContactFields.FirstName) + " "
                + contact.GetAttributeValue<string>(ContactFields.LastName)).Trim();
        }

        private System.Collections.Generic.IList<Entity> RetrieveAll(string logicalName)
        {
            var query = new QueryExpression(logicalName) { ColumnSet = new ColumnSet(true) };
            return _env.Service.RetrieveMultiple(query).Entities.ToList();
        }

        private static void Heading(string text)
        {
            Console.WriteLine();
            Console.WriteLine("== " + text + " ==");
        }

        private static void Step(string text)
        {
            Console.WriteLine(">> " + text);
        }
    }
}
