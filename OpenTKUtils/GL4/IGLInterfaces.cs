﻿/*
 * Copyright © 2015 - 2018 EDDiscovery development team
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

namespace OpenTKUtils.GL4
{
    public interface IGLVertexArray : IDisposable       // to be attached to a renderableitem, vertex arrays need to be based on this
    {
        int Id { get; }
        void Bind();                 // called just before the item is drawn
    }

    public interface IGLInstanceData                        // to be attached to a rendableitem, instance data need to be based on this. Should not need to be disposable..
    {
        void Bind(IGLProgramShader shader, Common.MatrixCalc c);  // called just before the item is drawn
    }

    public interface IGLShader : IDisposable                // All shaders inherit from this
    {
        int Id { get; }
        void Start();                                       // Renders call this when program has just started
        void Finish();                                      // Renders call this when program has ended
    }

    public interface IGLPipelineShader : IGLShader          // All pipeline shaders come from this
    {
    }

    public interface IGLProgramShader : IGLShader           // Shaders suitable for the rendering queue inherit from this
    {
        IGLShader Get(OpenTK.Graphics.OpenGL4.ShaderType t);    // get a subcomponent.  if the shader does not have subcomponents, its should return itself.
        Action<IGLProgramShader> StartAction { get; set; }      // allow start and finish actions to be added to the shader..
        Action<IGLProgramShader> FinishAction { get; set; }
        Tuple<IGLTexture, int>[] Textures {get;}            // optional set of textures to bind at the Start point.
    }

    public interface IGLTexture : IDisposable               // all textures from this..
    {
        int Id { get; }
        int Width { get; }                                  // primary width of mipmap level 0 bitmap on first array entry
        int Height { get; }
        void Bind(int bindingpoint);                        // textures have a chance to bind themselves, called either by instance data (if per object texture) or by shader (either internally or via StartAction)
    }

    public interface IGLRenderableItem                      // a renderable item inherits from this..
    {
        void Bind(IGLProgramShader shader, Common.MatrixCalc c );                 // Bind to context
        void Render();                                      // and render - do the Draw.
        IGLInstanceData InstanceData { get; set; }          // may be null - no instance data.  Allows instance data to be modified in the main program
        OpenTK.Graphics.OpenGL4.PrimitiveType PrimitiveType { get; set; }       // Draw type

    }
}