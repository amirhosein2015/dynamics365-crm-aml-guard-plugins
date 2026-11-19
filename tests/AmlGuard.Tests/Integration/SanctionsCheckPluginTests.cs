using System;
using System.Collections.Generic;
using System.Linq;
using AmlGuard.Plugins;
using AmlGuard.Plugins.Schema;
using DataverseFakes;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Xunit;

namespace AmlGuard.Tests.Integration
{
    // The contact is seeded in its final (post-save) state and passed as Target, like PostOperation.
    // For Update the Target holds only the changed columns, as Dataverse does.
    public class SanctionsCheckPluginTests
    {
        private const int PostOperationStage = 40;

        private readonly XrmFakedContext _context = new XrmFakedContext();

        private IOrganizationService Service
        {
            get { return _context.GetOrganizationService(); }
        }

        // ---- Helpers ----

        private static Entity Contact(string firstName, string lastName, ReviewStatus? status = null)
        {
            var contact = new Entity(ContactFields.LogicalName, Guid.NewGuid());
            if (firstName != null)
            {
                contact[ContactFields.FirstName] = firstName;
            }

            if (lastName != null)
            {
                contact[ContactFields.LastName] = lastName;
            }

            if (status.HasValue)
            {
                contact[ContactFields.ReviewStatus] = new OptionSetValue((int)status.Value);
            }

            return contact;
        }

        private static Entity Sanctioned(string name)
        {
            var entry = new Entity(SanctionedNameFields.LogicalName, Guid.NewGuid());
            entry[SanctionedNameFields.Name] = name;
            entry[SanctionedNameFields.Source] = "Fictional list";
            return entry;
        }

        private static Entity ExistingAlert(Entity contact, AlertStatus status)
        {
            var alert = new Entity(AlertFields.LogicalName, Guid.NewGuid());
            alert[AlertFields.Type] = new OptionSetValue((int)AlertType.SanctionsMatch);
            alert[AlertFields.Severity] = new OptionSetValue((int)AlertSeverity.Critical);
            alert[AlertFields.Customer] = new EntityReference(ContactFields.LogicalName, contact.Id);
            alert[AlertFields.Status] = new OptionSetValue((int)status);
            return alert;
        }

        private void Seed(params Entity[] entities)
        {
            _context.Initialize(entities.ToList());
        }

        private void RunCreate(Entity contact)
        {
            _context.ExecutePluginWithTarget<SanctionsCheckPlugin>(
                contact, messageName: "Create", stage: PostOperationStage);
        }

        // Target with only the named columns changed, like an Update message.
        private void RunUpdate(Entity contact, params string[] changedColumns)
        {
            var target = new Entity(ContactFields.LogicalName, contact.Id);
            foreach (var column in changedColumns)
            {
                target[column] = contact[column];
            }

            _context.ExecutePluginWithTarget<SanctionsCheckPlugin>(
                target, messageName: "Update", stage: PostOperationStage);
        }

        private IList<Entity> Alerts()
        {
            var query = new QueryExpression(AlertFields.LogicalName) { ColumnSet = new ColumnSet(true) };
            return Service.RetrieveMultiple(query).Entities.ToList();
        }

        private int? ReviewStatusOf(Entity contact)
        {
            var stored = Service.Retrieve(
                ContactFields.LogicalName, contact.Id, new ColumnSet(ContactFields.ReviewStatus));
            var value = stored.GetAttributeValue<OptionSetValue>(ContactFields.ReviewStatus);
            return value == null ? (int?)null : value.Value;
        }

        // ---- Match on Create ----

        [Fact]
        public void ExactMatchOnCreate_FlagsTheContactAndCreatesACriticalAlert()
        {
            var contact = Contact("Ivan", "Petrov");
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunCreate(contact);

            Assert.Equal((int)ReviewStatus.UnderReview, ReviewStatusOf(contact));
            var alert = Assert.Single(Alerts());
            Assert.Equal((int)AlertType.SanctionsMatch, alert.GetAttributeValue<OptionSetValue>(AlertFields.Type).Value);
            Assert.Equal((int)AlertSeverity.Critical, alert.GetAttributeValue<OptionSetValue>(AlertFields.Severity).Value);
            Assert.Equal((int)AlertStatus.Open, alert.GetAttributeValue<OptionSetValue>(AlertFields.Status).Value);
            Assert.Equal(contact.Id, alert.GetAttributeValue<EntityReference>(AlertFields.Customer).Id);
        }

        [Fact]
        public void MatchedAlert_DescribesTheMatch()
        {
            var contact = Contact("Ivan", "Petrov");
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunCreate(contact);

            var description = Assert.Single(Alerts()).GetAttributeValue<string>(AlertFields.Description);
            Assert.Contains("\"Ivan Petrov\"", description);
        }

