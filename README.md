# CompuMaster.Dms
DMS Browser component for Scopevisio Teamwork, CenterDevice, WebDAV (e.g. NextCloud, OwnCloud)

[![Github Release](https://img.shields.io/github/release/CompuMasterGmbH/CompuMaster.Dms.svg?maxAge=2592000&label=GitHub%20Release)](https://github.com/CompuMasterGmbH/CompuMaster.Dms/releases) 
[![NuGet CompuMaster.Dms](https://img.shields.io/nuget/v/CompuMaster.Dms.Providers.svg?maxAge=2592000&label=NuGet%20CM.Dms.Providers)](https://www.nuget.org/packages/CompuMaster.Dms.Providers) 
[![NuGet CompuMaster.Dms.BrowserUI](https://img.shields.io/nuget/v/CompuMaster.Dms.BrowserUI.svg?maxAge=2592000&label=NuGet%20CM.Dms.BrowserUI)](https://www.nuget.org/packages/CompuMaster.Dms.BrowserUI) 

## Simple download/installation using NuGet
```powershell
Install-Package CompuMaster.Dms.Providers
```
respectively
```powershell
Install-Package CompuMaster.Dms.BrowserUI
```
Also see: https://www.nuget.org/packages/CompuMaster.Dms.Providers and https://www.nuget.org/packages/CompuMaster.Dms.BrowserUI

## Modules Overview

There are following main modules for your use:
* CompuMaster.Dms.Providers – The base library (compatible with .NET Standard/Core/Framework) for your own implementations to access your DMS systems (with build-in support for WebDAV (e.g. NextCloud, OwnCloud) and Scopevisio Teamwork (a flavored CenterDevice implementation)
* CompuMaster.Dms.BrowserUI – An implementation (currently in German only, commits for additional languages welcome!) of all required forms and dialogs with System.Windows.Forms (requires .NET Framework 4.8 or .NET 5.0-Windows) to 
  * download and upload files 
  * setup user sharings and link sharings (if supported by the underlying provider)
  * provide several levels of allowed actions depending on required action context (manage folder structure only without viewing files, view and edit folder structure and files, or view everything without editing, etc.)
* CompuMaster.Dms.TestDemo.WebDav – A demo application to show functionality of CompuMaster.Dms.BrowserUI components with a WebDAV server (based on System.Windows.Forms which requires .NET Framework 4.8 or .NET 5.0-Windows)
* CompuMaster.Dms.TestDemo.ScopevisioTeamwork – A demo application to show functionality of CompuMaster.Dms.BrowserUI components with Scopevisio Teamwork (based on System.Windows.Forms which requires .NET Framework 4.8 or .NET 5.0-Windows)

### WebDAV sharing capabilities

The WebDAV provider keeps generic WebDAV servers provider-neutral. For recognized Nextcloud and ownCloud personal-file endpoints, it probes the Open Collaboration Services (OCS) API and enables user, group, and public-link sharing only when that probe succeeds. OCS permission bits are mapped without silently changing their meaning; view and download remain coupled because the classic OCS bit field cannot represent modern Nextcloud download restrictions separately.

ownCloud Infinite Scale personal-file compatibility endpoints can use the OCS adapter. Space endpoints (`/dav/spaces/<space-id>`) remain regular WebDAV endpoints because their sharing model uses LibreGraph roles and item identifiers; a future LibreGraph adapter can add that capability without changing the common DMS sharing API.

## Screenshots

### Login form, customized for WebDAV
![image](https://user-images.githubusercontent.com/3033827/126822624-fd9a0b0b-6762-41d9-a1c6-7bb285fdebdd.png)

### Browser dialog window
![image](https://user-images.githubusercontent.com/3033827/126822834-e87c9897-1978-4f84-9fae-52e276daaf6d.png)

### Extended file properties windows
![image](https://user-images.githubusercontent.com/3033827/126822920-08a09683-a884-484b-a72d-724a7acfd41a.png)

### Sharing setup
Dialogs for sharing setup for internal users (user accounts) or external users (web links) depend on DMS provider
