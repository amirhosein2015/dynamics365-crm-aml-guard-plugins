# 2-minute demo script (screen recording)

Goal: show that the idea is clear in one look, that it works, and that the code is built to be tested.
Keep the README open in one window and a terminal in the project folder in another.

## 0:00 – 0:20 · The idea (README, diagram)

Screen: top of the README, the architecture diagram.

> "This is AML Guard, two plug-ins for a Dynamics 365 CRM. A save in Dataverse triggers a thin plug-in, the plug-in calls a handler, and the actual rules are plain C# classes with no SDK dependency. The result is an alert. All data is fictional."

## 0:20 – 0:30 · Start the demo

Screen: terminal.

```powershell
dotnet run --project src\AmlGuard.Demo
```

> "I run the real plug-in classes against an in-memory Dataverse, because I do not have a free Dataverse environment. I will say more about that at the end."

## 0:30 – 1:10 · Split deposits

Screen: scroll to sections 2 and 3 of the output.

> "Maria deposits 4,000, then 3,500. Each is below the 10,000 reporting threshold, so nothing happens. The third deposit of 3,200 brings the total to 10,700 inside 24 hours, and the plug-in raises one High alert. A fourth deposit does not raise a second alert: the rule sees the existing one. The thresholds are read from a settings table, not hard-coded."

## 1:10 – 1:40 · Sanctions

Screen: sections 5, 6 and 7.

> "A contact is created as 'JOSÉ martinez' with an accent, capital letters and extra spaces. After normalization it equals 'Jose Martinez' on the list, so the contact is set to Under Review and a Critical alert is created. Notice the plug-in does not throw: an exception would roll back the transaction and the alert with it. The second case is an Update: a clean contact is renamed to a listed name and is caught the same way."

## 1:40 – 2:00 · Quality and honesty

Screen: GitHub Actions tab, then `docs/decisions.md`.

> "There are 127 tests, run on every push by GitHub Actions, including one that fails if the domain code ever references the Dataverse SDK. The one thing I could not do is register the plug-ins in a live environment, because Microsoft no longer creates free developer environments. The README says so, and the registration steps are documented."
