﻿/*
 * Copyright © 2019 Robbyxp1 @ github.com
 * Part of the EDDiscovery Project
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
 */


using System;
using OpenTK.Graphics.OpenGL4;

namespace OpenTKUtils.GL4
{
    // This is a GL shader object of type ShaderType
    // It can compile itself
    // Programs hold multiple shaders of different types.

    public class GLShader : IDisposable
    {
        public int Id { get; private set; }
        public bool Compiled { get { return Id != -1; } }
        ShaderType type;

        public GLShader( ShaderType t )
        {
            Id = -1;
            type = t;
        }

        public string Compile(string source)                // string return gives any errors
        {
            Id = GL.CreateShader(type);
            GL.ShaderSource(Id, source);
            GL.CompileShader(Id);
            string CompileReport = GL.GetShaderInfoLog(Id);

            if (CompileReport.HasChars())
            {
                GL.DeleteShader(Id);
                Id = -1;

                int opos = CompileReport.IndexOf("0(");
                if (opos != -1)
                {
                    int opose = CompileReport.IndexOf(")", opos);
                    if (opose != -1)     // lets help ourselves by reporting the source.. since the source can be obscure.
                    {
                        int? lineno = CompileReport.Substring(opos + 2, opose - opos - 2).InvariantParseIntNull();

                        if (lineno.HasValue)
                        {
                            CompileReport = CompileReport + Environment.NewLine + source.LineMarking(lineno.Value - 5, 10, "##0", lineno.Value);
                        }
                    }
                }

                return CompileReport;
            }

            return null;
        }

        public void Dispose()           // you can double dispose.
        {
            if (Id != -1)
            {
                GL.DeleteShader(Id);
                Id = -1;
            }
        }
    }
}