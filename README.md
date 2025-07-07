# UtiliExtract

**UtiliExtract** is a Blazor WebAssembly application built to simplify the extraction of energy and utility consumption data from PDF bills. Designed specifically for British Columbia, Canada, this tool automates what is traditionally a manual and time-consuming task done by analysts — reading bills and inputting usage data into internal or third-party systems.

## Description

UtiliExtract provides an easy-to-use interface that allows users to upload utility bills in PDF format. The system automatically parses and extracts critical information such as:

* Account number
* Account Name
* Billing period and billing date
* Usage (e.g., kWh, GJ, m³)
* Amount due
* Service address

Currently supported utility providers include:

* FortisBC Electricity
* FortisBC Gas (Direct Energy)
* BC Hydro
* Enmax
* Creative Energy
* City of Vancouver (Water)
* City of Williams Lake (Water)

This is a **front-end only** tool — there is no backend server component. All parsing and data extraction is handled in-browser. The output is structured as JSON and ready to be sent to internal or external APIs, depending on the user’s integration needs.

The primary goal is to enable companies to streamline consumption reporting processes and take advantage of sustainability programs offered by various government agencies.

---

## Deployment & Running Locally

### Prerequisites

* [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) installed
* A modern browser (Edge, Chrome, Firefox)

### Building and Running

To build and run UtiliExtract locally:

```bash
git clone https://github.com/alibahaloo/UtiliExtract.git
cd UtiliExtract
dotnet build
dotnet run
```

Open your browser and navigate to the URL given by Kestrel.

---

## Configuration & Extending

Adding support for additional bill types or providers is straightforward:

### Modify `BillMetadata.cs`

* Add new entries to the `BillProvider` enum
* Extend `ProviderUsageTypeMap` to include usage types for the new provider
* Map usage types to measurement units in `UsageTypeToUnitMap`
* Add provider-specific keywords to `BillTypeKeywords` to assist in detection

### Add a New Bill Reader

Create a new helper class in `UtiliExtract.Helpers` using the existing ones (`BcHydroBillReader`, `DirectEnergyBillReader`, etc.) as templates. Your reader should:

* Accept the full text of the PDF
* Extract fields into a `BillData` object
* Handle duration, usage, amount due, and account identification

---

## License

This project, **UtiliExtract**, is licensed under the [Apache License 2.0](LICENSE.txt).

You are free to use, modify, and distribute this software, provided that you:

* Include proper attribution to the original author.
* Retain the license and notice files in any redistribution.

See the [NOTICE](NOTICE.txt) file for attribution details.

---

© 2025 Ali Bahaloo