        [Fact]
        public void MatchIgnoresCaseSpacingAndDiacritics()
        {
            var contact = Contact("  JOS\u00c9 ", "garc\u00eda");
            Seed(contact, Sanctioned("Jose   Garcia"));

            RunCreate(contact);

            Assert.Equal((int)ReviewStatus.UnderReview, ReviewStatusOf(contact));
            Assert.Single(Alerts());
        }

        [Fact]
        public void MatchWithTheSecondEntryOfTheList_IsFound()
        {
            var contact = Contact("Ivan", "Petrov");
            Seed(contact, Sanctioned("Anna Smith"), Sanctioned("Ivan Petrov"), Sanctioned("Li Wei"));

            RunCreate(contact);

            Assert.Single(Alerts());
        }

        // ---- No match ----

        [Fact]
        public void NoMatch_LeavesTheContactAndCreatesNoAlert()
        {
            var contact = Contact("Ivan", "Petrova");
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunCreate(contact);

            Assert.Null(ReviewStatusOf(contact));
            Assert.Empty(Alerts());
        }

        [Fact]
        public void EmptySanctionsList_NeverMatches()
        {
            var contact = Contact("Ivan", "Petrov");
            Seed(contact);

            RunCreate(contact);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void ContactWithoutAName_IsSkipped()
        {
            var contact = Contact(null, null);
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunCreate(contact);

            Assert.Empty(Alerts());
        }

        // ---- Update ----

        [Fact]
        public void UpdateOfOnlyTheLastName_UsesTheStoredFirstNameToo()
        {
            var contact = Contact("Ivan", "Petrov");
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunUpdate(contact, ContactFields.LastName);

            Assert.Equal((int)ReviewStatus.UnderReview, ReviewStatusOf(contact));
            Assert.Single(Alerts());
        }

        [Fact]
        public void UpdateOfOnlyTheFirstName_IsAlsoScreened()
        {
            var contact = Contact("Ivan", "Petrov");
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunUpdate(contact, ContactFields.FirstName);

            Assert.Single(Alerts());
        }

        [Fact]
        public void UpdateThatDoesNotChangeTheName_IsNotScreened_ThisPreventsALoop()
        {
            // This is the Target of the plug-in's own review-status update. The name matches the list,
            // but the update carries no name column, so nothing may happen.
            var contact = Contact("Ivan", "Petrov", ReviewStatus.UnderReview);
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunUpdate(contact, ContactFields.ReviewStatus);

            Assert.Empty(Alerts());
        }

        [Fact]
        public void NameChangedToANonMatch_DoesNotClearAnExistingReviewFlag()
        {
            var contact = Contact("Ivan", "Petrova", ReviewStatus.UnderReview);
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunUpdate(contact, ContactFields.LastName);

            Assert.Equal((int)ReviewStatus.UnderReview, ReviewStatusOf(contact));
            Assert.Empty(Alerts());
        }

        // ---- Duplicate alerts ----

        [Fact]
        public void ScreeningTwice_KeepsASingleOpenAlert()
        {
            var contact = Contact("Ivan", "Petrov");
            Seed(contact, Sanctioned("Ivan Petrov"));

            RunCreate(contact);
            RunUpdate(contact, ContactFields.LastName);

            Assert.Single(Alerts());
        }

        [Fact]
        public void ExistingOpenAlert_PreventsAnotherOne()
        {
            var contact = Contact("Ivan", "Petrov", ReviewStatus.UnderReview);
            Seed(contact, Sanctioned("Ivan Petrov"), ExistingAlert(contact, AlertStatus.Open));

            RunUpdate(contact, ContactFields.LastName);

            Assert.Single(Alerts());
        }

        [Fact]
        public void ClosedAlert_AllowsANewAlertWhenTheNameIsScreenedAgain()
        {
            var contact = Contact("Ivan", "Petrov", ReviewStatus.UnderReview);
            Seed(contact, Sanctioned("Ivan Petrov"), ExistingAlert(contact, AlertStatus.Closed));

            RunUpdate(contact, ContactFields.LastName);

            Assert.Equal(2, Alerts().Count);
        }

        [Fact]
        public void OpenAlertOfAnotherContact_DoesNotPreventAnAlert()
        {
            var contact = Contact("Ivan", "Petrov");
            var other = Contact("Anna", "Smith");
            Seed(contact, other, Sanctioned("Ivan Petrov"), ExistingAlert(other, AlertStatus.Open));

            RunCreate(contact);

            Assert.Equal(2, Alerts().Count);
        }

        // ---- Context guards ----

        [Fact]
        public void CallForAnotherTable_IsIgnored()
        {
            // The plug-in is registered on contact only; a Create on another table must do nothing.
            var contact = Contact("Ivan", "Petrov");
            var otherRecord = new Entity(TransactionFields.LogicalName, Guid.NewGuid());
            Seed(contact, otherRecord, Sanctioned("Ivan Petrov"));

            _context.ExecutePluginWithTarget<SanctionsCheckPlugin>(
                otherRecord, messageName: "Create", stage: PostOperationStage);

            Assert.Empty(Alerts());
            Assert.Null(ReviewStatusOf(contact));
        }
    }
}
