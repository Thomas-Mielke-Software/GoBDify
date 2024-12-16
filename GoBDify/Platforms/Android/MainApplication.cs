using Android.App;
using Android.OS;
using Android.Runtime;

namespace GoBDify
{
    [Application(UsesCleartextTraffic = true)]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        //private static bool CheckExternalStoragePermission()
        //{
        //    if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        //    {
        //        var result = Android.OS.Environment.IsExternalStorageManager;
        //        if (!result)
        //        {
        //            var manage = ActionManageAppAllFilesAccessPermission;
        //            Intent intent = new Intent(manage);
        //            Android.Net.Uri uri = Android.Net.Uri.Parse("package:" + AppInfo.Current.PackageName);
        //            intent.SetData(uri);
        //            Platform.CurrentActivity.StartActivity(intent);
        //        }
        //        return result;
        //    }

        //    return true;
        //}
    }
}
