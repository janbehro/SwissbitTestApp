using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using AndroidX.AppCompat.App;
using Com.Secureflashcard.Wormapi;
using Com.Sunmi.Peripheral.Printer;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Environment = Android.OS.Environment;

namespace DatapacTestApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private Button _buttonPrinterInit;
        private Button _buttonPrint;
        private Button _buttonInitSwissbit;
        private Button _buttonTestSwissbitWrite;
        private Button _buttonTestSwissbitRead;
        private Button _buttonSwissbitDisponse;
        private EditText _cbcTextBox;
        private ISunmiPrinterService _sunmiPrinterService;
        private WormAccess _worm;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            _buttonPrinterInit = FindViewById<Button>(Resource.Id.buttonInitPrinter);
            _buttonPrint = FindViewById<Button>(Resource.Id.buttonPrintButton);
            _buttonInitSwissbit = FindViewById<Button>(Resource.Id.buttonSwissbitInit);
            _buttonTestSwissbitWrite = FindViewById<Button>(Resource.Id.buttonSwissbitWrite);
            _buttonTestSwissbitRead = FindViewById<Button>(Resource.Id.buttonSwissbitRead);
            _buttonSwissbitDisponse = FindViewById<Button>(Resource.Id.buttonSwissbitDispose);

            _cbcTextBox = FindViewById<EditText>(Resource.Id.textCbc);

            _buttonPrinterInit.Click += InitPrinter;
            _buttonPrint.Click += Print;
            _buttonInitSwissbit.Click += InitSwissbit;
            _buttonTestSwissbitWrite.Click += TestSwissbitWrite;
            _buttonTestSwissbitRead.Click += TestSwissbitRead;
            _buttonSwissbitDisponse.Click += DisposeSwissbit;
        }

        private void Log(string message, string actionName)
        {
            try
            {
                var logFile = Path.Combine(Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads).AbsolutePath, "DatapacTest.txt");
                File.AppendAllText(logFile, $"{DateTime.Now.ToString()} {actionName} {message}{System.Environment.NewLine}");
            }
            catch { }
        }

        private void DisposeSwissbit(object sender, EventArgs e)
        {
            try
            {
            _worm?.Exit();
            _worm?.Dispose();
            _buttonInitSwissbit.Enabled = true;
            _buttonSwissbitDisponse.Enabled = false;
            }
            catch (Exception ex)
            {
                var error = "Error: " + ex.Message;
                Toast.MakeText(this, error, ToastLength.Long).Show();
                Log(error, nameof(DisposeSwissbit));
            }
        }

        private void TestSwissbitRead(object sender, EventArgs e)
        {
            try
            {
                short[] data = new short[512];
                var result = _worm.DataRead(data, 0, 1);
                if (result != WORM_ERROR.WormErrorNoerror)
                {
                    throw new Exception(result.ToString());
                }

                var bytes = data.Select(d => Convert.ToByte(d)).ToArray();
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                Toast.MakeText(this, "Read OK: " + text, ToastLength.Long).Show();
            }
            catch (Exception ex)
            {
                var error = "Error: " + ex.Message;
                Toast.MakeText(this, error, ToastLength.Long).Show();
                Log(error, nameof(TestSwissbitRead));
            }
        }

        private void TestSwissbitWrite(object sender, EventArgs e)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes("Hello world from Datapac test apk.");
                short[] outBuffer = new short[512];
                var outBufferSize = new int[1];
                var cbcKey = GetCbcMacKey();
                var cbcMacArray = ComputeCbcMac(Is32Bit(_worm), data, data.Length, cbcKey);
                var result = _worm.DataTransact(data, data.Length, outBuffer, outBufferSize, cbcMacArray, cbcMacArray.Length);
                if (result != WORM_ERROR.WormErrorNoerror)
                {
                    throw new Exception(result.ToString());
                }
                Toast.MakeText(this, "Write OK", ToastLength.Long).Show();
            }
            catch (Exception ex)
            {
                var error = "Error: " + ex.Message;
                Toast.MakeText(this, error, ToastLength.Long).Show();
                Log(error, nameof(TestSwissbitWrite));
            }
        }

        private void InitSwissbit(object sender, EventArgs e)
        {
            try
            {
                _worm = new WormAccess();
                var res = _worm.Init(ApplicationContext);
                if (res != 0)
                {
                    var error = "Error to initialize swissbit card: " + res.ToString();
                    Toast.MakeText(this, error, ToastLength.Long).Show();
                    Log(error, nameof(InitSwissbit));
                    return;
                }
            }
            catch (Exception ex)
            {
                var error = "Error to initialize swissbit card: " + ex.Message;
                Toast.MakeText(this, error, ToastLength.Long).Show();
                Log(error, nameof(InitSwissbit));
                return;
            }

            _buttonInitSwissbit.Enabled = false;
            _buttonTestSwissbitRead.Enabled = true;
            _buttonTestSwissbitWrite.Enabled = true;
            _buttonSwissbitDisponse.Enabled = true;
        }

        private void Print(object sender, EventArgs e)
        {
            try
            {
                var text = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book. It has survived not only five centuries, but also the leap into electronic typesetting, remaining essentially unchanged. It was popularised in the 1960s with the release of Letraset sheets containing Lorem Ipsum passages, and more recently with desktop publishing software like Aldus PageMaker including versions of Lorem Ipsum.";
                _sunmiPrinterService.PrintTextWithFont(text, "", 20, new SunmiResultCallback((x) => Log(x, nameof(SunmiResultCallback))));
            }
            catch (Exception ex)
            {
                var error = "Error to print: " + ex.Message;
                Toast.MakeText(this, error, ToastLength.Long).Show();
                Log(error, nameof(Print));
            }
        }

        private void InitPrinter(object sender, EventArgs e)
        {
            try
            {
                var callback = new SunmiPrinterCallback((ISunmiPrinterService service) =>
                {
                    _sunmiPrinterService = service;
                    _buttonPrint.Enabled = true;
                });
                InnerPrinterManager.Instance.BindService(ApplicationContext, callback);
                _buttonPrinterInit.Enabled = false;
            }
            catch (Exception ex)
            {
                var error = "Error to init printer: " + ex.Message;
                Toast.MakeText(this, error, ToastLength.Long).Show();
                Log(error, nameof(InitPrinter));
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private byte[] GetMemoryStatus(WormAccess worm)
        {
            var memorystatus = new short[512];
            var result = worm.DataReadStatus(memorystatus, new int[] { 512 });
            return memorystatus.Select(d => Convert.ToByte(d)).ToArray();
        }
        public bool Is32Bit(WormAccess worm)
        {
            var memoryStatus = GetMemoryStatus(worm);
            var result = memoryStatus[29] == 1;
            return result;
        }

        private byte[] GetCbcMacKey()
        {
            try
            {
                var array = _cbcTextBox.Text.Split(new string[] { ", ", "," }, StringSplitOptions.RemoveEmptyEntries);
                if (array.Length != 16)
                {
                    throw new Exception("Length of CBC must be 16 bytes delimited by comma.");
                }
                return array.Select(x => Convert.ToByte(byte.Parse(x))).ToArray();
            }
            catch (Exception ex)
            {
                throw new Exception("Invalid CBC: " + ex.Message);
            }
        }

        private byte[] ComputeCbcMac(bool is32Bit, byte[] data, int length, byte[] key)
        {
            byte[] array = new byte[16];
            int num = 2;
            if (is32Bit)
            {
                num = 4;
            }
            byte[] array2 = new byte[(length + num + 15) / 16 * 16];
            byte[] array3 = new byte[array2.Length];
            if (data.Length == 0)
            {
                return null;
            }
            if (key.Length != 16)
            {
                return null;
            }
            Array.Copy(data, 0, array3, num, length);
            if (is32Bit)
            {
                array3[0] = (byte)(length >> 24);
                array3[1] = (byte)(length >> 16);
                array3[2] = (byte)(length >> 8);
                array3[3] = (byte)length;
            }
            else
            {
                array3[0] = (byte)(length >> 8);
                array3[1] = (byte)length;
            }
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = array;
                aes.Padding = PaddingMode.Zeros;
                aes.Mode = CipherMode.CBC;
                aes.CreateEncryptor(key, array).TransformBlock(array3, 0, array3.Length, array2, 0);
            }
            byte[] array4 = new byte[16];
            Array.Copy(array2, array2.Length - 16, array4, 0, 16);
            return array4;
        }
    }

    internal class SunmiPrinterCallback : InnerPrinterCallback
    {
        private readonly Action<ISunmiPrinterService> _action;

        public SunmiPrinterCallback(Action<ISunmiPrinterService> action)
        {
            _action = action;
        }

        protected override async void OnConnected(ISunmiPrinterService service)
        {
            service.PrinterInit(null);
            _action?.Invoke(service);
        }
        protected override void OnDisconnected() { }
    }

    internal class SunmiResultCallback : InnerResultCallback
    {
        private readonly Action<string> _log;

        public SunmiResultCallback(Action<string> log)
        {
            _log = log;
        }
        public override void OnPrintResult(int p0, string p1) => _log($"{p0}, {p1}");
        public override void OnRaiseException(int p0, string p1) => _log($"{p0}, {p1}");
        public override void OnReturnString(string p0) => _log($"{p0}");
        public override void OnRunResult(bool p0) { }
    }
}