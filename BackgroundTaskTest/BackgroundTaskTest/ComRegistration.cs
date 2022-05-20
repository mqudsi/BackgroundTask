// Copyright Mahmoud Al-Qudsi, 2022.
// Released under the MIT Public License. This notice must be kept intact.

using Serilog;
using System;
using System.Runtime.InteropServices;
using WinRT;

namespace BackgroundTaskTest
{
    // System.Runtime.InteropServices.RegistrationServices is not available in .NET Core
    public static class RegistrationServices
    {
        [DllImport("ole32.dll")]
        private static extern int CoRegisterClassObject(
          [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
          [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
          [MarshalAs(UnmanagedType.U4)] RegistrationClassContext dwClsContext,
          [MarshalAs(UnmanagedType.U4)] RegistrationConnectionType flags,
          out uint lpdwRegister);

        [DllImport("ole32.dll")]
        private static extern int CoRevokeClassObject(uint dwRegister);

        /// <summary>
        /// Start and register an <c>IClassFactory</c> for user type <typeparamref name="T"/> implementing COM interface
        /// <typeparamref name="TInterface"/>.
        ///
        /// Type <typeparamref name="TInterface"/> should be either a projected CsWinRt type or a locally-defined COM
        /// interop interface. Type <typeparamref name="T"/> should derive from <typeparamref name="TInterface"/>. See
        /// type parameter documentation for more info.
        /// </summary>
        /// <typeparam name="T">The managed type to create a class factory for. The type should be public, sealed,
        /// derive from <typeparamref name="TInterface"/>, and have a unique GUID attached to it.
        ///
        /// An example of a type implementing COM interface <c>IBackgroundTask</c>:
        ///
        /// <code>
        /// [ComVisible(true)]
        /// [ClassInterface(ClassInterfaceType.None)]
        /// [Guid("A8082001-73F7-4607-8521-60F03476E462")]
        /// [ComSourceInterfaces(typeof(IBackgroundTask))]
        /// public sealed class BackgroundTask : IBackgroundTask
        /// { ... }
        /// </code>
        ///
        /// </typeparam>
        /// <typeparam name="TInterface">The COM interface implemented by the native type. If this is not a project
        /// CsWinRt type, then it should be locally defined as an interop interface with its method declaration
        /// copied/converted from the C header files (or MSDN). As an example:
        ///
        /// <code>
        /// [ComImport]
        /// [Guid("00000001-0000-0000-C000-000000000046")]
        /// [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        /// internal interface IClassFactory
        /// {
        ///     [PreserveSig]
        ///     int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);
        ///
        ///     [PreserveSig]
        ///     int LockServer(bool fLock);
        /// }
        /// </code>
        /// </typeparam>
        /// <returns>An <see cref="IDisposable"/> wrapping the COM registration token. This should remain alive/undisposed
        /// as long as the application is running (or as long as you desire the <c>IClassFactory</c> to remain active and
        /// accepting requests). Disposing the handle revokes the COM registration.</returns>
        /// <exception cref="Exception"></exception>
        public static IDisposable Register<T, TInterface>()
        where T: TInterface, new()
        {
            // All registration is taken care of by the sparse MSIX package/installer project in Package.appxmanifest,
            // which is responsible for registering the COM CLSID in the registry along with the executable/arguments
            // required to start it. See Package.appxmanifest for the misssing piece of the puzzle.
            //
            // The only thing we have to do is create the class factory to handle instantiation requests for the
            // target type.

            var guid = typeof(T).GUID;
            if (CoRegisterClassObject(guid, new ClassFactory<T, TInterface>(),
                RegistrationClassContext.LocalServer, RegistrationConnectionType.MultipleUse, out var comToken) != 0)
            {
                Log.Error("Error registering COM class activator for type!");
                throw new Exception("Error registering COM class activator for type!");
            }

            Log.Information("Registered class factory for COM background task");
            var registration = new ComServerRegistration { RegistrationToken = comToken };
            return registration;
        }

        private class ClassFactory<T, TInterface> : IClassFactory
            where T : TInterface, new()
        {
            private static readonly Guid IUnknownGuid = new("00000000-0000-0000-C000-000000000046");
            private const int CLASS_E_NOAGGREGATION = -2147221232;
            private const int E_NOINTERFACE = -2147467262;
            private const int S_OK = 0;

            public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
            {
                Log.Information("ClassFactory: Creating instance of target COM type");

                ppvObject = IntPtr.Zero;

                if (pUnkOuter != IntPtr.Zero)
                {
                    Log.Fatal("Aggregate COM initialization detected!");
                    Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);
                }

                Log.Verbose("Requested instance of type {TypeGuid}", riid);
                if (riid == typeof(T).GUID || riid == IUnknownGuid)
                {
                    // Create the instance of the .NET object
                    ppvObject = MarshalInterface<TInterface>.FromManaged(new T());
                }
                else
                {
                    Log.Fatal("ClassFactory: instance does not implement expected type!");
                    Marshal.ThrowExceptionForHR(E_NOINTERFACE);
                }

                return S_OK;
            }

            public int LockServer(bool fLock)
            {
                return S_OK;
            }
        }

        private readonly struct ComServerRegistration : IDisposable
        {
            public uint RegistrationToken { get; init; }

            public void Dispose()
            {
                CoRevokeClassObject(RegistrationToken);
            }
        }

        [ComImport]
        [Guid("00000001-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IClassFactory
        {
            [PreserveSig]
            int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

            [PreserveSig]
            int LockServer(bool fLock);
        }

        /// <summary>
        /// REGCLS_*
        /// </summary>
        [Flags]
        public enum RegistrationConnectionType : uint
        {
            SingleUse = 0,
            MultipleUse = 1,
            MultiSeparate = 2,
            Suspended = 4,
            Surrogate = 8,
            Agile = 0x10,
        }

        public enum RegistrationClassContext : uint
        {
            /// <summary>
            /// Disables activate-as-activator (AAA) activations for this activation only.
            /// </summary>
            DisableActivateAsActivator = 32768,

            /// <summary>
            /// Enables activate-as-activator (AAA) activations for this activation only.
            /// </summary>
            EnableActivateAsActivator = 65536,

            /// <summary>
            /// Allows the downloading of code from the Directory Service or the Internet.
            /// </summary>
            EnableCodeDownload = 8192,

            /// <summary>
            /// Begin this activation from the default context of the current apartment.
            /// </summary>
            FromDefaultContext = 131072,

            /// <summary>
            /// The code that manages objects of this class is an in-process handler.
            /// </summary>
            InProcessHandler = 2,

            /// <summary>
            /// Not used.
            /// </summary>
            InProcessHandler16 = 32,

            /// <summary>
            /// The code that creates and manages objects of this class is a DLL that runs in the same process as the caller of the function specifying the class context.
            /// </summary>
            InProcessServer = 1,

            /// <summary>
            /// Not used.
            /// </summary>
            InProcessServer16 = 8,

            /// <summary>
            /// The EXE code that creates and manages objects of this class runs on same machine but is loaded in a separate process space.
            /// </summary>
            LocalServer = 4,

            /// <summary>
            /// Disallows the downloading of code from the Directory Service or the Internet.
            /// </summary>
            NoCodeDownload = 1024,

            /// <summary>
            /// Specifies whether activation fails if it uses custom marshaling.
            /// </summary>
            NoCustomMarshal = 4096,

            /// <summary>
            /// Overrides the logging of failures.
            /// </summary>
            NoFailureLog = 16384,

            /// <summary>
            /// A remote machine context.
            /// </summary>
            RemoteServer = 16,
        }
    }
}
