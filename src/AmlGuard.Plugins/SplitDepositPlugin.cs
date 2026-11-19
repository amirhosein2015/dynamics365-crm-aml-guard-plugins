using System;
using AmlGuard.Plugins.Handlers;
using AmlGuard.Plugins.Schema;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Plugins
{
    // Registration: message Create, table aml_transaction, stage PostOperation (40), synchronous.
    // Thin wrapper: it only unpacks the execution context and hands over to SplitDepositHandler.
    public sealed class SplitDepositPlugin : IPlugin
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

            if (!IsTransactionCreate(context))
            {
                tracing.Trace("SplitDepositPlugin ignored a call that is not Create on aml_transaction.");
                return;
            }

            var target = (Entity)context.InputParameters["Target"];
            var transactionId = ResolveCreatedId(context, target);
            tracing.Trace("SplitDepositPlugin started. Transaction {0}, depth {1}.", transactionId, context.Depth);

            try
            {
                // null = run as the SYSTEM user: the rule must see every deposit of the customer, not only
                // the ones the current user may read, and the alert must be saved whoever made the deposit.
                var service = factory.CreateOrganizationService(null);
                new SplitDepositHandler(service, tracing).Handle(target, transactionId);
            }
            catch (Exception exception)
            {
                // Fail closed: if the AML check cannot run, the deposit is not saved unchecked.
                tracing.Trace("SplitDepositPlugin failed: {0}", exception);
                throw new InvalidPluginExecutionException(
                    "The AML split-deposit check failed. Details are in the Plugin Trace Log.", exception);
            }
        }

        private static bool IsTransactionCreate(IPluginExecutionContext context)
        {
            return string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.PrimaryEntityName, TransactionFields.LogicalName, StringComparison.Ordinal)
                && context.InputParameters.Contains("Target")
                && context.InputParameters["Target"] is Entity;
        }

        // In PostOperation the record exists and its id is known. The id lets the rule avoid counting the
        // new deposit twice, so a missing id is an error rather than something to guess around.
        private static Guid ResolveCreatedId(IPluginExecutionContext context, Entity target)
        {
            if (target.Id != Guid.Empty)
            {
                return target.Id;
            }

            if (context.OutputParameters.Contains("id") && context.OutputParameters["id"] is Guid)
            {
                return (Guid)context.OutputParameters["id"];
            }

            throw new InvalidPluginExecutionException(
                "The created transaction has no id. Register the step as PostOperation (stage 40).");
        }
    }
}
