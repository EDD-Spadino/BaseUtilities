﻿/*
 * Copyright © 2016 - 2020 EDDiscovery development team
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
using System.IO;
using System.Linq;

namespace BaseUtils
{
    static public class LogClean
    {
        public static void DeleteOldLogFiles(string logroot, string search , int maxagedays, long MaxLogDirSizeMB )
        {
            if ( Directory.Exists(logroot))
            { 
                long totsize = 0;

                System.Diagnostics.Trace.WriteLine("Running logfile age check");

                DirectoryInfo dir = new DirectoryInfo(logroot);     // in order, time decending
                FileInfo[] files = dir.GetFiles(search).OrderByDescending(f => f.LastWriteTimeUtc).ToArray();

                foreach (FileInfo fi in files)
                {
                    DateTime time = fi.CreationTime;

                    TimeSpan maxage = new TimeSpan(maxagedays, 0, 0, 0);
                    TimeSpan fileage = DateTime.Now - time;
                    totsize += fi.Length;

                    try
                    {
                        if (fileage > maxage)
                        {
                            System.Diagnostics.Trace.WriteLine(String.Format("File {0} is older then maximum age. Removing file from Logs.", fi.Name));
                            fi.Delete();
                        }
                        else if (totsize >= MaxLogDirSizeMB * 1048576)
                        {
                            System.Diagnostics.Trace.WriteLine($"File {fi.Name} pushes total log directory size over limit of {MaxLogDirSizeMB}MB");
                            fi.Delete();
                        }
                    }
                    catch ( Exception e )
                    {
                        System.Diagnostics.Trace.WriteLine($"File {fi.Name} cannot remove " + e.ToString());
                    }
                }
            }
        }
    }
}
