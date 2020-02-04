﻿/*
 * Copyright 2019-2020 Robbyxp1 @ github.com
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


using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;

namespace OpenTKUtils.GL4
{
    // this is a render list, holding a list of Shader programs
    // each shader program is associated with zero or more RenderableItems 
    // This Start() each program, goes thru the render list Binding and Rendering each item
    // then it Finish() the program
    // Shaders are executed in the order added
    // Renderable items are ordered by shader, then in the order added.
    // if you add a compute shader to the list, then the renderable item must be null.  
    // adding a compute shader in the middle of other renderable items may be useful - but remember to use a memory barrier if required in the shader FinishAction routine

    public class GLRenderProgramSortedList
    {
        private Dictionary<IGLProgramShader, List<Tuple<string, IGLRenderableItem>>> renderables;
        private Dictionary<string,IGLRenderableItem> byname;
        private int unnamed = 0;

        public GLRenderProgramSortedList()
        {
            renderables = new Dictionary<IGLProgramShader, List<Tuple<string, IGLRenderableItem>>>();
            byname = new Dictionary<string, IGLRenderableItem>();
        }

        public void Add(IGLProgramShader prog, string name, IGLRenderableItem r)        // name is the id given to this renderable
        {
            AddItem(prog, name, r);
            byname.Add(name, r);
        }

        public void Add(IGLProgramShader prog, IGLRenderableItem r)
        {
            string n = prog.GetType().Name + ":" + r.GetType().Name + " # " + (unnamed++).ToStringInvariant();
            AddItem(prog, n, r);
        }

        public void Add(GLShaderCompute cprog)
        {
            string n = cprog.GetType().Name + " # " + (unnamed++).ToStringInvariant();
            AddItem(cprog, n, null);
        }

        public IGLRenderableItem this[string renderitem] { get { return byname[renderitem]; } }
        public bool Contains(string renderitem) { return byname.ContainsKey(renderitem); }

        public void Render(GLRenderControl currentstate, GLMatrixCalc c)
        {
            foreach (var d in renderables)
            {
               // System.Diagnostics.Debug.WriteLine("Shader " + d.Key.GetType().Name);
                d.Key.Start();       // start the program

                foreach (var g in d.Value)
                {
                    if (g.Item2 != null)  // may have added a null renderable item if its a compute shader.
                    {
                       // System.Diagnostics.Debug.WriteLine("Render " + g.Item1);
                        g.Item2.Bind(currentstate, d.Key, c);
                        g.Item2.Render();
                       // System.Diagnostics.Debug.WriteLine("....Render Over " + g.Item1);
                    }
                }

                d.Key.Finish();
            }

            GL.UseProgram(0);           // final clean up
            GL.BindProgramPipeline(0);
        }


        private void AddItem(IGLProgramShader prog, string name, IGLRenderableItem r)
        {
            if (!renderables.ContainsKey(prog))
                renderables.Add(prog, new List<Tuple<string, IGLRenderableItem>>());

            var list = renderables[prog];

            foreach( var x in list)
            {
                int deltas = x.Item2.RenderControl.Deltas(r.RenderControl);
                System.Diagnostics.Debug.WriteLine("Render list " + x.Item1 + " delta to " + name + " = " + deltas);
            }


            renderables[prog].Add(new Tuple<string, IGLRenderableItem>(name, r));
        }
    }

    // use this to just have a compute shader list - same as above, but can only add compute shaders
    public class GLComputeShaderList : GLRenderProgramSortedList        
    {
        public new void Add(IGLProgramShader prog, string name, IGLRenderableItem r)
        {
            System.Diagnostics.Debug.Assert(false);
        }

        public new  void Add(IGLProgramShader prog, IGLRenderableItem r)
        {
            System.Diagnostics.Debug.Assert(false);
        }

        public void Run()      
        {
            Render(null,null);
        }
    }
}

