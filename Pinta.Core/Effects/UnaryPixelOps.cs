/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) Rick Brewster, Tom Jackson, and past contributors.            //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Immutable;
using System.Linq;

namespace Pinta.Core;

/// <summary>
/// Provides a set of standard UnaryPixelOps.
/// </summary>
public static class UnaryPixelOps
{
	/// <summary>
	/// Passes through the given color value.
	/// result(color) = color
	/// </summary>
	[Serializable]
	public sealed class Identity : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			return color;
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; i++)
				dst[i] = src[i];
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			return;
		}
	}

	/// <summary>
	/// Always returns a constant color.
	/// </summary>
	[Serializable]
	public sealed class Constant : UnaryPixelOp
	{
		private ColorBgra set_color;

		public override ColorBgra Apply (in ColorBgra color)
		{
			return set_color;
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; i++)
				dst[i] = set_color;
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			for (int i = 0; i < dst.Length; i++)
				dst[i] = set_color;
		}

		public Constant (ColorBgra setColor)
		{
			this.set_color = setColor;
		}
	}

	/// <summary>
	/// Blends pixels with the specified constant color.
	/// </summary>
	[Serializable]
	public sealed class BlendConstant : UnaryPixelOp
	{
		private ColorBgra blend_color;

		public override ColorBgra Apply (in ColorBgra color)
		{
			int a = blend_color.A;
			int invA = 255 - a;

			int r = ((color.R * invA) + (blend_color.R * a)) / 256;
			int g = ((color.G * invA) + (blend_color.G * a)) / 256;
			int b = ((color.B * invA) + (blend_color.B * a)) / 256;
			byte a2 = ComputeAlpha (color.A, blend_color.A);

			return ColorBgra.FromBgra ((byte) b, (byte) g, (byte) r, a2);
		}

		public BlendConstant (ColorBgra blendColor)
		{
			this.blend_color = blendColor;
		}
	}

	/// <summary>
	/// Used to set a given channel of a pixel to a given, predefined color.
	/// Useful if you want to set only the alpha value of a given region.
	/// </summary>
	[Serializable]
	public sealed class SetChannel : UnaryPixelOp
	{
		private readonly int channel;
		private readonly byte set_value;

		public override ColorBgra Apply (in ColorBgra color)
		{
			ColorBgra result = color;
			result[channel] = set_value;
			return color;
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; ++i) {
				dst[i] = src[i];
				dst[i][channel] = set_value;
			}
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			for (int i = 0; i < dst.Length; ++i)
				dst[i][channel] = set_value;
		}

		public SetChannel (int channel, byte setValue)
		{
			this.channel = channel;
			this.set_value = setValue;
		}
	}

	/// <summary>
	/// Specialization of SetChannel that sets the alpha channel.
	/// </summary>
	/// <remarks>This class depends on the system being litte-endian with the alpha channel 
	/// occupying the 8 most-significant-bits of a ColorBgra instance.
	/// By the way, we use addition instead of bitwise-OR because an addition can be
	/// perform very fast (0.5 cycles) on a Pentium 4.</remarks>
	[Serializable]
	public sealed class SetAlphaChannel : UnaryPixelOp
	{
		private readonly UInt32 add_value;

		public override ColorBgra Apply (in ColorBgra color)
		{
			return ColorBgra.FromUInt32 ((color.Bgra & 0x00ffffff) + add_value);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; ++i)
				dst[i].Bgra = (src[i].Bgra & 0x00ffffff) + add_value;
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			for (int i = 0; i < dst.Length; ++i)
				dst[i].Bgra = (dst[i].Bgra & 0x00ffffff) + add_value;
		}

		public SetAlphaChannel (byte alphaValue)
		{
			add_value = (uint) alphaValue << 24;
		}
	}

	/// <summary>
	/// Specialization of SetAlphaChannel that always sets alpha to 255.
	/// </summary>
	[Serializable]
	public sealed class SetAlphaChannelTo255 : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			return ColorBgra.FromUInt32 (color.Bgra | 0xff000000);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; ++i)
				dst[i].Bgra = src[i].Bgra | 0xff000000;
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			for (int i = 0; i < dst.Length; ++i)
				dst[i].Bgra |= 0xff000000;
		}
	}

	/// <summary>
	/// Inverts a pixel's color, and passes through the alpha component.
	/// </summary>
	[Serializable]
	public sealed class Invert : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			//Note: Cairo images use premultiplied alpha values
			//The formula for changing B would be: (255 - B * 255 / A) * A / 255
			//This can be simplified to: A - B
			return ColorBgra.FromBgra ((byte) (color.A - color.B), (byte) (color.A - color.G), (byte) (color.A - color.R), color.A);
		}
	}

	/// <summary>
	/// If the color is within the red tolerance, remove it
	/// </summary>
	[Serializable]
	public sealed class RedEyeRemove : UnaryPixelOp
	{
		private readonly int tolerance;
		private readonly double set_saturation;

		public RedEyeRemove (int tol, int sat)
		{
			tolerance = tol;
			set_saturation = (double) sat / 100;
		}

		public override ColorBgra Apply (in ColorBgra color)
		{
			// The higher the saturation, the more red it is
			int saturation = GetSaturation (color);

			// The higher the difference between the other colors, the more red it is
			int difference = color.R - Math.Max (color.B, color.G);

			// If it is within tolerance, and the saturation is high
			if ((difference > tolerance) && (saturation > 100)) {
				double i = 255.0 * color.GetIntensity ();
				byte ib = (byte) (i * set_saturation); // adjust the red color for user inputted saturation
				return ColorBgra.FromBgra ((byte) color.B, (byte) color.G, ib, color.A);
			} else {
				return color;
			}
		}

		//Saturation formula from RgbColor.cs, public HsvColor ToHsv()
		private static int GetSaturation (in ColorBgra color)
		{
			double min;
			double max;
			double delta;

			double r = (double) color.R / 255;
			double g = (double) color.G / 255;
			double b = (double) color.B / 255;

			double s;

			min = Math.Min (Math.Min (r, g), b);
			max = Math.Max (Math.Max (r, g), b);
			delta = max - min;

			if (max == 0 || delta == 0) {
				// R, G, and B must be 0, or all the same.
				// In this case, S is 0, and H is undefined.
				// Using H = 0 is as good as any...
				s = 0;
			} else {
				s = delta / max;
			}

			return (int) (s * 255);
		}
	}

	/// <summary>
	/// Inverts a pixel's color and its alpha component.
	/// </summary>
	[Serializable]
	public sealed class InvertWithAlpha : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			return ColorBgra.FromBgra ((byte) (255 - color.B), (byte) (255 - color.G), (byte) (255 - color.R), (byte) (255 - color.A));
		}
	}

	/// <summary>
	/// Averages the input color's red, green, and blue channels. The alpha component
	/// is unaffected.
	/// </summary>
	[Serializable]
	public sealed class AverageChannels : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			byte average = (byte) (((int) color.R + (int) color.G + (int) color.B) / 3);
			return ColorBgra.FromBgra (average, average, average, color.A);
		}
	}

	[Serializable]
	public sealed class Desaturate : UnaryPixelOp
	{
		public override ColorBgra Apply (in ColorBgra color)
		{
			byte i = color.GetIntensityByte ();
			return ColorBgra.FromBgra (i, i, i, color.A);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; ++i) {
				ref ColorBgra result = ref dst[i];
				byte val = src[i].GetIntensityByte ();
				dst[i] = ColorBgra.FromBgra (val, val, val, src[i].A);
				result.R = result.G = result.B = val;
				result.A = src[i].A;
			}
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			for (int i = 0; i < dst.Length; ++i) {
				ref ColorBgra result = ref dst[i];
				byte val = result.GetIntensityByte ();
				result.R = result.G = result.B = val;

			}
		}
	}

	[Serializable]
	public sealed class LuminosityCurve : UnaryPixelOp
	{
		public byte[] Curve { get; }
		public LuminosityCurve ()
		{
			var curve = new byte[256];
			for (int i = 0; i < 256; ++i) {
				curve[i] = (byte) i;
			}
			Curve = curve;
		}

		public override ColorBgra Apply (in ColorBgra color)
		{
			byte lumi = color.GetIntensityByte ();
			int diff = Curve[lumi] - lumi;

			return ColorBgra.FromBgraClamped (
			    color.B + diff,
			    color.G + diff,
			    color.R + diff,
			    color.A);
		}
	}

	[Serializable]
	public class ChannelCurve : UnaryPixelOp
	{
		public byte[] CurveB { get; internal set; }
		public byte[] CurveG { get; internal set; }
		public byte[] CurveR { get; internal set; }

		public ChannelCurve ()
		{
			var curveB = new byte[256];
			var curveG = new byte[256];
			var curveR = new byte[256];
			for (int i = 0; i < 256; ++i) {
				curveB[i] = (byte) i;
				curveG[i] = (byte) i;
				curveR[i] = (byte) i;
			}
			CurveB = curveB;
			CurveG = curveG;
			CurveR = curveR;
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; ++i) {
				ref ColorBgra d = ref dst[i];
				ref readonly ColorBgra s = ref src[i];
				d = ColorBgra.FromBgra (
					b: CurveB[s.B],
					g: CurveG[s.G],
					r: CurveR[s.R],
					a: s.A
				);
			}
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			for (int i = 0; i < dst.Length; ++i) {
				ref ColorBgra d = ref dst[i];
				d.B = CurveB[d.B];
				d.G = CurveG[d.G];
				d.R = CurveR[d.R];

			}
		}

		public override ColorBgra Apply (in ColorBgra color)
		{
			return ColorBgra.FromBgra (CurveB[color.B], CurveG[color.G], CurveR[color.R], color.A);
		}

		//            public override void Apply(Surface dst, Point dstOffset, Surface src, Point srcOffset, int scanLength)
		//            {
		//                base.Apply (dst, dstOffset, src, srcOffset, scanLength);
		//            }
	}

	[Serializable]
	public sealed class Level : ChannelCurve, ICloneable
	{
		private ColorBgra color_in_low;
		public ColorBgra ColorInLow {
			get => color_in_low;

			set {
				if (value.R == 255) {
					value.R = 254;
				}

				if (value.G == 255) {
					value.G = 254;
				}

				if (value.B == 255) {
					value.B = 254;
				}

				if (color_in_high.R < value.R + 1) {
					color_in_high.R = (byte) (value.R + 1);
				}

				if (color_in_high.G < value.G + 1) {
					color_in_high.G = (byte) (value.R + 1);
				}

				if (color_in_high.B < value.B + 1) {
					color_in_high.B = (byte) (value.R + 1);
				}

				color_in_low = value;
				UpdateLookupTable ();
			}
		}

		private ColorBgra color_in_high;
		public ColorBgra ColorInHigh {
			get => color_in_high;

			set {
				if (value.R == 0) {
					value.R = 1;
				}

				if (value.G == 0) {
					value.G = 1;
				}

				if (value.B == 0) {
					value.B = 1;
				}

				if (color_in_low.R > value.R - 1) {
					color_in_low.R = (byte) (value.R - 1);
				}

				if (color_in_low.G > value.G - 1) {
					color_in_low.G = (byte) (value.R - 1);
				}

				if (color_in_low.B > value.B - 1) {
					color_in_low.B = (byte) (value.R - 1);
				}

				color_in_high = value;
				UpdateLookupTable ();
			}
		}

		private ColorBgra color_out_low;
		public ColorBgra ColorOutLow {
			get => color_out_low;

			set {
				if (value.R == 255) {
					value.R = 254;
				}

				if (value.G == 255) {
					value.G = 254;
				}

				if (value.B == 255) {
					value.B = 254;
				}

				if (color_out_high.R < value.R + 1) {
					color_out_high.R = (byte) (value.R + 1);
				}

				if (color_out_high.G < value.G + 1) {
					color_out_high.G = (byte) (value.G + 1);
				}

				if (color_out_high.B < value.B + 1) {
					color_out_high.B = (byte) (value.B + 1);
				}

				color_out_low = value;
				UpdateLookupTable ();
			}
		}

		private ColorBgra color_out_high;
		public ColorBgra ColorOutHigh {
			get => color_out_high;

			set {
				if (value.R == 0) {
					value.R = 1;
				}

				if (value.G == 0) {
					value.G = 1;
				}

				if (value.B == 0) {
					value.B = 1;
				}

				if (color_out_low.R > value.R - 1) {
					color_out_low.R = (byte) (value.R - 1);
				}

				if (color_out_low.G > value.G - 1) {
					color_out_low.G = (byte) (value.G - 1);
				}

				if (color_out_low.B > value.B - 1) {
					color_out_low.B = (byte) (value.B - 1);
				}

				color_out_high = value;
				UpdateLookupTable ();
			}
		}

		private readonly float[] gamma = new float[3];
		public float GetGamma (int index)
		{
			if (index < 0 || index >= 3) {
				throw new ArgumentOutOfRangeException (nameof (index), index, "Index must be between 0 and 2");
			}

			return gamma[index];
		}

		public void SetGamma (int index, float val)
		{
			if (index < 0 || index >= 3) {
				throw new ArgumentOutOfRangeException (nameof (index), index, "Index must be between 0 and 2");
			}

			gamma[index] = Math.Clamp (val, 0.1f, 10.0f);
			UpdateLookupTable ();
		}

		public bool IsValid { get; private set; } = true;

		public static Level AutoFromLoMdHi (ColorBgra lo, ColorBgra md, ColorBgra hi)
		{
			float[] gamma = new float[3];

			for (int i = 0; i < 3; i++) {
				if (lo[i] < md[i] && md[i] < hi[i]) {
					gamma[i] = (float) Math.Clamp (Math.Log (0.5, (float) (md[i] - lo[i]) / (float) (hi[i] - lo[i])), 0.1, 10.0);
				} else {
					gamma[i] = 1.0f;
				}
			}

			return new Level (lo, hi, gamma, ColorBgra.Black, ColorBgra.White);
		}

		private void UpdateLookupTable ()
		{
			for (int i = 0; i < 3; i++) {
				if (color_out_high[i] < color_out_low[i] ||
				    color_in_high[i] <= color_in_low[i] ||
				    gamma[i] < 0) {
					IsValid = false;
					return;
				}

				for (int j = 0; j < 256; j++) {
					ColorBgra col = Apply (j, j, j);
					CurveB[j] = col.B;
					CurveG[j] = col.G;
					CurveR[j] = col.R;
				}
			}
		}

		public Level ()
		    : this (ColorBgra.Black,
			   ColorBgra.White,
			   new float[] { 1, 1, 1 },
			   ColorBgra.Black,
			   ColorBgra.White)
		{
		}

		public Level (ColorBgra in_lo, ColorBgra in_hi, float[] gamma, ColorBgra out_lo, ColorBgra out_hi)
		{
			color_in_low = in_lo;
			color_in_high = in_hi;
			color_out_low = out_lo;
			color_out_high = out_hi;

			if (gamma.Length != 3)
				throw new ArgumentException ($"{nameof (gamma)} must be a float[3]", nameof (gamma));

			this.gamma = gamma;
			UpdateLookupTable ();
		}

		public ColorBgra Apply (float r, float g, float b)
		{
			ColorBgra ret = new ColorBgra ();
			ReadOnlySpan<float> input = stackalloc float[] { b, g, r };

			for (int i = 0; i < 3; i++) {
				float v = (input[i] - color_in_low[i]);

				if (v < 0) {
					ret[i] = color_out_low[i];
				} else if (v + color_in_low[i] >= color_in_high[i]) {
					ret[i] = color_out_high[i];
				} else {
					ret[i] = (byte) Math.Clamp (
					    color_out_low[i] + (color_out_high[i] - color_out_low[i]) * Math.Pow (v / (color_in_high[i] - color_in_low[i]), gamma[i]),
					    0.0f,
					    255.0f);
				}
			}

			return ret;
		}

		public void UnApply (ColorBgra after, Span<float> beforeOut, Span<float> slopesOut)
		{
			if (beforeOut.Length != 3)
				throw new ArgumentException ($"{nameof (beforeOut)} must be a float[3]", nameof (beforeOut));

			if (slopesOut.Length != 3)
				throw new ArgumentException ($"{nameof (slopesOut)} must be a float[3]", nameof (slopesOut));

			for (int i = 0; i < 3; i++) {

				beforeOut[i] = color_in_low[i] + (color_in_high[i] - color_in_low[i]) *
				    (float) Math.Pow ((float) (after[i] - color_out_low[i]) / (color_out_high[i] - color_out_low[i]), 1 / gamma[i]);

				slopesOut[i] = (float) (color_in_high[i] - color_in_low[i]) / ((color_out_high[i] - color_out_low[i]) * gamma[i]) *
				    (float) Math.Pow ((float) (after[i] - color_out_low[i]) / (color_out_high[i] - color_out_low[i]), 1 / gamma[i] - 1);

				if (float.IsInfinity (slopesOut[i]) || float.IsNaN (slopesOut[i]))
					slopesOut[i] = 0;
			}
		}

		public object Clone ()
		{
			Level copy = new Level (color_in_low, color_in_high, (float[]) gamma.Clone (), color_out_low, color_out_high) {
				CurveB = CurveB.ToArray (),
				CurveG = CurveG.ToArray (),
				CurveR = CurveR.ToArray ()
			};

			return copy;
		}
	}

	[Serializable]
	public sealed class HueSaturationLightness : UnaryPixelOp
	{
		private readonly int hue_delta;
		private readonly int sat_factor;
		private readonly UnaryPixelOp blend_op;

		public HueSaturationLightness (int hueDelta, int satDelta, int lightness)
		{
			this.hue_delta = hueDelta;
			this.sat_factor = (satDelta * 1024) / 100;

			if (lightness == 0) {
				blend_op = new UnaryPixelOps.Identity ();
			} else if (lightness > 0) {
				blend_op = new UnaryPixelOps.BlendConstant (ColorBgra.FromBgra (255, 255, 255, (byte) ((lightness * 255) / 100)));
			} else // if (lightness < 0)
			  {
				blend_op = new UnaryPixelOps.BlendConstant (ColorBgra.FromBgra (0, 0, 0, (byte) ((-lightness * 255) / 100)));
			}
		}

		public override ColorBgra Apply (in ColorBgra src_color)
		{
			//adjust saturation
			ColorBgra color = src_color;
			byte intensity = color.GetIntensityByte ();
			color.R = Utility.ClampToByte ((intensity * 1024 + (color.R - intensity) * sat_factor) >> 10);
			color.G = Utility.ClampToByte ((intensity * 1024 + (color.G - intensity) * sat_factor) >> 10);
			color.B = Utility.ClampToByte ((intensity * 1024 + (color.B - intensity) * sat_factor) >> 10);

			HsvColor hsvColor = (new RgbColor (color.R, color.G, color.B)).ToHsv ();
			int newHue = hsvColor.Hue;

			newHue += hue_delta;

			while (newHue < 0) {
				newHue += 360;
			}

			while (newHue > 360) {
				newHue -= 360;
			}

			hsvColor = new HsvColor (newHue, hsvColor.Saturation, hsvColor.Value);

			RgbColor rgbColor = hsvColor.ToRgb ();
			ColorBgra newColor = ColorBgra.FromBgr ((byte) rgbColor.Blue, (byte) rgbColor.Green, (byte) rgbColor.Red);
			newColor = blend_op.Apply (newColor);
			newColor.A = color.A;

			return newColor;
		}

	}

	[Serializable]
	public sealed class PosterizePixel : UnaryPixelOp
	{
		private readonly ImmutableArray<byte> red_levels;
		private readonly ImmutableArray<byte> green_levels;
		private readonly ImmutableArray<byte> blue_levels;

		public PosterizePixel (int red, int green, int blue)
		{
			this.red_levels = CalcLevels (red);
			this.green_levels = CalcLevels (green);
			this.blue_levels = CalcLevels (blue);
		}

		private static ImmutableArray<byte> CalcLevels (int levelCount)
		{
			Span<byte> t1 = stackalloc byte[levelCount];

			for (int i = 1; i < levelCount; i++) {
				t1[i] = (byte) ((255 * i) / (levelCount - 1));
			}

			var levels = ImmutableArray.CreateBuilder<byte> (256);
			levels.Count = 256;

			int j = 0;
			int k = 0;

			for (int i = 0; i < 256; i++) {
				levels[i] = t1[j];

				k += levelCount;

				if (k > 255) {
					k -= 255;
					j++;
				}
			}

			return levels.MoveToImmutable ();
		}

		public override ColorBgra Apply (in ColorBgra color)
		{
			return ColorBgra.FromBgra (blue_levels[color.B], green_levels[color.G], red_levels[color.R], color.A);
		}

		public override void Apply (Span<ColorBgra> dst, ReadOnlySpan<ColorBgra> src)
		{
			for (int i = 0; i < src.Length; ++i) {
				ref ColorBgra d = ref dst[i];
				ref readonly ColorBgra s = ref src[i];
				d.B = blue_levels[s.B];
				d.G = green_levels[s.G];
				d.R = red_levels[s.R];
				d.A = s.A;
			}
		}

		public override void Apply (Span<ColorBgra> dst)
		{
			for (int i = 0; i < dst.Length; ++i) {
				ref ColorBgra d = ref dst[i];
				d.B = blue_levels[d.B];
				d.G = green_levels[d.G];
				d.R = red_levels[d.R];

			}
		}
	}

}
