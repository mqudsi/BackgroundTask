# `IBackgroundTask` and COM server registration for .NET Core and .NET 5+

This project contains a self-contained sample background task Windows App SDK implementation, registered and triggered via the new CLSID/COM-based `IBackgroundTask` approach available since Windows 10 19041.

The bulk of the code and the hardest part of actually deploying this isn't the background task itself but rather the associated COM server registration, required for Windows to activate and run an instance of your `IBackgroundTask` implementation. This is made difficult since .NET Core and .NET 5/6 dropped the `RegistrationServices` interop classes that could be used to create and start a class factory (`IClassFactory`) that would be listen for and fulfill requests for the instantiation of an associated type implementing a COM interface.

## Architecture and Layout

The core components of this project can be found in a few different places:

* The background task related logic (implementation, registration) can be found in [`BackgroundTaskTest/BackgroundTask.cs`](./BackgroundTaskTest/BackgroundTaskTest/BackgroundTask.cs),
* The .NET 5+ implementation of the old .NET Framework `RegistrationServices` class that starts a class factory for any managed type and the heart of this project can be found in [`BackgroundTaskTest/ComRegistration.cs`](./BackgroundTaskTest/BackgroundTaskTest/ComRegistration.cs),
* The code that detects when the executable is launched with the intent of starting a class factory to instantiate the `IBackgroundTask` impl is found in [`BackgroundTaskTest/Program.cs`](./BackgroundTaskTest/BackgroundTaskTest/Program.cs) - note that `DISABLE_XAML_GENERATED_MAIN` must be defined in the csproj for this code to be used!
* The registration of the background task COM server with the system is done via the MSIX installer and is declaratively defined in the [`Package.appxmanifest` in the packaging project](.%2FBackgroundTaskTest%2FBackgroundTaskTest%20%28Package%29%2FPackage.appxmanifest).

## License

This sample project was written by Mahmoud Al-Qudsi of NeoSmart Technologies and is released under the MIT public license.
