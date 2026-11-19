using System;
using Microsoft.Xrm.Sdk;

namespace AmlGuard.Plugins
{
    internal static class PluginContextHelper
    {
        // In PostOperation of Create the record exists and its id is known. Code that needs the id
        // treats a missing one as an error rather than guessing around it.
        public static Guid ResolveCreatedId(IPluginExecutionContext context, Entity target)
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
                "The created record has no id. Register the step as PostOperation (stage 40).");
        }

        public static bool HasEntityTarget(IPluginExecutionContext context)
        {
            return context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity;
        }
    }
}
