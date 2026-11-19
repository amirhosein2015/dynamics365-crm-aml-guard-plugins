using System;
using AmlGuard.Plugins.Domain;
using AmlGuard.Plugins.Handlers;
using AmlGuard.Plugins.Schema;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Plugins
{
    // Registration (two steps, both PostOperation / synchronous, table contact):
    //   1. Create
    //   2. Update with filtering attributes firstname, lastname
    // Three layers keep it from looping when it updates the contact itself: the filtering attributes,
    // the check below that an Update really carries a name column, and the depth guard.
    public sealed class SanctionsCheckPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            var isCreate = string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase);
            var isUpdate = string.Equals(context.MessageName, "Update", StringComparison.OrdinalIgnoreCase);

            if (!(isCreate || isUpdate)
                || !string.Equals(context.PrimaryEntityName, ContactFields.LogicalName, StringComparison.Ordinal)
                || !PluginContextHelper.HasEntityTarget(context))
            {
                tracing.Trace("SanctionsCheckPlugin ignored a call that is not Create or Update on contact.");
                return;
            }

            if (!ExecutionDepthGuard.IsAllowed(context.Depth))
            {
                tracing.Trace(
                    "SanctionsCheckPlugin skipped: depth {0} is above the limit {1}.",
                    context.Depth,
                    ExecutionDepthGuard.MaxDepth);
                return;
            }

            var target = (Entity)context.InputParameters["Target"];

            // An Update that changes neither name column (for example our own review-status update)
            // is not a reason to screen again.
            if (isUpdate && !target.Contains(ContactFields.FirstName) && !target.Contains(ContactFields.LastName))
            {
                tracing.Trace("SanctionsCheckPlugin skipped: the update changes no name column.");
                return;
            }

            var contactId = isCreate ? PluginContextHelper.ResolveCreatedId(context, target) : target.Id;
            tracing.Trace("SanctionsCheckPlugin started. Contact {0}, depth {1}.", contactId, context.Depth);

            try
            {
                // null = SYSTEM user: screening must read the whole list and save the alert and the
                // review status whatever the privileges of the person editing the contact.
                var service = factory.CreateOrganizationService(null);
                new SanctionsHandler(service, tracing).Handle(contactId);
            }
            catch (Exception exception)
            {
                // Only a failure of the check itself throws; a match never does.
                tracing.Trace("SanctionsCheckPlugin failed: {0}", exception);
                throw new InvalidPluginExecutionException(
                    "The sanctions check failed. Details are in the Plugin Trace Log.", exception);
            }
        }
    }
}
