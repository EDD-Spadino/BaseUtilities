﻿/*
 * Copyright © 2015 - 2019 EDDiscovery development team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */
 
using SQLLiteExtensions;
using System;
using System.Globalization;

namespace EliteDangerousCore.DB
{
    public class SQLiteConnectionSystem : SQLExtConnectionWithLockRegister<SQLiteConnectionSystem>
    {
        static public string dbFile = @"c:\code\EDSM\edsm.sql";
        const string tablepostfix = "temp"; // postfix for temp tables
        const string debugoutfile = @"c:\code\edsm\Jsonprocess.lst";        // null off

        public SQLiteConnectionSystem() : base(dbFile, false, Initialize, AccessMode.ReaderWriter)
        {
        }

        public SQLiteConnectionSystem(AccessMode mode = AccessMode.ReaderWriter) : base(dbFile, false, Initialize, mode)
        {
        }

        private SQLiteConnectionSystem(bool utc, Action init) : base(dbFile, utc, init, AccessMode.ReaderWriter)
        {
        }

        #region Init

        public static void Initialize()
        {
            InitializeIfNeeded(() =>
            {
                using (SQLiteConnectionSystem conn = new SQLiteConnectionSystem(false, null))       // use this special one so we don't get double init.
                {
                    System.Diagnostics.Debug.WriteLine("Initialise EDSM DB");
                    UpgradeSystemsDB(conn);
                }
            });
        }

