using System;
using System.Threading.Tasks;
using Android.App;
using Android.Runtime;
using Android.Util;

namespace NigerianNewGrid
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        public override void OnCreate()
        {
            base.OnCreate();

            // Log unhandled exceptions from CLR
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log.Error("APP", $"AppDomain.UnhandledException: {ex}");
            };

            // Log Android environment exceptions coming from Java side
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                Log.Error("APP_DEBUG", $"AndroidEnvironment.UnhandledExceptionRaiser: {args.Exception}");
            };

            // Capture unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Log.Error("APP_DEBUG", $"UnobservedTaskException: {e.Exception}");
                e.SetObserved();
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
