// 
// BaseTransformTool.cs
//  
// Author:
//       Volodymyr <${AuthorEmail}>
// 
// Copyright (c) 2012 Volodymyr
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using Pinta.Core;

namespace Pinta.Tools
{
	public abstract class BaseTransformTool : BaseTool
	{
		private readonly int rotate_steps = 32;
		private readonly Matrix transform = CairoExtensions.CreateIdentityMatrix ();
		private RectangleD source_rect;
		private PointD original_point;
		private bool is_dragging = false;
		private bool is_rotating = false;
		private bool is_scaling = false;
		private bool using_mouse = false;
		private readonly Handles.RectangleHandle rect_handle;

		public override IEnumerable<IToolHandle> Handles => Enumerable.Repeat (rect_handle, 1);

		/// <summary>
		/// Initializes a new instance of the <see cref="Pinta.Tools.BaseTransformTool"/> class.
		/// </summary>
		public BaseTransformTool (IServiceManager services) : base (services)
		{
			rect_handle = new () { InvertIfNegative = false, EnableTranslation = false, Active = true };
		}

		protected override void OnActivated (Document? document)
		{
			base.OnActivated (document);

			if (document is not null)
				HandleSourceRectangleChanged (document);
		}

		protected override void OnMouseDown (Document document, ToolMouseEventArgs e)
		{
			if (IsActive)
				return;

			original_point = e.PointDouble;

			if (!document.Workspace.PointInCanvas (e.PointDouble))
				return;

			if (e.MouseButton == MouseButton.Right)
				is_rotating = true;
			else if (rect_handle.BeginDrag (e.PointDouble, document.ImageSize))
				is_scaling = true;
			else
				is_dragging = true;

			using_mouse = true;

			OnStartTransform (document);
		}

		protected override void OnMouseMove (Document document, ToolMouseEventArgs e)
		{
			if (!IsActive || !using_mouse) {
				UpdateCursor (e.WindowPoint);
				return;
			}

			var constrain = e.IsShiftPressed;

			transform.InitIdentity ();

			if (is_scaling) {
				// TODO - the constrain option should preserve the original aspect ratio, rather than creating a square.
				rect_handle.UpdateDrag (e.PointDouble, constrain);

				// Scale the original rectangle to fit the target rectangle.
				var target_rect = rect_handle.Rectangle;
				var sx = (source_rect.Width > 0) ? (target_rect.Width / source_rect.Width) : 0.0;
				var sy = (source_rect.Height > 0) ? (target_rect.Height / source_rect.Height) : 0.0;

				transform.Translate (target_rect.Left, target_rect.Top);
				transform.Scale (sx, sy);
				transform.Translate (-source_rect.Left, -source_rect.Top);

			} else if (is_rotating) {
				var center = source_rect.GetCenter ();

				var cx1 = original_point.X - center.X;
				var cy1 = original_point.Y - center.Y;

				var cx2 = e.PointDouble.X - center.X;
				var cy2 = e.PointDouble.Y - center.Y;

				var angle = Math.Atan2 (cy1, cx1) - Math.Atan2 (cy2, cx2);

				if (constrain)
					angle = Utility.GetNearestStepAngle (angle, rotate_steps);

				transform.Translate (center.X, center.Y);
				transform.Rotate (-angle);
				transform.Translate (-center.X, -center.Y);
			} else {
				// The cursor position can be a subpixel value. Round to an integer
				// so that we only translate by entire pixels.
				// (Otherwise, blurring / anti-aliasing may be introduced)
				var dx = Math.Floor (e.PointDouble.X - original_point.X);
				var dy = Math.Floor (e.PointDouble.Y - original_point.Y);
				transform.Translate (dx, dy);
			}

			OnUpdateTransform (document, transform);
		}

		protected override void OnMouseUp (Document document, ToolMouseEventArgs e)
		{
			if (!IsActive || !using_mouse)
				return;

			if (is_scaling) {
				rect_handle.EndDrag ();
			}

			OnFinishTransform (document, transform);
			UpdateCursor (e.WindowPoint);
		}

		protected override bool OnKeyDown (Document document, ToolKeyEventArgs e)
		{
			// Don't handle the arrow keys while already interacting via the mouse.
			if (using_mouse)
				return base.OnKeyDown (document, e);

			var dx = 0.0;
			var dy = 0.0;

			switch (e.Key) {
				case Gdk.Key.Left:
					dx = -1;
					break;
				case Gdk.Key.Right:
					dx = 1;
					break;
				case Gdk.Key.Up:
					dy = -1;
					break;
				case Gdk.Key.Down:
					dy = 1;
					break;
				default:
					// Otherwise, let the key be handled elsewhere.
					return base.OnKeyDown (document, e);
			}

			if (!IsActive) {
				is_dragging = true;
				OnStartTransform (document);
			}

			transform.Translate (dx, dy);
			OnUpdateTransform (document, transform);

			return true;
		}

		protected override bool OnKeyUp (Document document, ToolKeyEventArgs e)
		{
			if (IsActive && !using_mouse)
				OnFinishTransform (document, transform);

			return base.OnKeyUp (document, e);
		}

		protected abstract RectangleD GetSourceRectangle (Document document);

		protected virtual void OnStartTransform (Document document)
		{
			source_rect = GetSourceRectangle (document);
			transform.InitIdentity ();
		}

		protected virtual void OnUpdateTransform (Document document, Matrix transform)
		{
		}

		protected virtual void OnFinishTransform (Document document, Matrix transform)
		{
			is_dragging = false;
			is_rotating = false;
			is_scaling = false;
			using_mouse = false;
		}

		protected override void OnAfterUndo (Document document)
		{
			base.OnAfterUndo (document);
			HandleSourceRectangleChanged (document);
		}

		protected override void OnAfterRedo (Document document)
		{
			base.OnAfterRedo (document);
			HandleSourceRectangleChanged (document);
		}

		private void HandleSourceRectangleChanged (Document document)
		{
			// TODO - this needs more testing to ensure the
			// rectangle handle is correctly updated whenever the
			// source rectangle (e.g. selection) changes.
			var dirty = rect_handle.InvalidateRect;
			rect_handle.Rectangle = GetSourceRectangle (document);
			dirty = dirty.Union (rect_handle.InvalidateRect);
			PintaCore.Workspace.InvalidateWindowRect (dirty);
		}

		private void UpdateCursor (in PointD view_pos)
		{
			string? cursor_name = null;
			if (rect_handle.Active)
				cursor_name = rect_handle?.GetCursorAtPoint (view_pos);

			if (cursor_name is not null) {
				SetCursor (Gdk.Cursor.NewFromName (cursor_name, null));
			} else {
				SetCursor (DefaultCursor);
			}
		}

		private bool IsActive => is_dragging || is_rotating || is_scaling;
	}
}

