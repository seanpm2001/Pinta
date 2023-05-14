using System;
using System.Linq;
using Cairo;
using Pinta.Core;
using Pinta.Resources;

namespace Pinta.Tools.Handles
{
	/// <summary>
	/// A handle for specifying a rectangular region.
	/// </summary>
	public class RectangleHandle : IToolHandle
	{
		private PointD start_pt = PointD.Zero;
		private PointD end_pt = PointD.Zero;
		private Size image_size = Size.Empty;
		private readonly MoveHandle[] handles = new MoveHandle[9];
		private MoveHandle? active_handle = null;
		private PointD? drag_start_pos = null;

		public RectangleHandle ()
		{
			handles[0] = new MoveHandle { CursorName = StandardCursors.ResizeNW };
			handles[1] = new MoveHandle { CursorName = StandardCursors.ResizeSW };
			handles[2] = new MoveHandle { CursorName = StandardCursors.ResizeNE };
			handles[3] = new MoveHandle { CursorName = StandardCursors.ResizeSE };
			handles[4] = new MoveHandle { CursorName = StandardCursors.ResizeW };
			handles[5] = new MoveHandle { CursorName = StandardCursors.ResizeN };
			handles[6] = new MoveHandle { CursorName = StandardCursors.ResizeE };
			handles[7] = new MoveHandle { CursorName = StandardCursors.ResizeS };
			handles[8] = new MoveHandle { CursorName = StandardCursors.Move };

			foreach (var handle in handles)
				handle.Active = true;
		}

		#region IToolHandle Implementation
		public bool Active { get; set; }

		public void Draw (Context cr)
		{
			foreach (MoveHandle handle in handles) {
				if (handle.Active)
					handle.Draw (cr);
			}
		}
		#endregion

		/// <summary>
		/// If enabled, dragging the end point before the start point
		/// flips the points to produce a valid rectangle, rather than
		/// producing an empty rectangle.
		/// </summary>
		public bool InvertIfNegative { get; set; }

		/// <summary>
		/// Enables translating the entire rectangle using a handle at the center.
		/// </summary>
		public bool EnableTranslation { get => handles[8].Active; set => handles[8].Active = value; }

		/// <summary>
		/// Bounding rectangle to use with InvalidateWindowRect() when triggering a redraw.
		/// </summary>
		public RectangleI InvalidateRect => MoveHandle.UnionInvalidateRects (handles);

		/// <summary>
		/// Whether the user is currently dragging a corner of the rectangle.
		/// </summary>
		public bool IsDragging => drag_start_pos is not null;

		/// <summary>
		/// The rectangle selected by the user.
		/// </summary>
		public RectangleD Rectangle {
			get => RectangleD.FromPoints (start_pt, end_pt, InvertIfNegative);
			set {
				start_pt = value.Location ();
				end_pt = value.EndLocation ();
				UpdateHandlePositions ();
			}
		}

		/// <summary>
		/// Begins a drag operation if the mouse position is on top of a handle.
		/// Mouse movements are clamped to fall within the specified image size.
		/// </summary>
		public bool BeginDrag (in PointD canvas_pos, in Size image_size)
		{
			if (IsDragging)
				return false;

			this.image_size = image_size;

			PointD view_pos = PintaCore.Workspace.CanvasPointToView (canvas_pos);
			UpdateHandleUnderPoint (view_pos);

			if (active_handle is null)
				return false;

			drag_start_pos = view_pos;
			return true;
		}

		/// <summary>
		/// Updates the rectangle as the mouse is moved.
		/// </summary>
		/// <param name="constrain">Constrain the rectangle to be a square</param>
		/// <returns>The region to redraw with InvalidateWindowRect()</returns>
		public RectangleI UpdateDrag (PointD canvas_pos, bool constrain)
		{
			if (!IsDragging)
				throw new InvalidOperationException ("Drag operation has not been started!");

			// Clamp mouse position to the image size.
			canvas_pos = new PointD (
				Math.Round (Utility.Clamp (canvas_pos.X, 0, image_size.Width)),
				Math.Round (Utility.Clamp (canvas_pos.Y, 0, image_size.Height)));

			var dirty = InvalidateRect;

			MoveActiveHandle (canvas_pos.X, canvas_pos.Y, constrain);
			UpdateHandlePositions ();

			dirty = dirty.Union (InvalidateRect);
			return dirty;
		}

		/// <summary>
		/// If a drag operation is active, returns whether the mouse has actually moved.
		/// This can be used to distinguish a "click" from a "click and drag".
		/// </summary>
		public bool HasDragged (PointD canvas_pos)
		{
			if (drag_start_pos is null)
				throw new InvalidOperationException ("Drag operation has not been started!");

			PointD view_pos = PintaCore.Workspace.CanvasPointToView (canvas_pos);
			return drag_start_pos.Value.Distance (view_pos) > 1;
		}

