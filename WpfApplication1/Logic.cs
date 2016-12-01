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

namespace WpfApplication1
{
    public enum DatabaseTypeEnum { MYSQL, POSTGRES, MSSQL, MSSQL_ENTITIES, ORACLE }

    public interface ISqlTestExecutor
    {
        StringBuilder Execute();
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

        protected abstract StringBuilder DoWorkMysql(SqlTester data);

        protected abstract StringBuilder DoWorkPostgres(SqlTester data);

        protected abstract StringBuilder DoWorkMssql(SqlTester data);

        protected abstract StringBuilder DoWorkEntities(SqlTester data);

        protected abstract StringBuilder DoWorkOracle(SqlTester data);

        protected abstract StringBuilder ExecuteGroupedView();

        protected abstract StringBuilder ExecuteSearcher();

        public StringBuilder Execute()
        {
            switch (TestType)
            {
                case TestTypeEnum.GROUPEDVIEW:
                    return ExecuteGroupedView();
                case TestTypeEnum.SEARCHER:
                    return ExecuteSearcher();
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

        protected override StringBuilder ExecuteGroupedView()
        {
            StringBuilder result;
            Stopwatch timer = new Stopwatch();
            timer.Start();
            switch (DataBaseType)
            {
                case DatabaseTypeEnum.MYSQL:
                    _sqlQuery = string.Concat("SELECT domain, cnt, now() FROM domains limit ", _maxCount);
                    result = this.DoWorkMysql(this);
                    break;
                case DatabaseTypeEnum.POSTGRES:
                    _sqlQuery = string.Concat("SELECT domain, cnt, now() FROM domains limit ", _maxCount);
                    result = this.DoWorkPostgres(this);
                    break;
                case DatabaseTypeEnum.MSSQL:
                    _sqlQuery = string.Concat("SELECT top ", _maxCount, " domain, cnt, getdate() FROM domains");
                    result = this.DoWorkMssql(this);
                    break;
                case DatabaseTypeEnum.MSSQL_ENTITIES:
                    result = this.DoWorkEntitiesGroupedView(this);
                    break;
                case DatabaseTypeEnum.ORACLE:
                    _sqlQuery = string.Concat("SELECT domain, cnt, TO_CHAR(SYSDATE, 'MM-DD-YYYY HH24:MI:SS') now FROM domains where rownum < ", _maxCount);
                    result = this.DoWorkOracle(this);
                    break;
                default:
                    throw new NotSupportedException("Bad DataBaseType");
            }
            timer.Stop();
            result.Insert(0, string.Concat("elapsed ", timer.ElapsedMilliseconds, " milliseconds", Environment.NewLine));
            return result;
        }

        protected override StringBuilder ExecuteSearcher()
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
                                                where clear_passwd like '%", _keyword2Search, "%'");
                    result = this.DoWorkMysql(this);
                    break;
                case DatabaseTypeEnum.POSTGRES:
                    _sqlQuery = string.Concat(@"select user_name, clear_passwd, cast(user_id as bigint), now()
                                                from accounts
                                                where clear_passwd like '%", _keyword2Search, "%'");
                    result = this.DoWorkPostgres(this);
                    break;
                case DatabaseTypeEnum.MSSQL:
                    _sqlQuery = string.Concat(@"select user_name, clear_passwd, cast(user_id as bigint), getdate() 
                                                from accounts 
                                                where clear_passwd like '%", _keyword2Search, "%'");
                    result = this.DoWorkMssql(this);
                    break;
                case DatabaseTypeEnum.MSSQL_ENTITIES:
                    result = this.DoWorkEntitiesSearcher(this);
                    break;
                case DatabaseTypeEnum.ORACLE:
                    _sqlQuery = string.Concat(@"select user_name, clear_passwd, user_id, TO_CHAR(SYSDATE, 'MM-DD-YYYY HH24:MI:SS') now
                                                from accounts 
                                                where clear_passwd like '%", _keyword2Search, "%'");
                    result = this.DoWorkOracle(this);
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

        protected override StringBuilder DoWorkMysql(SqlTester data)
        {
            if (data._initialSleep.HasValue)
                Thread.Sleep(data._initialSleep.Value);
            using (var conn = new MySqlConnection(ConfigurationManager.ConnectionStrings["Mysql"].ConnectionString))
            {
                conn.Open();

                var sb = new StringBuilder(100000);
                using (MySqlCommand cmd = new MySqlCommand(data._sqlQuery, conn))
                {
                    cmd.Prepare();
                    cmd.CommandType = System.Data.CommandType.Text;
                    using (MySqlDataReader rdr = cmd.ExecuteReader())
                    {
                        ProcessReader(rdr, sb);
                        return sb;
                    }
                }
            }
        }

        protected override StringBuilder DoWorkPostgres(SqlTester data)
        {
            if (data._initialSleep.HasValue)
                Thread.Sleep(data._initialSleep.Value);
            using (var conn = new NpgsqlConnection(ConfigurationManager.ConnectionStrings["Postgres"].ConnectionString))
            {
                conn.Open();

                var sb = new StringBuilder(1000);
                using (var cmd = new NpgsqlCommand(data._sqlQuery, conn))
                {
                    cmd.Prepare();
                    cmd.CommandType = System.Data.CommandType.Text;
                    using (NpgsqlDataReader rdr = cmd.ExecuteReader())
                    {
                        ProcessReader(rdr, sb);
                        return sb;
                    }
                }
            }
        }

        protected override StringBuilder DoWorkMssql(SqlTester data)
        {
            if (data._initialSleep.HasValue)
                Thread.Sleep(data._initialSleep.Value);
            using (var conn = new SqlConnection(ConfigurationManager.ConnectionStrings["Mssql"].ConnectionString))
            {
                conn.Open();

                var sb = new StringBuilder(1000);
                using (var cmd = new SqlCommand(data._sqlQuery, conn))
                {
                    cmd.Prepare();
                    cmd.CommandType = System.Data.CommandType.Text;
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        ProcessReader(rdr, sb);
                        return sb;
                    }
                }
            }
        }

        protected override StringBuilder DoWorkEntities(SqlTester data)
        {
            switch (TestType)
            {
                case TestTypeEnum.GROUPEDVIEW:
                    return DoWorkEntitiesGroupedView(data);
                case TestTypeEnum.SEARCHER:
                    return DoWorkEntitiesSearcher(data);
                default:
                    throw new NotSupportedException("bad test type");
            }
        }

        StringBuilder DoWorkEntitiesGroupedView(SqlTester data)
        {
            if (data._initialSleep.HasValue)
                Thread.Sleep(data._initialSleep.Value);
            using (testEntities context = new testEntities())
            {
                var query = (from e in context.GetDomains(data._maxCount)
                             select e);

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

        StringBuilder DoWorkEntitiesSearcher(SqlTester data)
        {
            if (data._initialSleep.HasValue)
                Thread.Sleep(data._initialSleep.Value);
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

        protected override StringBuilder DoWorkOracle(SqlTester data)
        {
            if (data._initialSleep.HasValue)
                Thread.Sleep(data._initialSleep.Value);
            using (var conn = new OracleConnection(ConfigurationManager.ConnectionStrings["OracleDbContext"].ConnectionString))
            {
                conn.Open();

                var sb = new StringBuilder(1000);
                using (var cmd = new OracleCommand(data._sqlQuery, conn))
                {
                    cmd.Prepare();
                    cmd.CommandType = System.Data.CommandType.Text;
                    using (OracleDataReader rdr = cmd.ExecuteReader())
                    {
                        ProcessReader(rdr, sb);
                        return sb;
                    }
                }
            }
        }
    }//end class
}