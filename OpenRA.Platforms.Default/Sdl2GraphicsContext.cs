#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Graphics;
using OpenRA.Primitives;
using SDL2;

namespace OpenRA.Platforms.Default
{
	sealed class Sdl2GraphicsContext : ThreadAffine, IGraphicsContext
	{
		readonly Sdl2PlatformWindow window;
		bool disposed;
		IntPtr context;

		public Sdl2GraphicsContext(Sdl2PlatformWindow window)
		{
			this.window = window;
		}

		internal void InitializeOpenGL()
		{
			SetThreadAffinity();

			context = SDL.SDL_GL_CreateContext(window.Window);
			if (context == IntPtr.Zero || SDL.SDL_GL_MakeCurrent(window.Window, context) < 0)
				throw new InvalidOperationException("Can not create OpenGL context. (Error: {0})".F(SDL.SDL_GetError()));

			OpenGL.Initialize();

			uint vao;
			OpenGL.CheckGLError();
			OpenGL.glGenVertexArrays(1, out vao);
			OpenGL.CheckGLError();
			OpenGL.glBindVertexArray(vao);
			OpenGL.CheckGLError();

			OpenGL.glEnableVertexAttribArray(Shader.VertexPosAttributeIndex);
			OpenGL.CheckGLError();
			OpenGL.glEnableVertexAttribArray(Shader.TexCoordAttributeIndex);
			OpenGL.CheckGLError();
			OpenGL.glEnableVertexAttribArray(Shader.TexMetadataAttributeIndex);
			OpenGL.CheckGLError();
		}

		public IVertexBuffer<Vertex> CreateVertexBuffer(int size)
		{
			VerifyThreadAffinity();
			return new VertexBuffer<Vertex>(size);
		}

		public ITexture CreateTexture()
		{
			VerifyThreadAffinity();
			return new Texture();
		}

		public IFrameBuffer CreateFrameBuffer(Size s)
		{
			VerifyThreadAffinity();
			return new FrameBuffer(s, new Texture(), Color.FromArgb(0));
		}

		public IFrameBuffer CreateFrameBuffer(Size s, Color clearColor)
		{
			VerifyThreadAffinity();
			return new FrameBuffer(s, new Texture(), clearColor);
		}

		public IFrameBuffer CreateFrameBuffer(Size s, ITextureInternal texture, Color clearColor)
		{
			VerifyThreadAffinity();
			return new FrameBuffer(s, texture, clearColor);
		}

		public IShader CreateShader(string name)
		{
			VerifyThreadAffinity();
			return new Shader(name);
		}

		public void EnableScissor(int x, int y, int width, int height)
		{
			VerifyThreadAffinity();

			if (width < 0)
				width = 0;

			if (height < 0)
				height = 0;

			var windowSize = window.WindowSize;
			var windowScale = window.WindowScale;
			var surfaceSize = window.SurfaceSize;

			if (windowSize != surfaceSize)
			{
				x = (int)Math.Round(windowScale * x);
				y = (int)Math.Round(windowScale * y);
				width = (int)Math.Round(windowScale * width);
				height = (int)Math.Round(windowScale * height);
			}

			OpenGL.glScissor(x, y, width, height);
			OpenGL.CheckGLError();
			OpenGL.glEnable(OpenGL.GL_SCISSOR_TEST);
			OpenGL.CheckGLError();
		}

		public void DisableScissor()
		{
			VerifyThreadAffinity();
			OpenGL.glDisable(OpenGL.GL_SCISSOR_TEST);
			OpenGL.CheckGLError();
		}

		public void Present()
		{
			VerifyThreadAffinity();
			SDL.SDL_GL_SwapWindow(window.Window);
		}

		static int ModeFromPrimitiveType(PrimitiveType pt)
		{
			switch (pt)
			{
				case PrimitiveType.PointList: return OpenGL.GL_POINTS;
				case PrimitiveType.LineList: return OpenGL.GL_LINES;
				case PrimitiveType.TriangleList: return OpenGL.GL_TRIANGLES;
			}

			throw new NotImplementedException();
		}

		public void DrawPrimitives(PrimitiveType pt, int firstVertex, int numVertices)
		{
			VerifyThreadAffinity();
			OpenGL.glDrawArrays(ModeFromPrimitiveType(pt), firstVertex, numVertices);
			OpenGL.CheckGLError();
		}

		public void Clear()
		{
			VerifyThreadAffinity();
			OpenGL.glClearColor(0, 0, 0, 1);
			OpenGL.CheckGLError();
			OpenGL.glClear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.CheckGLError();
		}

		public void EnableDepthBuffer()
		{
			VerifyThreadAffinity();
			OpenGL.glClear(OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.CheckGLError();
			OpenGL.glEnable(OpenGL.GL_DEPTH_TEST);
			OpenGL.CheckGLError();
			OpenGL.glDepthFunc(OpenGL.GL_LEQUAL);
			OpenGL.CheckGLError();
		}

		public void DisableDepthBuffer()
		{
			VerifyThreadAffinity();
			OpenGL.glDisable(OpenGL.GL_DEPTH_TEST);
			OpenGL.CheckGLError();
		}

		public void ClearDepthBuffer()
		{
			VerifyThreadAffinity();
			OpenGL.glClear(OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.CheckGLError();
		}

		public void SetBlendMode(BlendMode mode)
		{
			VerifyThreadAffinity();
			OpenGL.glBlendEquation(OpenGL.GL_FUNC_ADD);
			OpenGL.CheckGLError();

			switch (mode)
			{
				case BlendMode.None:
					OpenGL.glDisable(OpenGL.GL_BLEND);
					break;
				case BlendMode.Alpha:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_ONE, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
					break;
				case BlendMode.Additive:
				case BlendMode.Subtractive:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_ONE, OpenGL.GL_ONE);
					if (mode == BlendMode.Subtractive)
					{
						OpenGL.CheckGLError();
						OpenGL.glBlendEquationSeparate(OpenGL.GL_FUNC_REVERSE_SUBTRACT, OpenGL.GL_FUNC_ADD);
					}

					break;
				case BlendMode.Multiply:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
					OpenGL.CheckGLError();
					break;
				case BlendMode.Multiplicative:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_ZERO, OpenGL.GL_SRC_COLOR);
					break;
				case BlendMode.DoubleMultiplicative:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_SRC_COLOR);
					break;
				case BlendMode.LowAdditive:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_ONE);
					break;
				case BlendMode.Screen:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_SRC_COLOR, OpenGL.GL_ONE_MINUS_SRC_COLOR);
					break;
				case BlendMode.Translucent:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.CheckGLError();
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_ONE_MINUS_DST_COLOR);
					break;
			}

			OpenGL.CheckGLError();
		}

		public void SetVSyncEnabled(bool enabled)
		{
			VerifyThreadAffinity();
			SDL.SDL_GL_SetSwapInterval(enabled ? 1 : 0);
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			if (context != IntPtr.Zero)
			{
				SDL.SDL_GL_DeleteContext(context);
				context = IntPtr.Zero;
			}
		}

		public string GLVersion { get { return OpenGL.Version; } }
	}
}