		/// <summary>
		/// Finishes a drag operation.
		/// </summary>
		public void EndDrag ()
		{
			if (drag_start_pos is null)
				throw new InvalidOperationException ("Drag operation has not been started!");

			image_size = Size.Empty;
			active_handle = null;
			drag_start_pos = null;

			// If the rectangle was inverted, fix inverted start/end points.
			var rect = Rectangle;
			start_pt = rect.Location ();
			end_pt = rect.EndLocation ();
		}

		/// <summary>
		/// Name of the cursor to display, if the cursor is over a corner of the rectangle.
		/// </summary>
		public string? GetCursorAtPoint (PointD view_pos)
			=> handles.FirstOrDefault (c => c.Active && c.ContainsPoint (view_pos))?.CursorName;

		private void UpdateHandlePositions ()
		{
			var rect = Rectangle;
			var center = rect.GetCenter ();
			handles[0].CanvasPosition = new PointD (rect.Left, rect.Top);
			handles[1].CanvasPosition = new PointD (rect.Left, rect.Bottom);
			handles[2].CanvasPosition = new PointD (rect.Right, rect.Top);
			handles[3].CanvasPosition = new PointD (rect.Right, rect.Bottom);
			handles[4].CanvasPosition = new PointD (rect.Left, center.Y);
			handles[5].CanvasPosition = new PointD (center.X, rect.Top);
			handles[6].CanvasPosition = new PointD (rect.Right, center.Y);
			handles[7].CanvasPosition = new PointD (center.X, rect.Bottom);
			handles[8].CanvasPosition = center;
		}

		private void UpdateHandleUnderPoint (PointD view_pos)
		{
			active_handle = handles.FirstOrDefault (c => c.Active && c.ContainsPoint (view_pos));

			// If the rectangle is empty (e.g. starting a new drag), all the handles are
			// at the same position so pick the bottom right corner.
			var rect = Rectangle;
			if (active_handle is not null && rect.Width == 0.0 && rect.Height == 0.0)
				active_handle = handles[3];
		}

		private void MoveActiveHandle (double x, double y, bool constrain)
		{
			// Update the rectangle's size depending on which handle was dragged.
			switch (Array.IndexOf (handles, active_handle)) {
				case 0:
					start_pt.X = x;
					start_pt.Y = y;
					if (constrain) {
						if (end_pt.X - start_pt.X <= end_pt.Y - start_pt.Y)
							start_pt.X = end_pt.X - end_pt.Y + start_pt.Y;
						else
							start_pt.Y = end_pt.Y - end_pt.X + start_pt.X;
					}
					break;
				case 1:
					start_pt.X = x;
					end_pt.Y = y;
					if (constrain) {
						if (end_pt.X - start_pt.X <= end_pt.Y - start_pt.Y)
							start_pt.X = end_pt.X - end_pt.Y + start_pt.Y;
						else
							end_pt.Y = start_pt.Y + end_pt.X - start_pt.X;
					}
					break;
				case 2:
					end_pt.X = x;
					start_pt.Y = y;
					if (constrain) {
						if (end_pt.X - start_pt.X <= end_pt.Y - start_pt.Y)
							end_pt.X = start_pt.X + end_pt.Y - start_pt.Y;
						else
							start_pt.Y = end_pt.Y - end_pt.X + start_pt.X;
					}
					break;
				case 3:
					end_pt.X = x;
					end_pt.Y = y;
					if (constrain) {
						if (end_pt.X - start_pt.X <= end_pt.Y - start_pt.Y)
							end_pt.X = start_pt.X + end_pt.Y - start_pt.Y;
						else
							end_pt.Y = start_pt.Y + end_pt.X - start_pt.X;
					}
					break;
				case 4:
					start_pt.X = x;
					if (constrain) {
						var d = end_pt.X - start_pt.X;
						start_pt.Y = (start_pt.Y + end_pt.Y - d) / 2;
						end_pt.Y = (start_pt.Y + end_pt.Y + d) / 2;
					}
					break;
				case 5:
					start_pt.Y = y;
					if (constrain) {
						var d = end_pt.Y - start_pt.Y;
						start_pt.X = (start_pt.X + end_pt.X - d) / 2;
						end_pt.X = (start_pt.X + end_pt.X + d) / 2;
					}
					break;
				case 6:
					end_pt.X = x;
					if (constrain) {
						var d = end_pt.X - start_pt.X;
						start_pt.Y = (start_pt.Y + end_pt.Y - d) / 2;
						end_pt.Y = (start_pt.Y + end_pt.Y + d) / 2;
					}
					break;
				case 7:
					end_pt.Y = y;
					if (constrain) {
						var d = end_pt.Y - start_pt.Y;
						start_pt.X = (start_pt.X + end_pt.X - d) / 2;
						end_pt.X = (start_pt.X + end_pt.X + d) / 2;
					}
					break;
				case 8:
					double width = end_pt.X - start_pt.X;
					double height = end_pt.Y - start_pt.Y;
					start_pt = new (x - 0.5 * width, y - 0.5 * height);
					end_pt = new (x + 0.5 * width, y + 0.5 * height);
					break;
				default:
					throw new ArgumentOutOfRangeException ("handle");
			}
		}
	}
}

