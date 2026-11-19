using System;
using AmlGuard.Plugins;
using AmlGuard.Plugins.Schema;
using DataverseFakes;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Demo
{
    // An in-memory Dataverse. Real Dataverse runs the registered plug-in steps by itself when a record is
    // saved; here that step is imitated: save the record, then run the matching plug-in as PostOperation.
    // All scenario code only needs IOrganizationService, so it would work against a real environment too.
    internal sealed class DemoEnvironment
    {
        private const int PostOperationStage = 40;

        private readonly XrmFakedContext _context = new XrmFakedContext();

        public IOrganizationService Service
        {
            get { return _context.GetOrganizationService(); }
        }

        public Guid Create(Entity entity)
        {
            var id = Service.Create(entity);
            entity.Id = id;

            switch (entity.LogicalName)
            {
                case TransactionFields.LogicalName:
                    _context.ExecutePluginWithTarget<SplitDepositPlugin>(
                        entity, messageName: "Create", stage: PostOperationStage);
                    break;
                case ContactFields.LogicalName:
                    _context.ExecutePluginWithTarget<SanctionsCheckPlugin>(
                        entity, messageName: "Create", stage: PostOperationStage);
                    break;
            }

            return id;
        }

        public void RenameContact(Guid contactId, string firstName, string lastName)
        {
            var update = new Entity(ContactFields.LogicalName, contactId);
            update[ContactFields.FirstName] = firstName;
            update[ContactFields.LastName] = lastName;
            Service.Update(update);

            _context.ExecutePluginWithTarget<SanctionsCheckPlugin>(
                update, messageName: "Update", stage: PostOperationStage);
        }
    }
}
