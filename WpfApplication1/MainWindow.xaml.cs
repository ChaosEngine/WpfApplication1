using MySql.Data.MySqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WpfApplication1
{
	/// <summary>
	/// Logika interakcji dla klasy MainWindow.xaml
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
			object _syncer;

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
		private Task tsk;

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
				int i;
				if (int.TryParse(((ListBoxItem)cmbConcurrentyCount.SelectedItem).Content.ToString(), out i))
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
			CancellationTokenSource token;
			if (_cancellationTokenSource.TryGetValue(btn.Name, out token))
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

		async Task<double> ComputeStuffAsync(CancellationToken token)
		{
			var tsk = Task.Run(() =>
			{
				var sum = 0.0;
				for (int i = 1; i < (400 * 1000 * 1000); i++)
				{
					if (token.IsCancellationRequested)
						return 0;
					sum += Math.Sqrt(i);
				}
				return sum;
			}, token);

			return await tsk;
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

		private void thisWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			_cancellationTokenSource.Values.ToList().ForEach(x =>
			{
				if (x != null) x.Dispose();
			});
		}

		private void cmbConcurrentyCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

				var sum = await ComputeStuffAsync(GetCanellationtokenForButton((Button)sender).Token);

				OutputResultControl.Text = $"Sqrt sum = {sum}";
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
						using (var client = new HttpClient())
						{
							var stackContent = await client.GetAsync(url, token);
							var response = await stackContent.Content.ReadAsStringAsync();
							return int.Parse(response);
						}
					}, token);
				}
				//wait for all threads to complete
				await Task.WhenAll(all_tasks).ContinueWith((all_results) =>
				{
					watch.Stop();
					this.Dispatcher.Invoke(new Action(() =>//run something on UI thread
					{
						string comm = "";
						txtDebug.AppendText(Environment.NewLine);
						foreach (var res in all_results.Result)
						{
							txtDebug.AppendText(comm + res);
							comm = ",";
						}
						txtDebug.AppendText($"{Environment.NewLine}ElapsedMilliseconds: {watch.ElapsedMilliseconds}");
					}));

					watch.Reset();
					foreach (Task<int> tsk in all_tasks) tsk.Dispose();
					all_tasks = null;
				}, token);
				//await Task.Delay(250).ConfigureAwait(false);
				run_times++;
			}

			this.Dispatcher.Invoke(new Action(() =>//run something on UI thread
			{
				txtDebug.AppendText($"{Environment.NewLine}----------{Environment.NewLine}Bottom line summary: {global_watch.ElapsedMilliseconds}"
					+ $" run {run_times} times");
			}));
		}

		private async void btnAsyncWebTest_Click(object sender, EventArgs e)
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
}