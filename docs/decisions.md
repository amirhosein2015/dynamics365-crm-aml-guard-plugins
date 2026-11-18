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
