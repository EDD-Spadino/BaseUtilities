﻿using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTKUtils.GL4;
using OpenTKUtils.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenTKUtils;

namespace TestOpenTk
{
    public partial class ShaderTestGalaxyTexture : Form
    {
        private Controller3D gl3dcontroller = new Controller3D();

        private Timer systemtimer = new Timer();

        public ShaderTestGalaxyTexture()
        {
            InitializeComponent();

            this.glControlContainer.SuspendLayout();
            gl3dcontroller.CreateGLControl();
            this.glControlContainer.Controls.Add(gl3dcontroller.glControl);
            gl3dcontroller.PaintObjects = ControllerDraw;
            this.glControlContainer.ResumeLayout();

            systemtimer.Interval = 25;
            systemtimer.Tick += new EventHandler(SystemTick);
            systemtimer.Start();
        }

        GLRenderProgramSortedList rObjects = new GLRenderProgramSortedList();
        GLItemsList items = new GLItemsList();


        public class GLGalShader : GLShaderStandard
        {
            string vert =
@"
#version 450 core
#include OpenTKUtils.GL4.UniformStorageBlocks.matrixcalc.glsl
layout (location = 0) in vec4 position;
out gl_PerVertex {
        vec4 gl_Position;
        float gl_PointSize;
        float gl_ClipDistance[];
    };

layout(location = 1) in vec2 texco;

layout(location = 0) out vec2 vs_textureCoordinate;

layout (location = 22) uniform  mat4 transform;

void main(void)
{
	gl_Position = mc.ProjectionModelMatrix * transform * position;        // order important
    vs_textureCoordinate = texco;
}
";
            string frag =
@"
#version 450 core
layout (location=0) in vec2 vs_textureCoordinate;
layout (binding=1) uniform sampler2D textureObject;
out vec4 color;

void main(void)
{
    color = texture(textureObject, vs_textureCoordinate) * vec4(1,1,1,0.8);     
}
";
            public GLGalShader() : base()
            {
                CompileLink(vert, frag: frag);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Closed += ShaderTest_Closed;

            gl3dcontroller.MatrixCalc.PerspectiveNearZDistance = 0.1f;
            gl3dcontroller.ZoomDistance = 20F;
            gl3dcontroller.Start(new Vector3(0, 0, 0), new Vector3(110f, 0, 0f), 1F);

            gl3dcontroller.KeyboardTravelSpeed = (ms) =>
            {
                return (float)ms / 100.0f;
            };

            items.Add("COS-1L", new GLColourShaderWithWorldCoord((a) => { GLStatics.LineWidth(1); }));

            var ss = new GLGalShader();
            ss.StartAction = a => { GLStatics.CullFace(false); };
            ss.FinishAction = a => { GLStatics.DefaultCullFace(); };
            items.Add("TEX-NC", ss);

            items.Add("gal", new GLTexture2D(Properties.Resources.galheightmap7));

            // thoughts.
            // the galmap, extended into 3d, with a function giving the opacity of the bit
            // test this with a set of planes in normal transform above

            // use the volumetric system
            // take a bounding box (-100,100,-20,20,-100,100)
            // rotate to model view
            // find polys to map to bounding box (pass in modelview vertexes)
            // find texture co-ords at vertex edges
            // pass to shader drawing triangles.
            
            #region coloured lines

            rObjects.Add(items.Shader("COS-1L"),    // horizontal
                         GLRenderableItem.CreateVector4Color4(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Lines,
                                                    GLShapeObjectFactory.CreateLines(new Vector3(-100, 0, -100), new Vector3(-100, 0, 100), new Vector3(10, 0, 0), 21),
                                                    new Color4[] { Color.Gray })
                               );


            rObjects.Add(items.Shader("COS-1L"),    // vertical
                         GLRenderableItem.CreateVector4Color4(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Lines,
                               GLShapeObjectFactory.CreateLines(new Vector3(-100, 0, -100), new Vector3(100, 0, -100), new Vector3(0, 0, 10), 21),
                                                         new Color4[] { Color.Gray })
                               );

            rObjects.Add(items.Shader("TEX-NC"),
                        GLRenderableItem.CreateVector4Vector2(items, OpenTK.Graphics.OpenGL4.PrimitiveType.Quads,
                        GLShapeObjectFactory.CreateQuad(200.0f, 200.0f, new Vector3(0, 0, 0)), GLShapeObjectFactory.TexQuad,
                        new GLObjectDataTranslationRotationTexture(items.Tex("gal"), new Vector3(0, 0, 0))
                        ));


            #endregion

            items.Add("MCUB", new GLMatrixCalcUniformBlock());     // create a matrix uniform block 

        }

        private void ShaderTest_Closed(object sender, EventArgs e)
        {
            items.Dispose();
        }

        private void ControllerDraw(MatrixCalc mc, long time)
        {
            ((GLMatrixCalcUniformBlock)items.UB("MCUB")).Set(gl3dcontroller.MatrixCalc);        // set the matrix unform block to the controller 3d matrix calc.

            rObjects.Render(gl3dcontroller.MatrixCalc);

            this.Text = "Looking at " + gl3dcontroller.MatrixCalc.TargetPosition + " dir " + gl3dcontroller.Camera.Current + " eye@ " + gl3dcontroller.MatrixCalc.EyePosition + " Dist " + gl3dcontroller.MatrixCalc.EyeDistance;
        }

        private void SystemTick(object sender, EventArgs e )
        {
            gl3dcontroller.HandleKeyboard(true, OtherKeys);
            gl3dcontroller.Redraw();
        }

        private void OtherKeys( BaseUtils.KeyboardState kb )
        {
            if (kb.IsPressedRemove(Keys.F1, BaseUtils.KeyboardState.ShiftState.None))
            {
                gl3dcontroller.CameraLookAt(new Vector3(0, 0, 0), 1, 2);
            }

            if (kb.IsPressedRemove(Keys.F2, BaseUtils.KeyboardState.ShiftState.None))
            {
                gl3dcontroller.CameraLookAt(new Vector3(4, 0, 0), 1, 2);
            }

            if (kb.IsPressedRemove(Keys.F3, BaseUtils.KeyboardState.ShiftState.None))
            {
                gl3dcontroller.CameraLookAt(new Vector3(10, 0, -10), 1, 2);
            }

            if (kb.IsPressedRemove(Keys.F4, BaseUtils.KeyboardState.ShiftState.None))
            {
                gl3dcontroller.CameraLookAt(new Vector3(50, 0, 50), 1, 2);
            }

        }

    }



}

