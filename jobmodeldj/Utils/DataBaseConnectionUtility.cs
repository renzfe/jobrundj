using System.Data;
using System.Data.Common;
using MySqlConnector;

namespace jobmodeldj.Utils
{
    public class DataBaseConnectionUtility : IDisposable
    {
        public static readonly string FACTORY_SQLSERVER = "Microsoft.Data.SqlClient";
        public static readonly string FACTORY_ORACLE = "System.Data.OracleClient";
        public static readonly string FACTORY_SQLITE = "Microsoft.Data.Sqlite";
        public static readonly string FACTORY_MYSQL = "MySqlConnector";

        public static DataBaseConnectionUtility CreateAndOpenConnection(string connectionString, string factoryString)
        {
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, factoryString);
            dbu.OpenConnection();
            return dbu;
        }

        public static DataBaseConnectionUtility CreateAndOpenConnectionForSQL(string connectionString)
        {
            DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient, Version=5.0.0.0");
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, FACTORY_SQLSERVER);
            dbu.OpenConnection();
            return dbu;
        }
        public static DataBaseConnectionUtility CreateAndOpenConnectionForSqlite(string connectionString)
        {
            //DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
            DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", "Microsoft.Data.Sqlite.SqliteFactory, Microsoft.Data.Sqlite, Version=7.0.0.0");
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, FACTORY_SQLITE);
            dbu.OpenConnection();
            return dbu;
        }

        public static DataBaseConnectionUtility CreateAndOpenConnectionForOracle(string connectionString)
        {
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, FACTORY_ORACLE);
            dbu.OpenConnection();
            return dbu;
        }

        public static DataBaseConnectionUtility CreateAndOpenConnectionForMySql(string connectionString)
        {

            DbProviderFactories.RegisterFactory(FACTORY_MYSQL, MySqlConnectorFactory.Instance);
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, FACTORY_MYSQL);
            dbu.OpenConnection();
            return dbu;
        }


        public string ConnectionString { get { return _connectionString; } }
        //public DbProviderFactory DbProviderFactory { get { return _providerFactory; } }

        private DbTransaction currentTransaction = null;
        private DbConnection dbConnection;
        private string _connectionString;
        DbProviderFactory _providerFactory;
        string _providerFactoryString;

        private DataBaseConnectionUtility(string connectionString, string factoryString)
        {
            _connectionString = connectionString;
            _providerFactoryString = factoryString;
        }

        public DataSet DoQuery(string query)
        {
            DataSet ods = new DataSet();
            if (_providerFactory != null)
            {
                DbDataAdapter odadpt = _providerFactory.CreateDataAdapter();
                DbCommand cmd = dbConnection.CreateCommand();
                if (currentTransaction != null)
                    cmd.Transaction = currentTransaction;
                cmd.CommandText = query;
                odadpt.SelectCommand = cmd;
                odadpt.Fill(ods);
                odadpt.Dispose();
                odadpt = null;
            }
            return ods;
        }
        public DataTable DoReaderAsDataTable(string query)
        {
            DataTable odt = new DataTable();

            if (_providerFactory != null)
            {
                DbCommand cmd = dbConnection.CreateCommand();
                if (currentTransaction != null)
                    cmd.Transaction = currentTransaction;
                cmd.CommandText = query;

                using (var reader = cmd.ExecuteReader())
                {
                    odt.Load(reader);
                }
            }

            return odt;
        }
        public DataTable DoReader(string query)
        {
            DataTable odt = null;
            int rowsNum = -1;
            int colsNum = 0;

            if (_providerFactory != null)
            {
                DbCommand cmd = dbConnection.CreateCommand();
                if (currentTransaction != null)
                    cmd.Transaction = currentTransaction;
                cmd.CommandText = query;

                using (var reader = cmd.ExecuteReader())
                {
                    odt = new DataTable();
                    colsNum = reader.FieldCount;
                    for (int c = 0; c < colsNum; c++)
                    {
                        string colName = reader.GetName(c);
                        Type colType = reader.GetFieldType(c);
                        odt.Columns.Add(colName, colType);
                    }

                    while (reader.Read())
                    {
                        DataRow row = odt.NewRow();
                        for (int c = 0; c < colsNum; c++)
                        {
                            string colName = reader.GetName(c);
                            Type colType = reader.GetFieldType(c);
                            if (colType == typeof(string))
                            {
                                row[c] = reader.GetString(c);
                            }
                            else
                            {
                                row[c] = reader.GetString(c);
                            }
                        }

                        odt.Rows.Add(row);
                    }
                }

            }
            return odt;
        }

        public int ExecCommand(string query)
        {
            if (_providerFactory != null)
            {
                DbCommand cmd = dbConnection.CreateCommand();
                if (currentTransaction != null)
                    cmd.Transaction = currentTransaction;
                cmd.CommandText = query;
                return cmd.ExecuteNonQuery();
            }
            return -1;
        }

        public bool OpenConnection()
        {
            dbConnection= _GetDBConnection();
            dbConnection.Open();

            return true;
        }

        public void Dispose()
        {
            CloseConnection();
        }

        public bool CloseConnection()
        {
            try
            {
                if (currentTransaction != null)
                {
                    currentTransaction.Rollback();
                }

                dbConnection.Close();
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void BeginTransaction()
        {
            if (currentTransaction != null)
            {
                currentTransaction.Rollback();
                currentTransaction.Dispose();
                currentTransaction = null;
            }
            currentTransaction = dbConnection.BeginTransaction();
        }

        public void CommitTransaction()
        {
            currentTransaction.Commit();
            currentTransaction.Dispose();
            currentTransaction = null;
        }

        public void RollbackTransaction()
        {
            if (currentTransaction != null)
            {
                currentTransaction.Rollback();
                currentTransaction.Dispose();
                currentTransaction = null;
            }
        }
        private DbConnection _GetDBConnection()
        {
            if (this.dbConnection != null) return this.dbConnection;

            DbConnection localDbConnection = null;
            this._providerFactory = _GetFactory();
            if (this._providerFactory != null)
            {
                localDbConnection = this._providerFactory.CreateConnection();
                localDbConnection.ConnectionString = ConnectionString;
            }

            return localDbConnection;
        }

        private DbProviderFactory _GetFactory()
        {
            DbProviderFactory factory = null;
            factory = DbProviderFactories.GetFactory(this._providerFactoryString);
            return factory;
        }

    }
}
