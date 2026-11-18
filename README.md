# AML Guard for Dynamics 365 CRM – Anti-Fraud Plugins

Anti-fraud and AML (anti-money-laundering) compliance plugins for Microsoft Dynamics 365 CRM / Dataverse, written in C#.

> **Educational project with fictional data.** It is not a real compliance product and must not be used to make real AML decisions.

## What it does

1. **Split deposit detection (structuring):** raises an alert when a customer makes several deposits that are each below the reporting threshold but together reach it within a time window.
2. **Sanctions list check:** compares a contact's name against a sanctioned-names table (exact match after normalization), flags the contact for review and raises a critical alert.

## Status

Work in progress. The full README (architecture, design decisions, how to run, limitations) will be added at the end of the project.
