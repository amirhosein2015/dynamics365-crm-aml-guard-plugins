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
