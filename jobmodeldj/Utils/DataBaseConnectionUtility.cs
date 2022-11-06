using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;

namespace jobmodeldj.Utils
{
    public class DataBaseConnectionUtility : IDisposable
    {
        public static readonly string FACTORY_SQLSERVER = "System.Data.SqlClient";
        public static readonly string FACTORY_ORACLE = "System.Data.OracleClient";

        public static DataBaseConnectionUtility CreateAndOpenConnection(string connectionString, string factoryString)
        {
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, factoryString);
            dbu.OpenConnection();
            return dbu;
        }

        public static DataBaseConnectionUtility CreateAndOpenConnectionForSQL(string connectionString)
        {
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, FACTORY_SQLSERVER);
            dbu.OpenConnection();
            return dbu;
        }

        public static DataBaseConnectionUtility CreateAndOpenConnectionForOracle(string connectionString)
        {
            DataBaseConnectionUtility dbu = new DataBaseConnectionUtility(connectionString, FACTORY_ORACLE);
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
