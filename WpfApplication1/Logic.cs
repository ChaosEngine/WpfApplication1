using MySql.Data.MySqlClient;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace WpfApplication1
{
	public enum DatabaseTypeEnum { MYSQL, POSTGRES, MSSQL, MSSQL_ENTITIES, ORACLE }

	public interface ISqlTestExecutor
	{
		Task<StringBuilder> Execute(CancellationToken token);
	}

	public abstract class SqlTesterBase : ISqlTestExecutor
	{
		public enum TestTypeEnum { GROUPEDVIEW, SEARCHER }

		protected string _sqlQuery { get; set; }

		public DatabaseTypeEnum DataBaseType { get; protected set; }

		public TestTypeEnum TestType { get; protected set; }

		public SqlTesterBase(DatabaseTypeEnum dataBaseType, TestTypeEnum testType)
		{
			DataBaseType = dataBaseType;
			TestType = testType;
		}

		protected abstract Task<StringBuilder> DoWorkMysql(SqlTester data, CancellationToken token);

		protected abstract Task<StringBuilder> DoWorkPostgres(SqlTester data, CancellationToken token);

		protected abstract Task<StringBuilder> DoWorkMssql(SqlTester data, CancellationToken token);

		protected abstract Task<StringBuilder> DoWorkEntities(SqlTester data, CancellationToken token);

		protected abstract Task<StringBuilder> DoWorkOracle(SqlTester data, CancellationToken token);

		protected abstract Task<StringBuilder> ExecuteGroupedView(CancellationToken token);

		protected abstract Task<StringBuilder> ExecuteSearcher(CancellationToken token);

		public async Task<StringBuilder> Execute(CancellationToken token)
		{
			switch (TestType)
			{
				case TestTypeEnum.GROUPEDVIEW:
					return await ExecuteGroupedView(token);
				case TestTypeEnum.SEARCHER:
					return await ExecuteSearcher(token);
				default:
					throw new NotSupportedException("bad test type");
			}
		}
	}

	/// <summary>
	/// Used to pass custom args into the worker function.
	/// </summary>
	public class SqlTester : SqlTesterBase
	{
		int? _initialSleep;
		Random _rnd;
		string _keyword2Search = null;
		int _maxCount = 3000;

		public SqlTester(DatabaseTypeEnum dataBaseType, TestTypeEnum testType)
			: base(dataBaseType, testType)
		{

			_rnd = new Random((int)DateTime.Now.Ticks);
			_initialSleep = null;
		}

		string GetRandomString(int size)
		{
			StringBuilder builder = new StringBuilder(size);
			char ch;
			for (int i = 0; i < size; i++)
			{
				ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * _rnd.NextDouble() + 65)));
				builder.Append(ch);
			}
			return builder.ToString();
		}

		protected override async Task<StringBuilder> ExecuteGroupedView(CancellationToken token)
		{
			StringBuilder result;
			Stopwatch timer = new Stopwatch();
			timer.Start();
			switch (DataBaseType)
			{
				case DatabaseTypeEnum.MYSQL:
					_sqlQuery = string.Concat("SELECT domain, cnt, now() FROM domains limit ", _maxCount);
					result = await this.DoWorkMysql(this, token);
					break;
				case DatabaseTypeEnum.POSTGRES:
					_sqlQuery = string.Concat("SELECT domain, cnt, now() FROM domains limit ", _maxCount);
					result = await this.DoWorkPostgres(this, token);
					break;
				case DatabaseTypeEnum.MSSQL:
					_sqlQuery = string.Concat("SELECT top ", _maxCount, " domain, cnt, getdate() FROM domains");
					result = await this.DoWorkMssql(this, token);
					break;
				case DatabaseTypeEnum.MSSQL_ENTITIES:
					result = await this.DoWorkEntitiesGroupedView(this, token);
					break;
				case DatabaseTypeEnum.ORACLE:
					_sqlQuery = string.Concat("SELECT domain, cnt, TO_CHAR(SYSDATE, 'MM-DD-YYYY HH24:MI:SS') now FROM domains where rownum < ", _maxCount);
					result = await this.DoWorkOracle(this, token);
					break;
				default:
					throw new NotSupportedException("Bad DataBaseType");
			}
			timer.Stop();
			result.Insert(0, string.Concat("elapsed ", timer.ElapsedMilliseconds, " milliseconds", Environment.NewLine));
			return result;
		}

		protected override async Task<StringBuilder> ExecuteSearcher(CancellationToken token)
		{
			StringBuilder result;
			Stopwatch timer = new Stopwatch();
			timer.Start();
			_keyword2Search = GetRandomString(3).ToLower();
			switch (DataBaseType)
			{
				case DatabaseTypeEnum.MYSQL:
					_sqlQuery = string.Concat(@"select user_name, clear_passwd, user_id, now() 
                                                from accounts
                                                where MATCH(clear_passwd) AGAINST('*", _keyword2Search, "*' IN BOOLEAN MODE)");
					result = await this.DoWorkMysql(this, token);
					break;
				case DatabaseTypeEnum.POSTGRES:
					_sqlQuery = string.Concat(@"select user_name, clear_passwd, cast(user_id as bigint), now()
                                                from accounts
                                                where clear_passwd like '%", _keyword2Search, "%'");
					result = await this.DoWorkPostgres(this, token);
					break;
				case DatabaseTypeEnum.MSSQL:
					_sqlQuery = string.Concat(@"select user_name, clear_passwd, cast(user_id as bigint), getdate() 
                                                from accounts 
                                                where clear_passwd like '%", _keyword2Search, "%'");
					result = await this.DoWorkMssql(this, token);
					break;
				case DatabaseTypeEnum.MSSQL_ENTITIES:
					result = await this.DoWorkEntitiesSearcher(this, token);
					break;
				case DatabaseTypeEnum.ORACLE:
					_sqlQuery = string.Concat(@"select user_name, clear_passwd, user_id, TO_CHAR(SYSDATE, 'MM-DD-YYYY HH24:MI:SS') now
                                                from accounts 
                                                where clear_passwd like '%", _keyword2Search, "%'");
					result = await this.DoWorkOracle(this, token);
					break;
				default:
					throw new NotSupportedException("Bad DataBaseType");
			}
			timer.Stop();
			result.Insert(0, string.Concat("for ", _keyword2Search, " elapsed ", timer.ElapsedMilliseconds, " millis",
				Environment.NewLine));
			return result;
		}

		[Obsolete]
		void ParseReaderSimple(IDataReader rdr, StringBuilder sb)
		{
			sb.Append(rdr.GetString(0)).Append(" | ")
				.Append(rdr.GetInt64(1)).Append(" | ")
				.Append(rdr.GetDateTime(2))
				.AppendLine();
		}

		void ParseReaderAll(IDataReader rdr, StringBuilder sb)
		{
			string comma = string.Empty;
			int count = rdr.FieldCount;
			for (int i = 0; i < count; i++)
			{
				sb.Append(comma)
					.Append(rdr[i].ToString());
				comma = " | ";
			}
			sb.AppendLine();
		}

		void ProcessReader(IDataReader rdr, StringBuilder sb)
		{
			//uint i = 0;
			while (rdr.Read())
			{
				ParseReaderAll(rdr, sb);
				//i++;
			}
			//sb.Append("Got ").Append(i).Append(" rows");
		}

		protected override async Task<StringBuilder> DoWorkMysql(SqlTester data, CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			if (data._initialSleep.HasValue)
				await Task.Delay(data._initialSleep.Value);

			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			using (var conn = new MySqlConnection(ConfigurationManager.ConnectionStrings["Mysql"].ConnectionString))
			{
				await conn.OpenAsync(token);

				var sb = new StringBuilder(100000);
				using (MySqlCommand cmd = new MySqlCommand(data._sqlQuery, conn))
				{
                    await cmd.PrepareAsync();
					cmd.CommandType = System.Data.CommandType.Text;

					if (token.IsCancellationRequested)
						return new StringBuilder("cancelled");

					using (var rdr = await cmd.ExecuteReaderAsync(token))
					{
						ProcessReader(rdr, sb);
						return sb;
					}
				}
			}
		}

		protected override async Task<StringBuilder> DoWorkPostgres(SqlTester data, CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			if (data._initialSleep.HasValue)
				await Task.Delay(data._initialSleep.Value);

			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			//string clientCertPath = @"PostgresqlClientCert\postgresql.pfx";
			using (var conn = new NpgsqlConnection(ConfigurationManager.ConnectionStrings["Postgres"].ConnectionString))
			{
				conn.ProvideClientCertificatesCallback = MyProvideClientCertificatesCallback;

				await conn.OpenAsync(token);

				var sb = new StringBuilder(1000);
				using (var cmd = new NpgsqlCommand(data._sqlQuery, conn))
				{
                    await cmd.PrepareAsync();
					cmd.CommandType = System.Data.CommandType.Text;

					if (token.IsCancellationRequested)
						return new StringBuilder("cancelled");

					using (var rdr = await cmd.ExecuteReaderAsync(token))
					{
						ProcessReader(rdr, sb);
						return sb;
					}
				}
			}

			void MyProvideClientCertificatesCallback(X509CertificateCollection clientCerts)
			{
				using (X509Store store = new X509Store(StoreLocation.CurrentUser))
				{
					store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

					var currentCerts = store.Certificates;
					currentCerts = currentCerts.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
					currentCerts = currentCerts.Find(X509FindType.FindByIssuerName, "theBrain.ca", false);
					currentCerts = currentCerts.Find(X509FindType.FindBySubjectName, Environment.MachineName, false);
					if (currentCerts != null && currentCerts.Count > 0)
					{
						var cert = currentCerts[0];
						clientCerts.Add(cert);
					}
				}
			}
		}

		protected override async Task<StringBuilder> DoWorkMssql(SqlTester data, CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			if (data._initialSleep.HasValue)
				await Task.Delay(data._initialSleep.Value);

			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Mssql"].ConnectionString))
			{
				await conn.OpenAsync(token);

				var sb = new StringBuilder(1000);
				using (var cmd = new SqlCommand(data._sqlQuery, conn))
				{
                    await cmd.PrepareAsync();
					cmd.CommandType = CommandType.Text;

					if (token.IsCancellationRequested)
						return new StringBuilder("cancelled");

					using (var rdr = await cmd.ExecuteReaderAsync(token))
					{
						ProcessReader(rdr, sb);
						return sb;
					}
				}
			}
		}

		protected async override Task<StringBuilder> DoWorkEntities(SqlTester data, CancellationToken token)
		{
			switch (TestType)
			{
				case TestTypeEnum.GROUPEDVIEW:
					return await DoWorkEntitiesGroupedView(data, token);
				case TestTypeEnum.SEARCHER:
					return await DoWorkEntitiesSearcher(data, token);
				default:
					throw new NotSupportedException("bad test type");
			}
		}

		async Task<StringBuilder> DoWorkEntitiesGroupedView(SqlTester data, CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			if (data._initialSleep.HasValue)
				await Task.Delay(data._initialSleep.Value);

			using (testEntities context = new testEntities())
			{
				var query = (from e in context.GetDomains(data._maxCount)
							 select e);

				if (token.IsCancellationRequested)
					return new StringBuilder("cancelled");

				var sb = new StringBuilder(1000);
				foreach (var rdr in query.AsParallel())
				{
					sb.Append(rdr.domain).Append(" | ")
						.Append(rdr.cnt).Append(" | ")
						.Append(rdr.now)
						.AppendLine();
				}
				return sb;
			}
		}

		async Task<StringBuilder> DoWorkEntitiesSearcher(SqlTester data, CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			if (data._initialSleep.HasValue)
				await Task.Delay(data._initialSleep.Value);

			using (testEntities context = new testEntities())
			{
				var query = (from acc in context.accounts
							 where acc.clear_passwd.Contains(data._keyword2Search)
							 select new
							 {
								 user_name = acc.user_name,
								 clear_passwd = acc.clear_passwd,
								 user_id = acc.user_id,
								 now = DateTime.Now
							 });

				if (token.IsCancellationRequested)
					return new StringBuilder("cancelled");

				var sb = new StringBuilder(1000);
				foreach (var rdr in query.AsParallel())
				{
					sb.Append(rdr.user_name).Append(" | ")
						.Append(rdr.clear_passwd).Append(" | ")
						.Append(rdr.user_id).Append(" | ")
						.Append(rdr.now)
						.AppendLine();
				}
				return sb;
			}
		}

		private void HandleWalletInConnectionString(ReadOnlySpan<char> connectionString)
		{
            //WALLET_LOCATION=(SOURCE=(METHOD=file)(METHOD_DATA=(DIRECTORY=c:\\Users\\user\\.blablabla\\wallet)))
            int start = connectionString.IndexOf("DIRECTORY=");
            if (start != -1)
            {
                start = start + "DIRECTORY=".Length;
                int end = connectionString.Slice(start).IndexOf(")");
                if (end != -1)
                {
                    var directory = connectionString.Slice(start, end);
                    if (!directory.IsEmpty)
                    {
                        //Enter directory where the tnsnames.ora and sqlnet.ora files are located
                        OracleConfiguration.TnsAdmin = directory.ToString();

                        //Alternatively, connect descriptor and net service name entries can be placed in app itself
                        //To use, uncomment below and enter the DB machine port, hostname/IP, service name, and distinguished name
                        //Lastly, set the Data Source value to "autonomous"
                        //OracleConfiguration.OracleDataSources.Add("autonomous", "(description=(address=(protocol=tcps)(port=<PORT>)(host=<HOSTNAME/IP>))(connect_data=(service_name=<SERVICE NAME>))(security=(ssl_server_cert_dn=<DISTINGUISHED NAME>)))");                       

                        //Enter directory where wallet is stored locally
                        OracleConfiguration.WalletLocation = OracleConfiguration.TnsAdmin;
                    }
                }
            }
        }

		protected async override Task<StringBuilder> DoWorkOracle(SqlTester data, CancellationToken token)
		{
			if (token.IsCancellationRequested)
				return new StringBuilder("cancelled");

			if (data._initialSleep.HasValue)
				await Task.Delay(data._initialSleep.Value);

			var conn_str = ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString;
			using (var conn = new OracleConnection(conn_str))
			{
				if (OracleConfiguration.TnsAdmin == null)
					HandleWalletInConnectionString(conn_str);

				await conn.OpenAsync(token);

				var sb = new StringBuilder(1000);
				using (var cmd = new OracleCommand(data._sqlQuery, conn))
				{
					if (token.IsCancellationRequested)
						return new StringBuilder("cancelled");

                    await cmd.PrepareAsync();
					cmd.CommandType = CommandType.Text;
					using (var rdr = await cmd.ExecuteReaderAsync(token))
					{
						ProcessReader(rdr, sb);
						return sb;
					}
				}
			}
		}
	}//end class
}