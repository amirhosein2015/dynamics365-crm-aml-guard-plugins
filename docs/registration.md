# Registering the plug-ins (Plugin Registration Tool)

Only needed if you have a Dataverse environment. The automated tests do not need one.

## Split deposit plug-in

| Setting | Value |
|---|---|
| Assembly | `AmlGuard.Plugins.dll` (signed, isolation mode Sandbox, location Database) |
| Plug-in type | `AmlGuard.Plugins.SplitDepositPlugin` |
| Message | Create |
| Primary entity | `aml_transaction` |
| Filtering attributes | not applicable (Create) |
| Event pipeline stage | PostOperation (40) |
| Execution mode | Synchronous |
| Run in user's context | Calling User (the plug-in itself switches to SYSTEM for its queries, see D11) |

## Sanctions check plug-in

Register the same plug-in type twice (two steps), both on table `contact`:

| Setting | Step 1 | Step 2 |
|---|---|---|
| Plug-in type | `AmlGuard.Plugins.SanctionsCheckPlugin` | `AmlGuard.Plugins.SanctionsCheckPlugin` |
| Message | Create | Update |
| Filtering attributes | not applicable | `firstname`, `lastname` (only these two) |
| Event pipeline stage | PostOperation (40) | PostOperation (40) |
| Execution mode | Synchronous | Synchronous |

The filtering attributes on step 2 matter: the plug-in updates `aml_reviewstatus` itself, and without the
filter that update would call the plug-in again (see D17 for the extra safeguards in code).

## Reading the Plugin Trace Log

1. Power Platform admin center, select the environment, Settings, Plug-in trace log: set it to **All**
   (turn it back to Exception or Off afterwards; it stores data).
2. Create a deposit, then open the trace log records (Advanced find on *Plug-in Trace Logs*, or in the
   classic settings area).
3. The trace lines come from `ITracingService`: the outcome, count and total of the check, and the id of the
   created alert.

Status: not yet verified against a live environment. See the limitations in the README.
