# MSBuild Editor Privacy Statement

The MSBuild Editor extension for Visual Studio has telemetry that sends anonymized error traces and usage information to the publisher of the extension. This information is used to help improve the extension.

## How do I opt out?

Telemetry is enabled by default but you can opt out. To opt out, open the _Tools > Options_ dialog and go to the _MSBuild Editor > Telemetry_ panel, then uncheck the "Enable extension telemetry when Visual Studio telemetry is enabled" option.

MSBuild Editor telemetry is automatically disabled if Visual Studio telemetry is disabled.

## What data is collected?

### Anonymous User ID and Machine ID

A User ID and Machine ID are sent to identify how many users are impacted by errors and how many users use particular features. This helps maximize the impact of bug fixes and feature development.

### Environment Information

Environment information is collected to help diagnose problems that are specific to certain
environments:

* MSBuild Editor extension version
* Visual Studio version
* Operating system name, version and architecture
* .NET version

## How is the data anonymized?

Best efforts are made to ensure that telemetry data does not contain any user
identifiable information:

* Anonymization is performed locally on the user's machine, so that all data is already anonymized before being sent and stored via telemetry
* The User ID and Machine ID are generated randomly and stored in the Windows `%AppData%` and `%LocalAppData%` directories respectively
* The environment information that is collected is limited in scope so that it cannot be used as a user fingerprint
* When telemetry messages include file paths and other strings that are potentially user-identifiable, they are anonymized by hashing using a SHA256 hashing algorithm. These hashes allow aggregation for diagnostic purposes, such as identifying error messages that refer to the same file.

All telemetry code is available for review in the extension's [GitHub repository](https://github.com/mhutch/MSBuildEditor). If any user identifiable information is found to have been accidentally transmitted as result of a bug, it will be manually deleted.

## Where is the data stored?

Data is stored in Microsoft Azure App Insights. Access is protected via two factor authentication. As the data is pre-anonymized, there is minimal risk if the data is breached.

## Who has access to the data?

The raw telemetry data is only accessibly by [Mikayla Hutchinson](https://github.com/mhutch), the primary author of the extension.

Error traces and statistical information obtained from telemetry may be shared openly. For example, error traces or statistics about how many users have user a particular feature may be shared in GitHub Issues in the extension's GitHub repository. Information will be manually reviewed before sharing to ensure it does not contain any accidental leak of user-identifiable information due to bugs in anonymization.
