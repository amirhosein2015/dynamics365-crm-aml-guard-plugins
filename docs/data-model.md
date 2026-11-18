# Data model

Publisher prefix: `aml`. Everything lives in one solution. All data is fictional.

Column logical names below match `src/AmlGuard.Plugins/Schema/AmlSchema.cs`.

## aml_transaction

| Column (logical name) | Type | Notes |
|---|---|---|
| aml_name | Text (primary) | Free label, e.g. "Deposit 9,500" |
| aml_customer | Lookup → contact | |
| aml_amount | Currency | |
| aml_transactiondate | Date and time | |
| aml_type | Choice | Deposit = 100000000, Withdrawal = 100000001 |

## aml_sanctionedname

| Column | Type | Notes |
|---|---|---|
| aml_name | Text (primary) | The sanctioned person's full name |
| aml_source | Text | Where the entry came from (fictional list) |

## aml_alert

| Column | Type | Notes |
|---|---|---|
| aml_name | Text (primary) | Short title |
| aml_type | Choice | Split Deposit = 100000000, Sanctions Match = 100000001 |
| aml_severity | Choice | Low = 100000000, Medium = 100000001, High = 100000002, Critical = 100000003 |
| aml_customer | Lookup → contact | |
| aml_description | Multiple lines of text | Explains why the alert was raised |
| aml_status | Choice | Open = 100000000, Closed = 100000001 |

## aml_settings

A single configuration row. The plug-ins read the three values from it and fall back to the
documented defaults only if no row exists.

| Column | Type | Default |
|---|---|---|
| aml_name | Text (primary) | "Default" |
| aml_reportingthreshold | Decimal number | 10000 |
| aml_mindepositcount | Whole number | 3 |
| aml_windowhours | Whole number | 24 |

## contact (existing table, one added column)

| Column | Type | Notes |
|---|---|---|
| aml_reviewstatus | Choice | Clear = 100000000, Under Review = 100000001 |

## Creating the choices in the portal

Dataverse generates choice values from the publisher's option-value prefix. When you create each
choice column, edit the values by hand so they match the numbers above. The code relies on them.
