# Design decisions

Short notes on why things are built this way. Each one is meant to be defensible in a review.

## D1. One signed plug-in assembly, no separate core library

Dataverse plug-in assemblies must target .NET Framework, and a stand-alone assembly must be
strong-name signed before it is registered. If the business logic lived in a second assembly, that
would become a dependent assembly and force the plug-in package route (NuGet-style packaging).
Keeping everything in one assembly avoids that. The logic stays testable because it lives in plain
classes in a `Domain` namespace that never references the Dataverse SDK; an architecture test will
enforce that rule.

## D2. Schema names live in one file

Logical names and choice values are constants in `Schema/AmlSchema.cs`. A typo becomes a compile
error instead of a runtime "attribute does not exist" error, and a portal rename is a one-file change.

## D3. Fixed choice values

Choice columns use explicit values (100000000 and up) rather than whatever the publisher prefix
generates. The code and the tests do not depend on the environment's publisher.

## D4. Test project targets net48

The plug-in project targets net462 (the documented plug-in target). The test project targets net48
so it can reference that project directly, and runs on any Windows machine with Visual Studio.

## D5. Split-deposit counting rules

The evaluator works on a window that ends at the new deposit's time and starts `WindowHours` earlier,
both ends included. Only deposits strictly below the threshold count, and only those count toward
the total, so one large deposit cannot "complete" a pattern. The alert fires when the qualifying
count reaches `MinDepositCount` and the qualifying total reaches the threshold (equal counts).
These edge choices are written down because auditors ask about them, and each one has a test.
A deposit that is itself at or above the threshold is out of scope here: a bank reports those
directly, this rule only looks for amounts kept just under the line.

## D6. Duplicate alerts: compare against the latest alert, not a flag

The evaluator receives the creation time of the customer's most recent split-deposit alert and skips
the new alert if it falls inside the current window. No extra "already alerted" column is needed
and the rule stays a pure function of its inputs. Trade-off: if the window slides forward, a new
alert is allowed once the old one is older than the window, which is the intended behaviour.

## D7. Exact name matching with normalization, no fuzzy matching

Names are compared after trimming, collapsing whitespace, lower-casing and removing diacritics
(Unicode decomposition, then dropping combining marks). The same function is applied to the sanctions
list and to the contact, so both sides always agree. It is deliberately not fuzzy: fuzzy matching
creates false positives that need a human anyway and is a separate design problem.
Known limits, covered by tests that document them: letters with no decomposition (such as the Polish
L-with-stroke) are kept as they are, and a reversed word order does not match.

## D8. Bad input must never block a save

`NameNormalizer` catches the exception .NET throws on malformed Unicode and falls back to the raw
text. A synchronous PostOperation plug-in that throws rolls back the user's save, so a strange
character in a name must not be able to do that.

## D9. The architecture test is a tripwire, not a proof

`ArchitectureTests` fails if a `Domain` type declares, stores or holds a local of a Dataverse SDK
type. It cannot see an SDK type that is only used inside a method call, so code review still matters.

## D10. Three layers: plug-in, handler, repository

`SplitDepositPlugin` only unpacks the execution context. `SplitDepositHandler` reads the inputs and writes
the alert. `AmlRepository` holds every query. The decision itself lives in `SplitDepositEvaluator`
(Domain). Each layer has one reason to change, and only the Domain layer needs unit tests without any
Dataverse. The handler and plug-in are covered by integration tests on an in-memory Dataverse.

## D11. The plug-in runs its queries as the SYSTEM user

The plug-in calls `CreateOrganizationService(null)`. Microsoft documents `null` as "run as SYSTEM", while
`context.UserId` runs as the user who triggered the plug-in. A structuring check has to see every deposit
of the customer, even those made by tellers in other business units, and the alert has to be saved no matter
who made the deposit. A normal user's read scope could hide deposits and silently weaken the rule.
Trade-off: the alert is created by SYSTEM, so `createdby` does not show the teller; the transaction
itself still does.

## D12. Fail closed for the split-deposit check

If the check cannot run (for example a query fails), the plug-in traces the error and throws an
`InvalidPluginExecutionException`, so the deposit is not saved unchecked. This is the opposite of D8 on
purpose: bad data in a name must not block a save, but a broken compliance control should not be
silently skipped. If the business prefers availability over control, the catch block is the one place to change.

## D13. Bad configuration falls back to defaults

If `aml_settings` has no row, or a value is missing or invalid (for example a threshold of 0), the handler
traces a message and uses the documented defaults. A typo in a settings row must not stop all deposits.
Trade-off: a wrong setting can go unnoticed, which is why the trace line names the bad value.

## D14. Duplicate alerts: any alert of the same type in the window counts, open or closed

An analyst closing an alert should not make the next deposit raise a second alert for the same pattern.
The check uses `createdon`, which Dataverse always sets. An alert without it is treated as current
(skip one alert rather than create a duplicate).

## D15. Integration tests start the plug-in with `ExecutePluginWithTarget`

The new deposit is saved to the in-memory Dataverse first and then passed as `Target`, like PostOperation.
This avoids depending on step registration and automatic pipeline triggering of the test library, which
is a small young project. Registering the step in the portal is covered by `docs/registration.md`.

## D16. Sanctions match: flag and alert, never throw

On a match the plug-in sets `aml_reviewstatus = Under Review` and creates a Critical alert. It does not
throw: an `InvalidPluginExecutionException` in a synchronous step rolls back the whole transaction, which
would also undo the alert that documents the match. The contact is saved and a human reviews it.
Only a failure of the check itself throws (fail closed, same reasoning as D12).

## D17. Three layers against infinite loops

The plug-in updates the contact itself, which could re-trigger it. (1) The Update step is registered with
filtering attributes `firstname` and `lastname`, and the plug-in only writes `aml_reviewstatus`.
(2) In code, an Update whose Target has no name column is skipped, which is a test case of its own, in case
someone registers the step without filtering. (3) `ExecutionDepthGuard` stops chains deeper than 2.
Depth 1 is the user and depth 2 is automation started by that save, so both are still screened; a deeper
chain is almost certainly a loop. Dataverse itself stops at depth 8.

## D18. Retrieve the contact instead of using a pre-image

On Update the Target holds only changed columns, so one name part may be missing. A pre/post image would
fix that but needs extra registration in the Plugin Registration Tool, which is easy to forget. One
`Retrieve` in PostOperation returns the saved name and keeps the registration to two simple steps.
Trade-off: one extra read per screened save.

## D19. A cleared review flag is never reset by the plug-in

If a name is edited to something that no longer matches, `aml_reviewstatus` stays as it is. Clearing a
compliance flag is a decision for an analyst, not for a plug-in; an automatic reset would let someone
launder a flag by editing and re-editing a name.

## D20. One open alert per contact; the trace log holds ids, not names

Saving the name again while an open sanctions alert exists does not create another one. Once the alert is
closed, a new screening can raise a new alert. Names are personal data, so the trace log records contact ids
only and the alert text carries the names, protected by the alert table's own security.
