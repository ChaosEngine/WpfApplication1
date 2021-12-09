using MySql.Data.MySqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WpfApplication1
{
	/// <summary>
	/// Interaction logic for class MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		interface IResultPrinterControl
		{
			string Text { get; set; }

			void AppendText(string textData);
		}

		class ResultPrinter : IResultPrinterControl
		{
			dynamic _resultPrinterImplementator;
			readonly object _syncer;

			public ResultPrinter(dynamic resultPrinterImplementator)
			{
				_resultPrinterImplementator = resultPrinterImplementator;
				_syncer = new object();
			}

			public string Text
			{
				get
				{
					lock (_syncer)
					{
						return _resultPrinterImplementator.Text;
					}
				}
				set
				{
					lock (_syncer)
					{
						_resultPrinterImplementator.Text = value;
					}
				}
			}

			public void AppendText(string value)
			{
				lock (_syncer)
				{
					_resultPrinterImplementator.AppendText(value);
				}
			}
		}

		private Dictionary<string, CancellationTokenSource> _cancellationTokenSource;

		IResultPrinterControl OutputResultControl { get; set; }

		DatabaseTypeEnum SelectedDBaseType
		{
			get
			{
				if (rbDBaseMysql.IsChecked.GetValueOrDefault(false))
					return DatabaseTypeEnum.MYSQL;
				else if (rbDBasePostgres.IsChecked.GetValueOrDefault(false))
					return DatabaseTypeEnum.POSTGRES;
				else if (rbDBaseMssql.IsChecked.GetValueOrDefault(false))
					return DatabaseTypeEnum.MSSQL;
				else if (rbDBaseMssqlEntities.IsChecked.GetValueOrDefault(false))
					return DatabaseTypeEnum.MSSQL_ENTITIES;
				else if (rbDBaseOracle.IsChecked.GetValueOrDefault(false))
					return DatabaseTypeEnum.ORACLE;
				else
					throw new NotSupportedException("Bad DataBaseType");
			}
		}

		public SqlTesterBase.TestTypeEnum TestType
		{
			get
			{
				if (cmbTestType.SelectionBoxItem.ToString().ToUpper() == SqlTesterBase.TestTypeEnum.GROUPEDVIEW.ToString())
					return SqlTesterBase.TestTypeEnum.GROUPEDVIEW;
				else if (cmbTestType.SelectionBoxItem.ToString().ToUpper() == SqlTesterBase.TestTypeEnum.SEARCHER.ToString())
					return SqlTesterBase.TestTypeEnum.SEARCHER;
				else
					throw new NotSupportedException("Bad DataBaseType");
			}
		}

		public int? ConcurrencyCount
		{
			get
			{
				if (int.TryParse(((ListBoxItem)cmbConcurrentyCount.SelectedItem).Content.ToString(), out int i))
				{
					return i;
				}
				return null;
			}
		}

		public MainWindow()
		{
			InitializeComponent();

			OutputResultControl = new ResultPrinter(txtDebug);
			_cancellationTokenSource = new Dictionary<string, CancellationTokenSource>(4);
		}

		CancellationTokenSource GetCanellationtokenForButton(Button btn)
		{
			if (_cancellationTokenSource.TryGetValue(btn.Name, out CancellationTokenSource token))
				return token;
			else
			{
				token = new CancellationTokenSource();
				_cancellationTokenSource.Add(btn.Name, token);
				return token;
			}
		}

		async Task<StringBuilder> DoWorkAsync(SqlTester data, CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			var task = Task.Run(() =>
			{
				Dispatcher.Invoke(() =>
				{
					OutputResultControl.AppendText("...working with...." + data.DataBaseType.ToString() + Environment.NewLine);
				});

				if (token.IsCancellationRequested)
					return Task.FromResult(new StringBuilder("cancelled"));

				return data.Execute(token);
			});

			return await task;
		}

		// Three things to note in the signature: 
		//  - The method has an async modifier.  
		//  - The return type is Task or Task<T>. (See "Return Types" section.)
		//    Here, it is Task<int> because the return statement returns an integer. 
		//  - The method name ends in "Async."
		async Task<StringBuilder> AccessTheWebAsync(CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			// You need to add a reference to System.Net.Http to declare client.
			var client = new HttpClient();

			// GetStringAsync returns a Task<string>. That means that when you await the 
			// task you'll get a string (urlContents).
			var tsk = client.GetAsync("http://msdn.microsoft.com", token);

			// You can do work here that doesn't rely on the string from GetStringAsync.
			OutputResultControl.AppendText("Working . . . . . . ." + Environment.NewLine);

			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			// The await operator suspends AccessTheWebAsync. 
			//  - AccessTheWebAsync can't continue until getStringTask is complete. 
			//  - Meanwhile, control returns to the caller of AccessTheWebAsync. 
			//  - Control resumes here when getStringTask is complete.  
			//  - The await operator then retrieves the string result from getStringTask. 
			var response = await tsk;
			string urlContents = await response.Content.ReadAsStringAsync();

			// The return statement specifies an integer result. 
			// Any methods that are awaiting AccessTheWebAsync retrieve the length value. 
			return new StringBuilder(urlContents);
		}

		async Task<double> ComputeStuffAsync(int? inputCount, CancellationToken token)
		{
			var tsk = Task.Run(() =>
			{
				var sum = 0.0;
				int DOP = 4;
				//var count = inputCount ?? 16;
				var count = inputCount ?? 4 * (100 * 1000 * 1000);
				long full_range = count / DOP;
				long reminder = count % DOP + 1;

				if (full_range > 0)
				{
					Parallel.For(0, DOP, new ParallelOptions { MaxDegreeOfParallelism = DOP, CancellationToken = token },
						// Initialize the local states
						() => 0.0,
						// Accumulate the thread-local computations in the loop body
						(th, loop, localState) =>
						{
							long from = full_range * (th);
							long to = full_range * (th + 1) - 1;

							var computed = calc(from, to, loop);

							//Dispatcher.Invoke(() =>
							//{
							//	OutputResultControl.AppendText($" th = {th}, from = {from}, to = {to}, computed = {computed} {Environment.NewLine}");
							//});

							return localState + computed;
						},
						// Combine all local states
						localState => Interlocked.Exchange(ref sum, sum + localState)
					);
				}
				if (reminder > 0)
				{
					long from = full_range * DOP;
					long to = from + reminder - 1;

					var computed = calcReminder(from, to);
					sum += computed;
				}

				return sum;
			}, token);

			return await tsk;

			//priv function
			double calc(long from, long to, ParallelLoopState loop)
			{
				double sum = 0;
				for (long i = from; i <= to && !loop.ShouldExitCurrentIteration; i++)
				{
					sum += Math.Sqrt(i);
					//sum += Approximate.Sqrt(i);
					//sum += Approximate.Isqrt(i);
				}

				Dispatcher.Invoke(() =>
				{
					OutputResultControl.AppendText($" from = {from}, to = {to}, sum = {sum} {Environment.NewLine}");
				});

				return sum;
			}

			double calcReminder(long from, long to)
			{
				double sum = 0;
				for (long i = from; i <= to && !token.IsCancellationRequested; i++)
				{
					sum += Math.Sqrt(i);
					//sum += Approximate.Sqrt(i);
					//sum += Approximate.Isqrt(i);
				}

				Dispatcher.Invoke(() =>
				{
					OutputResultControl.AppendText($" from = {from}, to = {to}, sum = {sum} {Environment.NewLine}");
				});

				return sum;
			}
		}

		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			if (((Button)sender).Content.ToString() == "Cancel")
			{
				GetCanellationtokenForButton((Button)sender).Cancel();
				return;
			}

			var prev = ((Button)sender).Content;
			try
			{
				OutputResultControl.Text = "starting multipple concurrency test" + Environment.NewLine;
				var db_type = SelectedDBaseType;
				var tst_type = TestType;
				((Button)sender).Content = "Cancel";
				int concurrentyCount = ConcurrencyCount.GetValueOrDefault(1);

				Task.Factory.StartNew(() =>
				{
					Parallel.For(0, concurrentyCount, async (i) =>
					{
						try
						{
							var data = new SqlTester(db_type, tst_type);
							CancellationToken token;
							Dispatcher.Invoke(() =>
							{
								token = GetCanellationtokenForButton((Button)sender).Token;
								OutputResultControl.AppendText("starting test #" + i + " with " + data.DataBaseType.ToString() +
									Environment.NewLine);
							});

							var result = await data.Execute(token);

							// Display the result. All is ok
							Dispatcher.Invoke(() =>
						{
							OutputResultControl.Text = result.ToString();
						});
						}
						catch (Exception ex)
						{
							//Show error
							Dispatcher.Invoke(() =>
						{
							OutputResultControl.Text = "Error: " + ex.Message;
						});
						}
					});
					//All sub-task ended. Notify UI
					Dispatcher.Invoke(() =>
				{
					((Button)sender).Content = prev;
					var cts = GetCanellationtokenForButton((Button)sender);
					if (cts.IsCancellationRequested)
					{
						cts.Dispose();
						_cancellationTokenSource.Remove(((Button)sender).Name);
					}
				});
				});
			}
			catch (Exception ex)
			{
				//Show error
				OutputResultControl.Text = "Error: " + ex.Message;
			}
		}

		private async void Button_Click_2(object sender, RoutedEventArgs e)
		{
			if (((Button)sender).Content.ToString() == "Cancel")
			{
				GetCanellationtokenForButton((Button)sender).Cancel();
				return;
			}

			var prev = ((Button)sender).Content;
			try
			{
				Task<StringBuilder> async_task = DoWorkAsync(new SqlTester(SelectedDBaseType, TestType), GetCanellationtokenForButton((Button)sender).Token);
				OutputResultControl.Text = "starting await select";
				((Button)sender).Content = "Cancel";
				StringBuilder result = await async_task;

				// Display the result. All is ok
				OutputResultControl.Text = result.ToString();
			}
			catch (Exception ex)
			{
				//Show error
				OutputResultControl.Text = "Error: " + ex.Message;
			}
			finally
			{
				((Button)sender).Content = prev;

				var cts = GetCanellationtokenForButton((Button)sender);
				if (cts.IsCancellationRequested)
				{
					cts.Dispose();
					_cancellationTokenSource.Remove(((Button)sender).Name);
				}
			}
		}

		private async void AsyncWeb_Click(object sender, RoutedEventArgs e)
		{
			if (((Button)sender).Content.ToString() == "Cancel")
			{
				GetCanellationtokenForButton((Button)sender).Cancel();
				return;
			}

			var prev = ((Button)sender).Content;
			try
			{
				((Button)sender).Content = "Cancel";
				Task<StringBuilder> async_task = AccessTheWebAsync(GetCanellationtokenForButton((Button)sender).Token);
				OutputResultControl.Text = "starting AccessTheWebAsync!";

				StringBuilder contentLength = await async_task;

				OutputResultControl.Text = contentLength.ToString();
			}
			catch (Exception ex)
			{
				//Show error
				OutputResultControl.Text = "Error: " + ex.Message;
			}
			finally
			{
				((Button)sender).Content = prev;

				var cts = GetCanellationtokenForButton((Button)sender);
				if (cts.IsCancellationRequested)
				{
					cts.Dispose();
					_cancellationTokenSource.Remove(((Button)sender).Name);
				}
			}
		}

		private void Window_KeyUp_1(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Escape)
				Close();
		}

		private void OnWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			_cancellationTokenSource.Values.ToList().ForEach(x =>
			{
				if (x != null) x.Dispose();
			});
		}

		private void OnConcurrentyCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int? i = this.ConcurrencyCount;
			if (i.HasValue)
			{
				btnAsync.IsEnabled = i.Value <= 1;
			}
			else
				btnAsync.IsEnabled = false;
		}

		private async void SqrtLoad_Click(object sender, RoutedEventArgs e)
		{
			if (((Button)sender).Content.ToString() == "Cancel")
			{
				GetCanellationtokenForButton((Button)sender).Cancel();
				return;
			}

			var prev = ((Button)sender).Content;
			try
			{
				((Button)sender).Content = "Cancel";

				int.TryParse(OutputResultControl.Text, out int count);

				var sum = await ComputeStuffAsync(count > 0 ? (int?)count : null,
					GetCanellationtokenForButton((Button)sender).Token);

				OutputResultControl.AppendText($"Sqrt sum = {sum} {Environment.NewLine}");
			}
			catch (Exception ex)
			{
				//Show error
				OutputResultControl.Text = "Error: " + ex.Message;
			}
			finally
			{
				((Button)sender).Content = prev;

				var cts = GetCanellationtokenForButton((Button)sender);
				if (cts.IsCancellationRequested)
				{
					cts.Dispose();
					_cancellationTokenSource.Remove(((Button)sender).Name);
				}
			}
		}

		private async Task SyncAcynsWebProcessing(CancellationToken token, int seconds2Run = 60, int threadCount = 50)
		{
			Stopwatch watch = new Stopwatch(), global_watch = new Stopwatch();
			int run_times = 0;
			string url = txtDebug.Text.Trim().Split(new[] { '\r', '\n' }).FirstOrDefault();
			global_watch.Start();

			while (true)
			{
				if (!token.IsCancellationRequested &&
					global_watch.ElapsedMilliseconds > (seconds2Run * 1000))//do this procedure X number of seconds
					break;

				var all_tasks = new Task<int>[threadCount];
				watch.Reset();
				watch.Start();
				for (int i = 0; i < threadCount; i++)//create-and-run x tasks
				{
					all_tasks[i] = Task.Run(async () =>
					{
						try
						{
							using (var client = new HttpClient())
							{
								var stackContent = await client.GetAsync(url, token);
								var response = await stackContent.Content.ReadAsStringAsync();
								if (response.Length > 10)
								{
									return response.Length;
								}
								else
								{
									if (int.TryParse(response, out int j))
										return j;
									return -1;
								}
							}
						}
						catch (Exception ex)
						{
							this.Dispatcher.Invoke(() =>//run something on UI thread
							{
								txtDebug.AppendText(ex.ToString());
							});
							return -2;
						}
					}, token);
				}
				//wait for all threads to complete
				await Task.WhenAll(all_tasks).ContinueWith((all_results) =>
				{
					watch.Stop();
					this.Dispatcher.Invoke(() =>//run something on UI thread
					{
						string comm = "";
						txtDebug.AppendText(Environment.NewLine);
						foreach (var res in all_results.Result)
						{
							txtDebug.AppendText(comm + res);
							comm = ",";
						}
						txtDebug.AppendText($"{Environment.NewLine}ElapsedMilliseconds: {watch.ElapsedMilliseconds}");
					});

					watch.Reset();
					foreach (Task<int> tsk in all_tasks) tsk.Dispose();
					all_tasks = null;
				}, token);
				//await Task.Delay(250).ConfigureAwait(false);
				run_times++;
			}

			this.Dispatcher.Invoke(() =>//run something on UI thread
			{
				txtDebug.AppendText($"{Environment.NewLine}----------{Environment.NewLine}Bottom line summary: {global_watch.ElapsedMilliseconds}"
					+ $" run {run_times} times");
			});
		}

		private async void OnBtnAsyncWebTest_Click(object sender, EventArgs e)
		{
			if (((Button)sender).Content.ToString() == "Cancel")
			{
				GetCanellationtokenForButton((Button)sender).Cancel();
				return;
			}

			var prev = ((Button)sender).Content;
			try
			{
				((Button)sender).Content = "Cancel";

				await SyncAcynsWebProcessing(GetCanellationtokenForButton((Button)sender).Token);
			}
			catch (Exception ex)
			{
				//Show error
				OutputResultControl.Text = "Error: " + ex.Message;
			}
			finally
			{
				((Button)sender).Content = prev;

				var cts = GetCanellationtokenForButton((Button)sender);
				if (cts.IsCancellationRequested)
				{
					cts.Dispose();
					_cancellationTokenSource.Remove(((Button)sender).Name);
				}
			}
		}
	}

	public class Approximate
	{
		public static float Sqrt(float z)
		{
			if (z == 0) return 0;
			FloatIntUnion u;
			u.tmp = 0;
			u.f = z;
			u.tmp -= 1 << 23; /* Subtract 2^m. */
			u.tmp >>= 1; /* Divide by 2. */
			u.tmp += 1 << 29; /* Add ((b + 1) / 2) * 2^m. */
			return u.f;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct FloatIntUnion
		{
			[FieldOffset(0)]
			public float f;

			[FieldOffset(0)]
			public int tmp;
		}

		// Finds the integer square root of a positive number  
		public static long Isqrt(long num)
		{
			if (0 == num) { return 0; }  // Avoid zero divide  
			long n = (num / 2) + 1;       // Initial estimate, never low  
			long n1 = (n + (num / n)) / 2;
			while (n1 < n)
			{
				n = n1;
				n1 = (n + (num / n)) / 2;
			} // end while  
			return n;
		} // end Isqrt() 
	}
}