        protected static bool UpgradeSystemsDB(SQLiteConnectionSystem conn)
        {
            int dbver;
            try
            {
                conn.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS Register (ID TEXT PRIMARY KEY NOT NULL, ValueInt INTEGER, ValueDouble DOUBLE, ValueString TEXT, ValueBlob BLOB)");

                // BE VERY careful with connections when creating/deleting tables - you end up with SQL Schema errors or it not seeing the table

                conn.ExecuteNonQueries(new string[]             // always kill these old tables and make EDDB new table
                    {
                    "DROP TABLE IF EXISTS EddbSystems",
                    "DROP TABLE IF EXISTS Distances",
                    "DROP TABLE IF EXISTS Stations",
                    "DROP TABLE IF EXISTS SystemAliases",
                    "DROP TABLE IF EXISTS station_commodities",
                    "CREATE TABLE IF NOT EXISTS EDDB (edsmid INTEGER PRIMARY KEY NOT NULL, eddbid INTEGER, eddbupdatedat INTEGER, population INTEGER, faction TEXT, government INTEGER, allegiance INTEGER, state INTEGER, security INTEGER, primaryeconomy INTEGER, needspermit INTEGER, power TEXT, powerstate TEXT, properties TEXT)",
                    "CREATE TABLE IF NOT EXISTS Aliases (edsmid INTEGER PRIMARY KEY NOT NULL, edsmid_mergedto INTEGER, name TEXT COLLATE NOCASE)"
                    });

                CreateStarTables(conn);                     // ensure we have
                CreateSystemDBTableIndexes(conn);           // ensure they are there 
                DropStarTables(conn, tablepostfix);         // clean out any temp tables half prepared 

                SQLExtRegister reg = new SQLExtRegister(conn);

                dbver = reg.GetSettingInt("DBVer", 0);      // use reg, don't use the built in func as they create new connections and confuse the schema
                if (dbver < 200)
                {
                    reg.PutSettingInt("DBVer", 200);
                    reg.DeleteKey("EDDBSystemsTime");       // force a reload of EDDB
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpgradeSystemsDB error: " + ex.Message + Environment.NewLine + ex.StackTrace);
                return false;
            }
        }

        #endregion

        #region Updators

        // full table replace

        public static void UpgradeSystemTableFromFile(string filename, bool[] gridids, Func<bool> cancelRequested, Action<string> reportProgress)
        {
            using (SQLiteConnectionSystem conn = new SQLiteConnectionSystem(AccessMode.ReaderWriter))     
            {
                DropStarTables(conn, tablepostfix);     // just in case, kill the old tables
                CreateStarTables(conn, tablepostfix);     // and make new temp tables
            }

            DateTime maxdate = DateTime.MinValue;
            long updates = SystemsDB.ParseEDSMJSONFile(filename, gridids, ref maxdate, cancelRequested, reportProgress, tablepostfix, presumeempty: true, debugoutputfile: debugoutfile);

            using (SQLiteConnectionSystem conn = new SQLiteConnectionSystem(AccessMode.ReaderWriter))   
            {
                if (updates > 0)
                {
                    reportProgress?.Invoke("Remove old data");
                    DropStarTables(conn);     // drop the main ones - this also kills the indexes

                    RenameStarTables(conn, tablepostfix, "");     // rename the temp to main ones

                    reportProgress?.Invoke("Shrinking database");
                    conn.Vacuum();

                    reportProgress?.Invoke("Creating indexes");
                    CreateSystemDBTableIndexes(conn);

                    SetLastEDSMRecordTimeUTC(maxdate);          // record last data stored in database
                }
                else
                    DropStarTables(conn, tablepostfix);     // clean out half prepared tables
            }
        }

        // use a file to update the data..

        public static long UpdateSystemTableFromFile(string filename, bool[] gridids, Func<bool> cancelRequested, Action<string> reportProgress)
        {
            DateTime maxdate = GetLastEDSMRecordTimeUTC();

            long updates = SystemsDB.ParseEDSMJSONFile(filename, gridids, ref maxdate, cancelRequested, reportProgress, tablepostfix, presumeempty: false, debugoutputfile: debugoutfile);

            if ( updates>0)
                SetLastEDSMRecordTimeUTC(maxdate);          // record last data stored in database

            return updates;
        }


        // check to see if table type 102 exists, if so, update

        public static void UpgradeSystemTableFrom102TypeDB(Func<bool> cancelRequested, Action<string> reportProgress)
        {
            bool executeupgrade = false;
            string tablepostfix = "temp";

            // first work out if we can upgrade, if so, create temp tables

            using (SQLiteConnectionSystem conn = new SQLiteConnectionSystem(AccessMode.ReaderWriter))  
            {
                var list = conn.Tables();

                if (list.Contains("EdsmSystems") )
                {
                    if (SQLiteConnectionSystem.GetSettingInt("DBVer", 0) == 102)        // is it a 102 database?, yes go.
                    {
                        DropStarTables(conn, tablepostfix);     // just in case, kill the old tables
                        CreateStarTables(conn, tablepostfix);     // and make new temp tables
                        executeupgrade = true;
                    }
                    else
                    {
                        conn.ExecuteNonQueries(new string[]         // older than 102, not supporting, remove
                        {
                            "DROP TABLE IF EXISTS EdsmSystems",
                            "DROP TABLE IF EXISTS SystemNames",
                        });
                    }
                }
            }

            //drop connection, execute upgrade in another connection, this solves an issue with SQL 17 error

            if ( executeupgrade )
            { 
                int maxgridid = int.MaxValue;// 109;    // for debugging

                long updates = SystemsDB.UpgradeDB102to200(cancelRequested, reportProgress, tablepostfix, tablesareempty: true, maxgridid: maxgridid);

                using (SQLiteConnectionSystem conn = new SQLiteConnectionSystem(AccessMode.ReaderWriter))      // use this special one so we don't get double init.
                {
                    if ( updates >= 0 ) // a cancel will result in -1
                    {
                        // keep code for checking

                        if (false)   // demonstrate replacement to show rows are overwitten and not duplicated in the edsmid column and that speed is okay
                        {
                            long countrows = conn.CountOf("Systems" + tablepostfix, "edsmid");
                            long countnames = conn.CountOf("Names" + tablepostfix, "id");
                            long countsectors = conn.CountOf("Sectors" + tablepostfix, "id");

                            // replace takes : Sector 108 took 44525 U1 + 116 store 5627 total 532162 0.02061489 cumulative 11727

                            SystemsDB.UpgradeDB102to200(cancelRequested, reportProgress, tablepostfix, tablesareempty: false, maxgridid: maxgridid);
                            System.Diagnostics.Debug.Assert(countrows == conn.CountOf("Systems" + tablepostfix, "edsmid"));
                            System.Diagnostics.Debug.Assert(countnames * 2 == conn.CountOf("Names" + tablepostfix, "id"));      // names are duplicated.. so should be twice as much
                            System.Diagnostics.Debug.Assert(countsectors == conn.CountOf("Sectors" + tablepostfix, "id"));
                            System.Diagnostics.Debug.Assert(1 == conn.CountOf("Systems" + tablepostfix, "edsmid", "edsmid=6719254"));
                        }

                        DropStarTables(conn);     // drop the main ones - this also kills the indexes

                        RenameStarTables(conn, tablepostfix, "");     // rename the temp to main ones

                        reportProgress?.Invoke("Removing old system tables");

                        conn.ExecuteNonQueries(new string[]
                        {
                            "DROP TABLE IF EXISTS EdsmSystems",
                            "DROP TABLE IF EXISTS SystemNames",
                        });

                        reportProgress?.Invoke("Shrinking database");
                        conn.Vacuum();  

                        reportProgress?.Invoke("Creating indexes");         // NOTE the date should be the same so we don't rewrite
                        CreateSystemDBTableIndexes(conn);
                    }
                    else
                    {
                        DropStarTables(conn, tablepostfix);     // just in case, kill the old tables
                    }
                }
            }
        }

        #endregion

        #region Time markers

        // time markers - keeping the old code for now, not using better datetime funcs

        static public void ForceEDSMFullUpdate()
        {
            SQLiteConnectionSystem.PutSettingString("EDSMLastSystems", "2010-01-01 00:00:00");
        }

        static public DateTime GetLastEDSMRecordTimeUTC()
        {
            string rwsystime = SQLiteConnectionSystem.GetSettingString("EDSMLastSystems", "2000-01-01 00:00:00"); // Latest time from RW file.
            DateTime edsmdate;

            if (!DateTime.TryParse(rwsystime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out edsmdate))
                edsmdate = new DateTime(2000, 1, 1);

            return edsmdate;
        }

        static public void SetLastEDSMRecordTimeUTC(DateTime time)
        {
            SQLiteConnectionSystem.PutSettingString("EDSMLastSystems", time.ToString(CultureInfo.InvariantCulture));
            System.Diagnostics.Debug.WriteLine("Last EDSM record " + time.ToString());
        }

        static public DateTime GetLastEDDBDownloadTime()
        {
            string timestr = SQLiteConnectionSystem.GetSettingString("EDDBSystemsTime", "0");
            return new DateTime(Convert.ToInt64(timestr), DateTimeKind.Utc);
        }

        static public void ForceEDDBFullUpdate()
        {
            SQLiteConnectionSystem.PutSettingString("EDDBSystemsTime", "0");
        }

        #endregion

        #region Helpers

        private static void CreateStarTables(SQLExtConnection conn, string postfix = "")
        {
            conn.ExecuteNonQueries(new string[]
            {
                //"CREATE TABLE IF NOT EXISTS Systems" + postfix + " (edsmid INTEGER PRIMARY KEY NOT NULL UNIQUE , sectorid INTEGER, nameid INTEGER, x INTEGER, y INTEGER, z INTEGER)",
                //"CREATE TABLE IF NOT EXISTS Systems" + postfix + " (id INTEGER PRIMARY KEY NOT NULL UNIQUE , edsmid INTEGER, sectorid INTEGER, nameid INTEGER, x INTEGER, y INTEGER, z INTEGER)",

                "CREATE TABLE IF NOT EXISTS Sectors" + postfix + " (id INTEGER PRIMARY KEY NOT NULL, gridid INTEGER, name TEXT NOT NULL COLLATE NOCASE)",
                "CREATE TABLE IF NOT EXISTS Systems" + postfix + " (edsmid INTEGER PRIMARY KEY NOT NULL , sectorid INTEGER, nameid INTEGER, x INTEGER, y INTEGER, z INTEGER)",
                "CREATE TABLE IF NOT EXISTS Names" + postfix + " (id INTEGER PRIMARY KEY NOT NULL , Name TEXT NOT NULL  COLLATE NOCASE )",
            });
        }

        private static void DropStarTables(SQLExtConnection conn, string postfix = "")
        {
            conn.ExecuteNonQueries(new string[]
            {
                "DROP TABLE IF EXISTS Sectors" + postfix,       // dropping the tables kills the indexes
                "DROP TABLE IF EXISTS Systems" + postfix,
                "DROP TABLE IF EXISTS Names" + postfix,
            });
        }

        private static void RenameStarTables(SQLExtConnection conn, string frompostfix, string topostfix)
        {
            conn.ExecuteNonQueries(new string[]
            {
                "ALTER TABLE Sectors" + frompostfix + " RENAME TO Sectors" + topostfix,       
                "ALTER TABLE Systems" + frompostfix + " RENAME TO Systems" + topostfix,       
                "ALTER TABLE Names" + frompostfix + " RENAME TO Names" + topostfix,       
            });
        }

        private static void CreateSystemDBTableIndexes(SQLiteConnectionSystem conn) 
        {
            string[] queries = new[]
            {
                 "CREATE INDEX IF NOT EXISTS SystemsNameid ON Systems (nameid)",        // on 32Msys, about 500mb cost, massive speed increase in find star
                 "CREATE INDEX IF NOT EXISTS SystemsSectorid ON Systems (sectorid)",    // on 32Msys, about 500mb cost, massive speed increase in find star

                 "CREATE INDEX IF NOT EXISTS NamesName ON Names (Name)",            // improved speed from 9038 (named)/1564 (std) to 516/446ms at minimal cost

                 "CREATE INDEX IF NOT EXISTS SectorName ON Sectors (name)",         // name - > entry
                 "CREATE INDEX IF NOT EXISTS SectorGridid ON Sectors (gridid)",     // gridid -> entry
            };

            conn.ExecuteNonQueries(queries);
        }

        #endregion

    }
}
