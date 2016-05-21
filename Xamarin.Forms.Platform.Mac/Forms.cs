using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms.Internals;
using AppKit;
using CoreFoundation;
using Foundation;
using Xamarin.Forms.Platform.Mac;

namespace Xamarin.Forms
{
	public static class Forms
	{
		//Preserve GetCallingAssembly
		static readonly bool nevertrue = false;

		static Forms()
		{
			if (nevertrue)
				Assembly.GetCallingAssembly();
		}

		public static bool IsInitialized { get; private set; }


		public static void Init()
		{
			if (IsInitialized)
				return;
			IsInitialized = true;
			Color.Accent = Color.FromRgba(50, 79, 133, 255);

			Log.Listeners.Add(new DelegateLogListener((c, m) => Trace.WriteLine(m, c)));

			Device.OS = TargetPlatform.Mac;
			Device.PlatformServices = new MacPlatformServices();
			Device.Info = new MacDeviceInfo();

			//Registrar.RegisterAll(new[] { typeof(ExportRendererAttribute), typeof(ExportCellAttribute), typeof(ExportImageSourceHandlerAttribute) });
		}

		public static event EventHandler<ViewInitializedEventArgs> ViewInitialized;

		internal static void SendViewInitialized(this VisualElement self, NSView nativeView)
		{
			var viewInitialized = ViewInitialized;
			if (viewInitialized != null)
				viewInitialized(self, new ViewInitializedEventArgs { View = self, NativeView = nativeView });
		}

		internal class MacDeviceInfo : DeviceInfo
		{
			readonly Size _scaledScreenSize;
			readonly double _scalingFactor;

			public MacDeviceInfo()
			{
				_scalingFactor = 1.0;
				var dim = NSScreen.MainScreen.Frame;
				_scaledScreenSize = new Size(dim.Width, dim.Height);
				PixelScreenSize = new Size(_scaledScreenSize.Width * _scalingFactor, _scaledScreenSize.Height * _scalingFactor);
			}

			public override Size PixelScreenSize { get; }

			public override Size ScaledScreenSize
			{
				get { return _scaledScreenSize; }
			}

			public override double ScalingFactor
			{
				get { return _scalingFactor; }
			}
		}

		class MacPlatformServices : IPlatformServices
		{
			static readonly MD5CryptoServiceProvider Checksum = new MD5CryptoServiceProvider();

			public void BeginInvokeOnMainThread(Action action)
			{
				NSRunLoop.Main.BeginInvokeOnMainThread(action.Invoke);
			}

			public Ticker CreateTicker()
			{
				throw new NotImplementedException ();
			}

			public Assembly[] GetAssemblies()
			{
				return AppDomain.CurrentDomain.GetAssemblies();
			}

			public string GetMD5Hash(string input)
			{
				var bytes = Checksum.ComputeHash(Encoding.UTF8.GetBytes(input));
				var ret = new char[32];
				for (var i = 0; i < 16; i++)
				{
					ret[i * 2] = (char)Hex(bytes[i] >> 4);
					ret[i * 2 + 1] = (char)Hex(bytes[i] & 0xf);
				}
				return new string(ret);
			}

			public double GetNamedSize(NamedSize size, Type targetElementType, bool useOldSizes)
			{
				// We make these up anyway, so new sizes didn't really change
				// iOS docs say default button font size is 15, default label font size is 17 so we use those as the defaults.
				switch (size)
				{
					case NamedSize.Default:
						return typeof(Button).IsAssignableFrom(targetElementType) ? 15 : 17;
					case NamedSize.Micro:
						return 12;
					case NamedSize.Small:
						return 14;
					case NamedSize.Medium:
						return 17;
					case NamedSize.Large:
						return 22;
					default:
						throw new ArgumentOutOfRangeException("size");
				}
			}

			public async Task<Stream> GetStreamAsync(Uri uri, CancellationToken cancellationToken)
			{
				using (var client = GetHttpClient())
				using (var response = await client.GetAsync(uri, cancellationToken))
					return await response.Content.ReadAsStreamAsync();
			}

			public IIsolatedStorageFile GetUserStoreForApplication()
			{
				return new _IsolatedStorageFile(IsolatedStorageFile.GetUserStoreForApplication());
			}

			public bool IsInvokeRequired
			{
				get { return !NSThread.IsMain; }
			}

			public void OpenUriAction(Uri uri)
			{
				NSWorkspace.SharedWorkspace.OpenUrl (new NSUrl(uri.AbsoluteUri));
			}

			public void StartTimer(TimeSpan interval, Func<bool> callback)
			{
				NSTimer timer = null;
				timer = NSTimer.CreateRepeatingScheduledTimer(interval, t =>
				{
					if (!callback())
						t.Invalidate();
				});
				NSRunLoop.Main.AddTimer(timer, NSRunLoopMode.Common);
			}

			HttpClient GetHttpClient()
			{
				var proxy = CoreFoundation.CFNetwork.GetSystemProxySettings();
				var handler = new HttpClientHandler();
				if (!string.IsNullOrEmpty(proxy.HTTPProxy))
				{
					handler.Proxy = CoreFoundation.CFNetwork.GetDefaultProxy();
					handler.UseProxy = true;
				}
				return new HttpClient(handler);
			}

			static int Hex(int v)
			{
				if (v < 10)
					return '0' + v;
				return 'a' + v - 10;
			}

			public class _Timer : ITimer
			{
				readonly Timer _timer;

				public _Timer(Timer timer)
				{
					_timer = timer;
				}

				public void Change(int dueTime, int period)
				{
					_timer.Change(dueTime, period);
				}

				public void Change(long dueTime, long period)
				{
					_timer.Change(dueTime, period);
				}

				public void Change(TimeSpan dueTime, TimeSpan period)
				{
					_timer.Change(dueTime, period);
				}

				public void Change(uint dueTime, uint period)
				{
					_timer.Change(dueTime, period);
				}
			}

			public class _IsolatedStorageFile : IIsolatedStorageFile
			{
				readonly IsolatedStorageFile _isolatedStorageFile;

				public _IsolatedStorageFile(IsolatedStorageFile isolatedStorageFile)
				{
					_isolatedStorageFile = isolatedStorageFile;
				}

				public Task CreateDirectoryAsync(string path)
				{
					_isolatedStorageFile.CreateDirectory(path);
					return Task.FromResult(true);
				}

				public Task<bool> GetDirectoryExistsAsync(string path)
				{
					return Task.FromResult(_isolatedStorageFile.DirectoryExists(path));
				}

				public Task<bool> GetFileExistsAsync(string path)
				{
					return Task.FromResult(_isolatedStorageFile.FileExists(path));
				}

				public Task<DateTimeOffset> GetLastWriteTimeAsync(string path)
				{
					return Task.FromResult(_isolatedStorageFile.GetLastWriteTime(path));
				}

				public Task<Stream> OpenFileAsync(string path, FileMode mode, FileAccess access)
				{
					Stream stream = _isolatedStorageFile.OpenFile(path, (System.IO.FileMode)mode, (System.IO.FileAccess)access);
					return Task.FromResult(stream);
				}

				public Task<Stream> OpenFileAsync(string path, FileMode mode, FileAccess access, FileShare share)
				{
					Stream stream = _isolatedStorageFile.OpenFile(path, (System.IO.FileMode)mode, (System.IO.FileAccess)access, (System.IO.FileShare)share);
					return Task.FromResult(stream);
				}
			}
		}
	}
}