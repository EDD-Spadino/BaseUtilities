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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Common;
using System.Data;
using System.Drawing;
using EMK.LightGeometry;

namespace EliteDangerousCore.DB
{
    public partial class SystemsDB
    {
        public static long GetTotalSystems()
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                using (DbCommand cmd = cn.CreateCommand("select Count(1) from Systems"))
                {
                    return (long)cmd.ExecuteScalar();
                }
            }
        }

        // Beware with no extra conditions, you get them all..  No EDDB info. Mostly used for debugging
        // use starreport to avoid storing the entries instead pass back one by one
        public static List<ISystem> FindStars(string where = null, string orderby = null, string limit = null, bool eddbinfo = false, Action<ISystem> starreport = null)
        {
            List<ISystem> ret = new List<ISystem>();

            //BaseUtils.AppTicks.TickCountLap("Star");

            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Writer))
            {
                using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", eddbinfo ? MakeSystemQueryEDDB : MakeSystemQueryNoEDDB, where, orderby, limit: limit,
                    joinlist: (eddbinfo ? MakeSystemQueryEDDBJoinList : MakeSystemQueryJoinList)))
                {
                    using (DbDataReader reader = selectSysCmd.ExecuteReader())
                    {
                        while (reader.Read())      // if there..
                        {
                            SystemClass s = MakeSystem(reader, eddbinfo);
                            if (starreport != null)
                                starreport(s);
                            else
                                ret.Add(s);
                        }
                    }
                }
            }

            //System.Diagnostics.Debug.WriteLine("Find stars " + BaseUtils.AppTicks.TickCountLap("Star"));
            return ret;
        }

        ///////////////////////////////////////// By Name

        public static ISystem FindStar(string name)
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                return FindStar(name, cn);
            }
        }

        public static ISystem FindStar(string name, SQLiteConnectionSystem cn)
        {
            EliteNameClassifier ec = new EliteNameClassifier(name);

            if (ec.IsNamed)
            {
                // needs index on Names(Name), Sectors(name).  Relies on Names.id being the edsmid.   No system indexes needed

                using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", MakeSystemQueryEDDB,
                                                    "s.edsmid IN (Select id FROM Names WHERE name=@p1) AND s.sectorid IN (Select id FROM Sectors c WHERE c.name=@p2)",
                                                    new Object[] { ec.StarName, ec.SectorName },
                                                    joinlist: MakeSystemQueryEDDBJoinList))
                {
                    // System.Diagnostics.Debug.WriteLine( cn.ExplainQueryPlanString(selectSysCmd));

                    using (DbDataReader reader = selectSysCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MakeSystem(reader);        // read back and make name from db info due to case problems.
                        }
                    }
                }

            }
            else
            {           
                // Numeric or Standard - all data in ID
                // needs index on Systems(Nameid), Sectors(name)

                using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", MakeSysStdNumericQueryEDDB,
                                                    "s.nameid = @p1 AND s.sectorid IN (Select id FROM Sectors c WHERE c.name=@p2)",
                                                    new Object[] { ec.ID, ec.SectorName },
                                                    joinlist: MakeSysStdNumericQueryEDDBJoinList))
                {
                  //  System.Diagnostics.Debug.WriteLine( cn.ExplainQueryPlanString(selectSysCmd));

                    using (DbDataReader reader = selectSysCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return MakeSystem(reader, ec.ID); // read back .. sector name is taken from DB for case reasons
                        }
                    }

                }
            }

            return null;
        }

        ///////////////////////////////////////// By EDSMID

        public static ISystem FindStar(long edsmid)
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                return FindStar(edsmid, cn);
            }
        }

        public static ISystem FindStar(long edsmid, SQLiteConnectionSystem cn)
        {
            // No indexes needed- edsmid is primary key

            using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", MakeSystemQueryEDDB,
                                                "s.edsmid=@p1",
                                                new Object[] { edsmid },
                                                joinlist: MakeSystemQueryEDDBJoinList))
            {
                //System.Diagnostics.Debug.WriteLine( cn.ExplainQueryPlanString(selectSysCmd));

                using (DbDataReader reader = selectSysCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MakeSystem(reader); 
                    }
                }
            }
            return null;
        }

        ///////////////////////////////////////// By Wildcard

        public static List<ISystem> FindStarWildcard(string name, int limit = int.MaxValue)
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                return FindStarWildcard(name, cn, limit);
            }
        }

        public static List<ISystem> FindStarWildcard(string name, SQLiteConnectionSystem cn, int limit = int.MaxValue)
        {
            EliteNameClassifier ec = new EliteNameClassifier(name);

            List<ISystem> ret = new List<ISystem>();

            if (ec.IsStandardParts)     // normal Euk PRoc qc-l d2-3
            {
                using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", MakeSystemQueryEDDB,
                                                    "s.nameid >= @p1 AND s.nameid <= @p2 AND s.sectorid IN (Select id FROM Sectors c WHERE c.name=@p3)",
                                                    new Object[] { ec.ID, ec.IDHigh, ec.SectorName },
                                                    limit:limit,
                                                    joinlist: MakeSystemQueryEDDBJoinList))
                {
                    //System.Diagnostics.Debug.WriteLine( cn.ExplainQueryPlanString(selectSysCmd));

                    using (DbDataReader reader = selectSysCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SystemClass sc = MakeSystem(reader);
                            ret.Add(sc);
                        }
                    }
                }
            }
            else if (ec.IsNumeric)        // HIP 29282
            {
                // checked select *,s.nameid & 0x3fffffffff , cast((s.nameid & 0x3fffffffff) as text) From Systems  s where (s.nameid & (1<<46)!=0) and s.sectorid=15568 USNO entries
                // beware, 1<<46 works, 0x40 0000 0000 does not.. check SQL later

                using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", MakeSystemQueryEDDB,
                                                    "(s.nameid & (1<<46) != 0) AND cast((s.nameid & 0x3fffffffff) as text) LIKE @p1 AND s.sectorid IN (Select id FROM Sectors c WHERE c.name=@p2)",
                                                    new Object[] { ec.NameIdNumeric.ToStringInvariant() + "%", ec.SectorName },
                                                    limit:limit,
                                                    joinlist: MakeSystemQueryEDDBJoinList))  
                {

                    //System.Diagnostics.Debug.WriteLine( cn.ExplainQueryPlanString(selectSysCmd));

                    using (DbDataReader reader = selectSysCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            SystemClass sc = MakeSystem(reader);
                            ret.Add(sc);
                        }
                    }
                }
            }
            else
            {                             // named
                if (ec.StarName.Length > 0)      // if we have a starname component and a sector name, look up sectorname + starname%
                {
                    // Requires CREATE INDEX IF NOT EXISTS NamesName ON Names (Name) CREATE INDEX IF NOT EXISTS SectorName ON Sectors (name)
                    // and requires CREATE INDEX IF NOT EXISTS SystemsName ON Systems (name) for fast lookup of nameid on star list

                    using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", MakeSystemQueryEDDB,
                                                        "s.nameid IN (Select id FROM Names WHERE name LIKE @p1) AND s.sectorid IN (Select id FROM Sectors c WHERE c.name=@p2)",
                                                        new Object[] { ec.StarName + "%", ec.SectorName },
                                                        limit: limit,
                                                        joinlist: MakeSystemQueryEDDBJoinList))
                    {
                        //System.Diagnostics.Debug.WriteLine(cn.ExplainQueryPlanString(selectSysCmd));

                        using (DbDataReader reader = selectSysCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                SystemClass sc = MakeSystem(reader);
                                ret.Add(sc);
                            }

                            limit -= ret.Count;
                        }
                    }
                }

                // look up Sector. Use sectorname, unless it NoSectorName in which case use the starname as a presumed sector name

                // Requires CREATE INDEX IF NOT EXISTS SectorName ON Sectors (name)
                // Requires CREATE INDEX IF NOT EXISTS SystemsSector ON Systems (sector) (Big cost)

                if (limit > 0)
                {
                    using (DbCommand selectSysCmd = cn.CreateSelect("Systems s", MakeSystemQueryEDDB,
                                                        "s.sectorid IN (Select id FROM Sectors c WHERE c.name LIKE @p1)",
                                                        new Object[] { (ec.SectorName != EliteNameClassifier.NoSectorName ? ec.SectorName : ec.StarName) + "%" },
                                                        limit: limit,
                                                        joinlist: MakeSystemQueryEDDBJoinList))
                    {
                       // System.Diagnostics.Debug.WriteLine(cn.ExplainQueryPlanString(selectSysCmd));

                        using (DbDataReader reader = selectSysCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                SystemClass sc = MakeSystem(reader);
                                ret.Add(sc);
                            }
                        }
                    }
                }
            }

            return ret;
        }

        ///////////////////////////////////////// List of systems near xyz

        public static void GetSystemListBySqDistancesFrom(BaseUtils.SortedListDoubleDuplicate<ISystem> distlist, double x, double y, double z,
                                                    int maxitems,
                                                    double mindist, double maxdist, bool spherical,
                                                    Action<ISystem> LookedUp = null
                                                    )
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                GetSystemListBySqDistancesFrom(distlist, x, y, z, maxitems, mindist, maxdist, spherical, cn, LookedUp);
            }
        }

        public static void GetSystemListBySqDistancesFrom(BaseUtils.SortedListDoubleDuplicate<ISystem> distlist,
                                                            double x, double y, double z,
                                                            int maxitems,
                                                            double mindist,         // 0 = no min dist
                                                            double maxdist, 
                                                            bool spherical,
                                                            SQLiteConnectionSystem cn,
                                                            Action<ISystem> LookedUp = null
                                                        )

        {
            int mindistint = mindist>0 ? SystemClass.DoubleToInt(mindist) * SystemClass.DoubleToInt(mindist) : 0;

            // needs a xz index for speed

            using (DbCommand cmd = cn.CreateSelect("Systems s",
                MakeSystemQueryEDDB,
                
                where:
                    "s.x >= @xv - @maxdist " +
                    "AND s.x <= @xv + @maxdist " +
                    "AND s.z >= @zv - @maxdist " +
                    "AND s.z <= @zv + @maxdist " +
                    "AND s.y >= @yv - @maxdist " +
                    "AND s.y <= @yv + @maxdist " +
                    (mindist > 0 ? ("AND (s.x-@xv)*(s.x-@xv)+(s.y-@yv)*(s.y-@yv)+(s.z-@zv)*(s.z-@zv)>=" + (mindistint).ToStringInvariant()) : ""),

                orderby: "(s.x-@xv)*(s.x-@xv)+(s.y-@yv)*(s.y-@yv)+(s.z-@zv)*(s.z-@zv)",         // just use squares to order

                joinlist: MakeSystemQueryEDDBJoinList,
                
                limit: "@max"))
            {
                cmd.AddParameterWithValue("@xv", SystemClass.DoubleToInt(x));
                cmd.AddParameterWithValue("@yv", SystemClass.DoubleToInt(y));
                cmd.AddParameterWithValue("@zv", SystemClass.DoubleToInt(z));
                cmd.AddParameterWithValue("@max", maxitems + 1);     // 1 more, because if we are on a System, that will be returned
                cmd.AddParameterWithValue("@maxdist", SystemClass.DoubleToInt(maxdist));

                //System.Diagnostics.Debug.WriteLine(cn.ExplainQueryPlanString(cmd));

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())// && distlist.Count < maxitems)           // already sorted, and already limited to max items
                    {
                        SystemClass s = MakeSystem(reader);
                        LookedUp?.Invoke(s);                            // callback to say looked up

                        double distsq = s.DistanceSq(x, y, z);
                        if ((!spherical || distsq <= maxdist * maxdist))// MUST use duplicate double list to protect against EDSM having two at the same point
                        {
                            distlist.Add(distsq, s);                  // which Rob has seen crashing the program! Bad EDSM!
                        }
                    }
                }
            }
        }

        public static ISystem GetSystemByPosition(double x, double y, double z)
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                return GetSystemByPosition(x, y, z, cn);
            }
        }

        public static ISystem GetSystemByPosition(double x, double y, double z, SQLiteConnectionSystem cn)
        {
            BaseUtils.SortedListDoubleDuplicate<ISystem> distlist = new BaseUtils.SortedListDoubleDuplicate<ISystem>();
            GetSystemListBySqDistancesFrom(distlist, x, y, z, 1, 0, 0.125, true, cn);
            return (distlist.Count > 0) ? distlist.First().Value : null;
        }

        /////////////////////////////////////////////// Nearest to a point determined by a metric

        public const int metric_nearestwaypoint = 0;     // easiest way to synchronise metric selection..
        public const int metric_mindevfrompath = 1;
        public const int metric_maximum100ly = 2;
        public const int metric_maximum250ly = 3;
        public const int metric_maximum500ly = 4;
        public const int metric_waypointdev2 = 5;

        public static ISystem GetSystemNearestTo(Point3D currentpos,            
                                              Point3D wantedpos,                
                                              double maxfromcurpos,             
                                              double maxfromwanted,             
                                              int routemethod,                  
                                              Action<ISystem> LookedUp = null)
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                return GetSystemNearestTo(currentpos, wantedpos, maxfromcurpos, maxfromwanted, routemethod, cn, LookedUp);
            }
        }


        public static ISystem GetSystemNearestTo(Point3D currentpos,
                                              Point3D wantedpos,
                                              double maxfromcurpos,
                                              double maxfromwanted,
                                              int routemethod,
                                              SQLiteConnectionSystem cn,
                                              Action<ISystem> LookedUp = null)
        {
            using (DbCommand cmd = cn.CreateSelect("Systems s",
                        MakeSystemQueryEDDB,
                        where: 
                                "x >= @xc - @maxfromcurpos " +
                                "AND x <= @xc + @maxfromcurpos " +
                                "AND z >= @zc - @maxfromcurpos " +
                                "AND z <= @zc + @maxfromcurpos " +
                                "AND x >= @xw - @maxfromwanted " +
                                "AND x <= @xw + @maxfromwanted " +
                                "AND z >= @zw - @maxfromwanted " +
                                "AND z <= @zw + @maxfromwanted " +
                                "AND y >= @yc - @maxfromcurpos " +
                                "AND y <= @yc + @maxfromcurpos " +
                                "AND y >= @yw - @maxfromwanted " +
                                "AND y <= @yw + @maxfromwanted ",

                        joinlist: MakeSystemQueryEDDBJoinList))
            {
                cmd.AddParameterWithValue("@xw", SystemClass.DoubleToInt(wantedpos.X));         // easier to manage with named paras
                cmd.AddParameterWithValue("@yw", SystemClass.DoubleToInt(wantedpos.Y));
                cmd.AddParameterWithValue("@zw", SystemClass.DoubleToInt(wantedpos.Z));
                cmd.AddParameterWithValue("@maxfromwanted", SystemClass.DoubleToInt(maxfromwanted));
                cmd.AddParameterWithValue("@xc", SystemClass.DoubleToInt(currentpos.X));
                cmd.AddParameterWithValue("@yc", SystemClass.DoubleToInt(currentpos.Y));
                cmd.AddParameterWithValue("@zc", SystemClass.DoubleToInt(currentpos.Z));
                cmd.AddParameterWithValue("@maxfromcurpos", SystemClass.DoubleToInt(maxfromcurpos));

                //System.Diagnostics.Debug.WriteLine(cn.ExplainQueryPlanString(cmd));

                double bestmindistance = double.MaxValue;
                SystemClass nearestsystem = null;

                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        SystemClass s = MakeSystem(reader);
                        LookedUp?.Invoke(s);                            // callback to say looked up

                        Point3D syspos = new Point3D(s.X, s.Y, s.Z);
                        double distancefromwantedx2 = Point3D.DistanceBetweenX2(wantedpos, syspos); // range between the wanted point and this, ^2
                        double distancefromcurposx2 = Point3D.DistanceBetweenX2(currentpos, syspos);    // range between the wanted point and this, ^2

                        // ENSURE its withing the circles now
                        if (distancefromcurposx2 <= (maxfromcurpos * maxfromcurpos) && distancefromwantedx2 <= (maxfromwanted * maxfromwanted))
                        {
                            if (routemethod == metric_nearestwaypoint)
                            {
                                if (distancefromwantedx2 < bestmindistance)
                                {
                                    nearestsystem = s;
                                    bestmindistance = distancefromwantedx2;
                                }
                            }
                            else
                            {
                                Point3D interceptpoint = currentpos.InterceptPoint(wantedpos, syspos);      // work out where the perp. intercept point is..
                                double deviation = Point3D.DistanceBetween(interceptpoint, syspos);
                                double metric = 1E39;

                                if (routemethod == metric_mindevfrompath)
                                    metric = deviation;
                                else if (routemethod == metric_maximum100ly)
                                    metric = (deviation <= 100) ? distancefromwantedx2 : metric;        // no need to sqrt it..
                                else if (routemethod == metric_maximum250ly)
                                    metric = (deviation <= 250) ? distancefromwantedx2 : metric;
                                else if (routemethod == metric_maximum500ly)
                                    metric = (deviation <= 500) ? distancefromwantedx2 : metric;
                                else if (routemethod == metric_waypointdev2)
                                    metric = Math.Sqrt(distancefromwantedx2) + deviation / 2;

                                if (metric < bestmindistance)
                                {
                                    nearestsystem = s;
                                    bestmindistance = metric;
                                }
                            }
                        }
                    }
                }

                return nearestsystem;
            }
        }

                                    
        public enum SystemAskType { AllStars, SplitPopulatedStars, UnpopulatedStars, PopulatedStars };

        // all stars
        public static void GetSystemVector<V>(int gridid, ref V[] vertices1, ref uint[] colours1, int percentage, Func<int, int, int, V> tovect, SystemAskType ask = SystemAskType.AllStars)
        {
            V[] v2 = null;
            uint[] c2 = null;
            GetSystemVector<V>(gridid, ref vertices1, ref colours1, ref v2, ref c2, percentage, tovect, ask);
        }

        // full interface. 
        // ask = AllStars/UnpopulatedStars/PopulatedStars = only v1/c1 is returned..
        // ask = SplitPopulatedStars = vertices1 is populated, 2 is unpopulated

        public static void GetSystemVector<V>(int gridid, ref V[] vertices1, ref uint[] colours1,
                                                          ref V[] vertices2, ref uint[] colours2,
                                                          int percentage, Func<int, int, int, V> tovect,
                                                          SystemAskType ask = SystemAskType.SplitPopulatedStars)
        {
            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                GetSystemVector<V>(gridid, ref vertices1, ref colours1, ref vertices2, ref colours2, percentage, tovect, cn, ask);
            }
        }

        public static void GetSystemVector<V>(int gridid, ref V[] vertices1, ref uint[] colours1,
                                                          ref V[] vertices2, ref uint[] colours2,
                                                          int percentage, Func<int, int, int, V> tovect,
                                                          SQLiteConnectionSystem cn,
                                                          SystemAskType ask = SystemAskType.SplitPopulatedStars)
        {
            int numvertices1 = 0;
            vertices1 = vertices2 = null;

            int numvertices2 = 0;
            colours1 = colours2 = null;

            Color[] fixedc = new Color[4];
            fixedc[0] = Color.Red;
            fixedc[1] = Color.Orange;
            fixedc[2] = Color.Yellow;
            fixedc[3] = Color.White;

            //System.Diagnostics.Debug.WriteLine("sysLap : " + BaseUtils.AppTicks.TickCountLap());

            using (DbCommand cmd = cn.CreateSelect("Systems s",
                                                    outparas: "s.edsmid,s.x,s.y,s.z" + (ask == SystemAskType.SplitPopulatedStars ? ",e.eddbid" : ""),
                                                    where: "s.sectorid IN (Select id FROM Sectors c WHERE c.gridid = @gridid)" +
                                                            (percentage < 100 ? (" AND ((s.edsmid*2333)%100) <" + percentage.ToStringInvariant()) : "") +
                                                            (ask == SystemAskType.PopulatedStars ? " AND e.edsmid NOT NULL " : "") +
                                                            (ask == SystemAskType.UnpopulatedStars ? " AND e.edsmid IS NULL " : ""),
                                                    joinlist: ask != SystemAskType.AllStars ? new string[] { "LEFT OUTER JOIN EDDB e ON e.edsmid = s.edsmid " } : null
                                                    ))
            {
                cmd.AddParameterWithValue("@gridid", gridid);

               // System.Diagnostics.Debug.WriteLine( cn.ExplainQueryPlanString(cmd));
                vertices1 = new V[250000];
                colours1 = new uint[250000];

                if (ask == SystemAskType.SplitPopulatedStars)
                {
                    vertices2 = new V[250000];
                    colours2 = new uint[250000];
                }
                
                using (DbDataReader reader = cmd.ExecuteReader())
                {
                    //System.Diagnostics.Debug.WriteLine("sysLapStart : " + BaseUtils.AppTicks.TickCountLap());

                    Object[] data = new Object[4];

                    while (reader.Read())
                    {
                        long id = reader.GetInt64(0);       // quicker than cast
                        int x = reader.GetInt32(1);
                        int y = reader.GetInt32(2);
                        int z = reader.GetInt32(3);

                        bool addtosecondary = (ask == SystemAskType.SplitPopulatedStars) ? (reader[4] is System.DBNull) : false;

                        Color basec = fixedc[(id) & 3];
                        int fade = 100 - (((int)id >> 2) & 7) * 8;
                        byte red = (byte)(basec.R * fade / 100);
                        byte green = (byte)(basec.G * fade / 100);
                        byte blue = (byte)(basec.B * fade / 100);

                        if (addtosecondary)
                        {
                            if (numvertices2 == vertices2.Length)
                            {
                                Array.Resize(ref vertices2, vertices2.Length *2);
                                Array.Resize(ref colours2, colours2.Length *2);
                            }

                            colours2[numvertices2] = BitConverter.ToUInt32(new byte[] { red, green, blue, 255 }, 0);
                            vertices2[numvertices2++] = tovect(x, y, z);
                        }
                        else
                        {
                            if (numvertices1 == vertices1.Length)
                            {
                                Array.Resize(ref vertices1, vertices1.Length *2);
                                Array.Resize(ref colours1, colours1.Length *2);
                            }

                            colours1[numvertices1] = BitConverter.ToUInt32(new byte[] { red, green, blue, 255 }, 0);
                            vertices1[numvertices1++] = tovect(x, y, z);
                        }
                    }

              //      System.Diagnostics.Debug.WriteLine("sysLapEnd : " + BaseUtils.AppTicks.TickCountLap());
                }

                Array.Resize(ref vertices1, numvertices1);
                Array.Resize(ref colours1, numvertices1);

                if (ask == SystemAskType.SplitPopulatedStars)
                {
                    Array.Resize(ref vertices2, numvertices2);
                    Array.Resize(ref colours2, numvertices2);
                }

                if (gridid == GridId.SolGrid && vertices1 != null)    // BODGE do here, better once on here than every star for every grid..
                {                       // replace when we have a better naming system
                    int solindex = Array.IndexOf(vertices1, tovect(0, 0, 0));
                    if (solindex >= 0)
                        colours1[solindex] = 0x00ffff;   //yellow
                }
            }
        }

        // randimised id % 100 < selector
        public static List<V> GetStarPositions<V>(int selector, Func<int, int, int, V> tovect)  // return all star positions..
        {
            List<V> ret = new List<V>();

            using (SQLiteConnectionSystem cn = new SQLiteConnectionSystem(mode: SQLLiteExtensions.SQLExtConnection.AccessMode.Reader))
            {
                using (DbCommand cmd = cn.CreateSelect("Systems s",
                                                       outparas: "s.x,s.y,s.z",
                                                       where: "((s.edsmid*2333)%100) <" + selector.ToStringInvariant()
                                                       ))
                {
                    using (DbDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ret.Add(tovect(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
                        }
                    }
                }
            }

            return ret;
        }

        #region Helpers

        //                                         0   1   2   3        4      5        6 
        const string MakeSysStdNumericQuery =     "s.x,s.y,s.z,s.edsmid,c.name,c.gridid";
        const string MakeSysStdNumericQueryEDDB = "s.x,s.y,s.z,s.edsmid,c.name,c.gridid,e.eddbid,e.eddbupdatedat,e.population,e.faction,e.government,e.allegiance,e.state,e.security,e.primaryeconomy,e.power,e.powerstate,e.needspermit";
        static string[] MakeSysStdNumericQueryJoinList = new string[] { "JOIN Sectors c on s.sectorid=c.id" };
        static string[] MakeSysStdNumericQueryEDDBJoinList = new string[] { "JOIN Sectors c on s.sectorid=c.id", "LEFT OUTER JOIN EDDB e on e.edsmid=s.edsmid" };

        static SystemClass MakeSystem(DbDataReader reader, ulong nid, bool eddb = true)
        {
            const int offset = 6;

            EliteNameClassifier ec = new EliteNameClassifier(nid);
            ec.SectorName = reader.GetString(4);

            if (!eddb || reader[offset] is System.DBNull)
            {
                return new SystemClass(ec.ToString(), reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3), reader.GetInt32(5));
            }
            else
            {
                return new SystemClass(ec.ToString(), reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3),
                                reader.GetInt64(offset), reader.GetInt32(offset + 1), reader.GetInt64(offset + 2), reader.GetString(offset + 3),
                                (EDGovernment)reader.GetInt64(offset + 4), (EDAllegiance)reader.GetInt64(offset + 5), (EDState)reader.GetInt64(offset + 6), (EDSecurity)reader.GetInt64(offset + 7),
                                (EDEconomy)reader.GetInt64(offset + offset), reader.GetString(offset + 9), reader.GetString(offset + 10), reader.GetInt32(offset + 11),
                                reader.GetInt32(5), SystemStatusEnum.EDSM);
            }
        }

        //                                     0   1   2   3        4      5        6        7      8            
        const string MakeSystemQueryEDDB    = "s.x,s.y,s.z,s.edsmid,c.name,c.gridid,s.nameid,n.Name,e.eddbid,e.eddbupdatedat,e.population,e.faction,e.government,e.allegiance,e.state,e.security,e.primaryeconomy,e.power,e.powerstate,e.needspermit";
        const string MakeSystemQueryNoEDDB  = "s.x,s.y,s.z,s.edsmid,c.name,c.gridid,s.nameid,n.Name";
        static string[] MakeSystemQueryJoinList = new string[] { "LEFT OUTER JOIN Names n On s.nameid=n.id", "JOIN Sectors c on s.sectorid=c.id" };
        static string[] MakeSystemQueryEDDBJoinList = new string[] { "LEFT OUTER JOIN Names n On s.nameid=n.id", "JOIN Sectors c on s.sectorid=c.id", "LEFT OUTER JOIN EDDB e on e.edsmid=s.edsmid" };

        static SystemClass MakeSystem(DbDataReader reader, bool eddbinfo = true)
        {
            EliteNameClassifier ec = new EliteNameClassifier((ulong)reader.GetInt64(6));
            ec.SectorName = reader.GetString(4);

            if (ec.IsNamed)
                ec.StarName = reader.GetString(7);

            const int offset = 8;
            if (!eddbinfo || reader[offset] is System.DBNull)
            {
                return new SystemClass(ec.ToString(), reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3), reader.GetInt32(5));
            }
            else
            {
                return new SystemClass(ec.ToString(), reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt64(3), 
                                reader.GetInt64(offset), reader.GetInt32(offset + 1), reader.GetInt64(offset + 2), reader.GetString(offset + 3),
                                (EDGovernment)reader.GetInt64(offset + 4), (EDAllegiance)reader.GetInt64(offset + 5), (EDState)reader.GetInt64(offset + 6), (EDSecurity)reader.GetInt64(offset + 7),
                                (EDEconomy)reader.GetInt64(offset + offset), reader.GetString(offset + 9), reader.GetString(offset + 10), reader.GetInt32(offset + 11),
                                reader.GetInt32(5), SystemStatusEnum.EDSM);
            }
        }

        #endregion
    }
}


