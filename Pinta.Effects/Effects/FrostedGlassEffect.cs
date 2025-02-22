/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Marco Rolappe <m_rolappe@gmx.net>                       //
/////////////////////////////////////////////////////////////////////////////////

using System;
using Cairo;
using Pinta.Core;
using Pinta.Gui.Widgets;

namespace Pinta.Effects;

public sealed class FrostedGlassEffect : BaseEffect
{
	public override string Icon => Pinta.Resources.Icons.EffectsDistortFrostedGlass;

	public sealed override bool IsTileable => true;

	public override string Name => Translations.GetString ("Frosted Glass");

	public override bool IsConfigurable => true;

	public override string EffectMenuCategory => Translations.GetString ("Distort");

	public FrostedGlassData Data => (FrostedGlassData) EffectData!;

	private readonly Random random = new ();

	public FrostedGlassEffect ()
	{
		EffectData = new FrostedGlassData ();
	}

	public override void LaunchConfiguration ()
	{
		EffectHelper.LaunchSimpleEffectDialog (this);
	}

	#region Algorithm Code Ported From PDN

	private sealed record FrostedGlassSettings (
		int amount,
		int src_width,
		int src_height);
	private FrostedGlassSettings CreateSettings (ImageSurface src)
	{
		return new (
			amount: Data.Amount,
			src_width: src.Width,
			src_height: src.Height
		);
	}

	public override void Render (ImageSurface src, ImageSurface dst, ReadOnlySpan<RectangleI> rois)
	{
		FrostedGlassSettings settings = CreateSettings (src);

		ReadOnlySpan<ColorBgra> src_data = src.GetReadOnlyPixelData ();
		Span<ColorBgra> dst_data = dst.GetPixelData ();

		foreach (var rect in rois) {
			for (int y = rect.Top; y <= rect.Bottom; ++y) {

				var dst_row = dst_data.Slice (y * settings.src_width, settings.src_width);
				int top = y - settings.amount;
				int bottom = y + settings.amount + 1;

				if (top < 0)
					top = 0;

				if (bottom > settings.src_height)
					bottom = settings.src_height;

				for (int x = rect.Left; x <= rect.Right; ++x)
					dst_row[x] = GetFinalPixelColor (settings, src_data, top, bottom, x);
			}
		}
	}

	private ColorBgra GetFinalPixelColor (FrostedGlassSettings settings, ReadOnlySpan<ColorBgra> src_data, int top, int bottom, int x)
	{
		int intensityChoicesIndex = 0;

		Span<int> intensityCount = stackalloc int[256];
		Span<uint> avgRed = stackalloc uint[256];
		Span<uint> avgGreen = stackalloc uint[256];
		Span<uint> avgBlue = stackalloc uint[256];
		Span<uint> avgAlpha = stackalloc uint[256];
		Span<byte> intensityChoices = stackalloc byte[(1 + (settings.amount * 2)) * (1 + (settings.amount * 2))];

		intensityCount.Clear ();
		avgRed.Clear ();
		avgGreen.Clear ();
		avgBlue.Clear ();
		avgAlpha.Clear ();
		intensityChoices.Clear ();

		int left = x - settings.amount;
		int right = x + settings.amount + 1;

		if (left < 0) {
			left = 0;
		}

		if (right > settings.src_width) {
			right = settings.src_width;
		}

		for (int j = top; j < bottom; ++j) {

			if (j < 0 || j >= settings.src_height)
				continue;

			var src_row = src_data.Slice (j * settings.src_width, settings.src_width);

			for (int i = left; i < right; ++i) {
				ColorBgra src_pixel = src_row[i];
				byte intensity = src_pixel.GetIntensityByte ();

				intensityChoices[intensityChoicesIndex] = intensity;
				++intensityChoicesIndex;

				++intensityCount[intensity];

				avgRed[intensity] += src_pixel.R;
				avgGreen[intensity] += src_pixel.G;
				avgBlue[intensity] += src_pixel.B;
				avgAlpha[intensity] += src_pixel.A;
			}
		}

		int randNum;
		lock (random) {
			randNum = random.Next (intensityChoicesIndex);
		}

		byte chosenIntensity = intensityChoices[randNum];

		return ColorBgra.FromBgra (
			b: (byte) (avgBlue[chosenIntensity] / intensityCount[chosenIntensity]),
			g: (byte) (avgGreen[chosenIntensity] / intensityCount[chosenIntensity]),
			r: (byte) (avgRed[chosenIntensity] / intensityCount[chosenIntensity]),
			a: (byte) (avgAlpha[chosenIntensity] / intensityCount[chosenIntensity])
		);
	}
	#endregion

	public sealed class FrostedGlassData : EffectData
	{
		[Caption ("Amount"), MinimumValue (1), MaximumValue (10)]
		public int Amount { get; set; } = 1;
	}
}
